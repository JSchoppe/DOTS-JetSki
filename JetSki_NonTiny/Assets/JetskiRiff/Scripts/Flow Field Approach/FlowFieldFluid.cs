using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

public sealed class FlowFieldFluid : MonoBehaviour
{
    private struct FieldNode
    {
        public float2 location;
        public int2 coordinate;
        public float height;
    }

    private struct FlowOver
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }


    [SerializeField] private int fieldSize = 2;
    [SerializeField] private float fieldStep = 1f;
    [SerializeField] private float spreadRate = 1f;
    [SerializeField] private float noiseIntensity = 1f;
    [SerializeField] private float heightStep = 1f;

    private void OnValidate()
    {
        if (noiseIntensity < 0f)
            noiseIntensity = 0f;
        if (fieldSize < 2)
            fieldSize = 2;
        if (fieldStep < 0.005f)
            fieldStep = 0.005f;
        if (spreadRate < 0f)
            spreadRate = 0f;
    }

    // private Vector2 currentOrigin;
    private NativeArray<FieldNode> fieldNodes;
    private NativeArray<float> influenceOutput;
    private NativeArray<float> heightOutput;

    private NativeArray<Vector3> vertexOperationArray;
    private Vector3[] verticesJobOutput;

    private JobHandle entropyJobHandle;
    private JobHandle neighborInfluenceHandle;
    private JobHandle applyFlowHandle;
    private JobHandle calculateHeightHandle;
    private JobHandle applyHeightHandle;

    private Mesh dynamicMesh;

    private void Awake()
    {
        float[] startingHeights = new float[fieldSize * fieldSize];
        for (int i = 0; i < startingHeights.Length - 1; i += 2)
        {
            startingHeights[i] = UnityEngine.Random.value * noiseIntensity;
            startingHeights[i + 1] = -startingHeights[i];
        }

        // Generate the flow field nodes.
        // Get the starting corner to iterate from.
        float2 startingCorner = new float2
        {
            x = transform.position.x - ((fieldSize - 1) * fieldStep * 0.5f),
            y = transform.position.z - ((fieldSize - 1) * fieldStep * 0.5f)
        };
        // Generate the nodes in a grid pattern.
        FieldNode[] nodes = new FieldNode[fieldSize * fieldSize];
        for (int y = 0; y < fieldSize; y++)
            for (int x = 0; x < fieldSize; x++)
                nodes[y * fieldSize + x] = new FieldNode
                {
                    coordinate = new int2(x, y),
                    location = startingCorner + new float2
                    {
                        x = x * fieldStep,
                        y = y * fieldStep
                    },
                    // By default the fluid is still.
                    height = startingHeights[y * fieldSize + x]
                };
        // Make the nodes accessible to jobs.
        fieldNodes = new NativeArray<FieldNode>(nodes, Allocator.Persistent);
        // TODO test whether it is faster to have these persistent
        // or declared every loop for the temp job.
        influenceOutput = new NativeArray<float>(nodes.Length, Allocator.Persistent);
        heightOutput = new NativeArray<float>(nodes.Length, Allocator.Persistent);

        // Create a new plane for our waves to populate.
        dynamicMesh = MeshHelper.GenerateQuadGrid(fieldSize - 2, new PlaneDefinition
        {
            origin = new Vector3(startingCorner.x, 0f, startingCorner.y),
            axisA = Vector3.right,
            axisB = Vector3.forward,
            lengthA = (fieldSize - 1) * fieldStep,
            lengthB = (fieldSize - 1) * fieldStep
        }, 3f);
        dynamicMesh.MarkDynamic();
        GetComponent<MeshFilter>().mesh = dynamicMesh;

        // Allocate space for the job that will act on the vertices.
        vertexOperationArray = new NativeArray<Vector3>(dynamicMesh.vertices, Allocator.Persistent);
        verticesJobOutput = new Vector3[vertexOperationArray.Length];
    }
    private void OnDestroy()
    {
        fieldNodes.Dispose();
        influenceOutput.Dispose();
        heightOutput.Dispose();
        vertexOperationArray.Dispose();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            int centerIndex = fieldSize * (fieldSize / 2) + (fieldSize / 2);
            for (int i = -9; i < 10; i++)
            {
                FieldNode node = fieldNodes[centerIndex + i];
                node.height = 95f;
                fieldNodes[centerIndex + i] = node;
            }
        }

        UpdateEntropyJob entropyJob = new UpdateEntropyJob
        {
            random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(0, int.MaxValue)),
            deltaTime = Time.deltaTime,
            nodes = fieldNodes
        };
        NeighborEnergyJob neighborJob = new NeighborEnergyJob
        {
            deltaTime = Time.deltaTime,
            size = fieldSize,
            neighborFactor = spreadRate,
            nodes = fieldNodes,
            influences = influenceOutput
        };
        ApplyFlowJob applyFlowJob = new ApplyFlowJob
        {
            influence = influenceOutput,
            neighborFactor = spreadRate,
            deltaTime = Time.deltaTime,
            nodes = fieldNodes
        };
        /*
        CalculateHeightJob calculateHeightJob = new CalculateHeightJob
        {
            size = fieldSize,
            magnitude = waveMagnitude,
            nodes = fieldNodes,
            heights = heightOutput
        };
        */
        ApplyHeightJob applyHeightJob = new ApplyHeightJob
        {
            meshOutput = vertexOperationArray,
            nodes = fieldNodes,
            coefficient = heightStep
        };
        // Schedule all jobs in order.
        entropyJobHandle = entropyJob.Schedule(fieldNodes.Length, 64);
        neighborInfluenceHandle = neighborJob.Schedule(fieldNodes.Length, 64, entropyJobHandle);
        applyFlowHandle = applyFlowJob.Schedule(fieldNodes.Length, 64, neighborInfluenceHandle);
        /*
        calculateHeightHandle = calculateHeightJob.Schedule(fieldNodes.Length, 64, applyFlowHandle);
        */
        applyHeightHandle = applyHeightJob.Schedule(fieldNodes.Length, 64, applyFlowHandle);
    }
    private void LateUpdate()
    {
        applyHeightHandle.Complete();

        vertexOperationArray.CopyTo(verticesJobOutput);
        dynamicMesh.vertices = verticesJobOutput;
        dynamicMesh.RecalculateNormals();
    }
    [BurstCompile]
    private struct UpdateEntropyJob : IJobParallelFor
    {
        [ReadOnly] public Unity.Mathematics.Random random;
        [ReadOnly] public float deltaTime;
        public NativeArray<FieldNode> nodes;

        public void Execute(int index)
        {
            FieldNode node = nodes[index];
            nodes[index] = node;
        }
    }
    [BurstCompile]
    private struct NeighborEnergyJob : IJobParallelFor
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public int size;
        [ReadOnly] public float neighborFactor;
        [ReadOnly] public NativeArray<FieldNode> nodes;
        [WriteOnly] public NativeArray<float> influences;
        public void Execute(int index)
        {
            FieldNode node = nodes[index];

            float leftHeight = node.coordinate.x != 0 ?
                nodes[index - 1].height :
                nodes[index - 1 + size].height;
            float rightHeight = node.coordinate.x != size - 1 ?
                nodes[index + 1].height :
                nodes[index + 1 - size].height;
            float downHeight = node.coordinate.y != 0 ?
                nodes[index - size].height :
                nodes[index - size + size * size].height;
            float upHeight = node.coordinate.y != size - 1 ?
                nodes[index + size].height :
                nodes[index + size - size * size].height;

            float heightDifference =
                (leftHeight + rightHeight + downHeight + upHeight) / 4f
                - node.height;

            if (heightDifference > 1f)
                influences[index] = deltaTime * neighborFactor;
            else if (heightDifference < -1f)
                influences[index] = -deltaTime * neighborFactor;
        }
    }
    [BurstCompile]
    private struct ApplyFlowJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> influence;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float neighborFactor;
        public NativeArray<FieldNode> nodes;
        public void Execute(int index)
        {
            // Apply new flow influences to the field.
            FieldNode node = nodes[index];

            node.height += influence[index];

            nodes[index] = node;
        }
    }
    /*
    [BurstCompile]
    private struct CalculateHeightJob : IJobParallelFor
    {
        [ReadOnly] public int size;
        [ReadOnly] public float magnitude;
        [ReadOnly] public NativeArray<FieldNode> nodes;
        [WriteOnly] public NativeArray<float> heights;
        public void Execute(int index)
        {
            FieldNode node = nodes[index];
            float height = 0f;

            if (node.coordinate.x != 0)
                height += nodes[index - 1].flowDirection.x;
            if (node.coordinate.x != size - 1)
                height -= nodes[index + 1].flowDirection.x;
            if (node.coordinate.y != 0)
                height += nodes[index - size].flowDirection.y;
            if (node.coordinate.y != size - 1)
                height -= nodes[index + size].flowDirection.y;

            height *= magnitude;
            heights[index] = height;
        }
    }
    */
    [BurstCompile]
    private struct ApplyHeightJob : IJobParallelFor
    {
        // Apply new flow heights to the field.
        public NativeArray<FieldNode> nodes;
        public NativeArray<Vector3> meshOutput;
        [ReadOnly] public float coefficient;
        public void Execute(int index)
        {
            Vector3 vert = meshOutput[index];
            vert.y = nodes[index].height * coefficient;
            meshOutput[index] = vert;
        }
    }

    /*
    public float GetElevation(Vector2 atLocation)
    {

    }
    */

    public void ApplyImpulse(Vector2 atLocation, float magnitude)
    {

    }
}
