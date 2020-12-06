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
        public float2 flowDirection;
        public float height;
        public float entropyAngle;
        public float entropyMagnitude;
    }

    [SerializeField] private float waveMagnitude = 1f;
    [SerializeField] private float deltaEntropyAngle = 0.3f;
    [SerializeField] private float deltaEntropyMagnitude = 0.1f;
    [SerializeField] private float maxEntropyMagnitude = 1f;
    [SerializeField] private float deltaFlowDecay = 0.1f;
    [SerializeField] private int fieldSize = 2;
    [SerializeField] private float fieldStep = 1f;
    [SerializeField] private float neighborInfluence = 1f;

    private void OnValidate()
    {
        if (waveMagnitude < 0f)
            waveMagnitude = 0f;
        if (deltaEntropyAngle < 0f)
            deltaEntropyAngle = 0f;
        if (deltaEntropyMagnitude < 0f)
            deltaEntropyMagnitude = 0f;
        if (maxEntropyMagnitude < 0f)
            maxEntropyMagnitude = 0f;
        if (deltaFlowDecay < 0f)
            deltaFlowDecay = 0f;
        if (fieldSize < 2)
            fieldSize = 2;
        if (fieldStep < 0.005f)
            fieldStep = 0.005f;
        if (neighborInfluence < 0f)
            neighborInfluence = 0f;
    }

    // private Vector2 currentOrigin;
    private NativeArray<FieldNode> fieldNodes;
    private NativeArray<float2> influenceOutput;
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
                    height = 0f,
                    flowDirection = float2.zero
                };
        // Make the nodes accessible to jobs.
        fieldNodes = new NativeArray<FieldNode>(nodes, Allocator.Persistent);
        // TODO test whether it is faster to have these persistent
        // or declared every loop for the temp job.
        influenceOutput = new NativeArray<float2>(nodes.Length, Allocator.Persistent);
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
            FieldNode node = fieldNodes[0];
            node.flowDirection = new float2(2, 2);
            fieldNodes[0] = node;
        }

        UpdateEntropyJob entropyJob = new UpdateEntropyJob
        {
            random = new Unity.Mathematics.Random((uint)UnityEngine.Random.Range(0, int.MaxValue)),
            deltaTime = Time.deltaTime,
            nodes = fieldNodes,
            deltaEntropyAngle = deltaEntropyAngle,
            deltaEntropyMagnitude = deltaEntropyMagnitude,
            maxEntropyMagnitude = maxEntropyMagnitude
        };
        NeighborInfluenceJob neighborJob = new NeighborInfluenceJob
        {
            deltaTime = Time.deltaTime,
            size = fieldSize,
            neighborFactor = neighborInfluence,
            nodes = fieldNodes,
            influences = influenceOutput
        };
        ApplyFlowJob applyFlowJob = new ApplyFlowJob
        {
            influence = influenceOutput,
            decay = deltaFlowDecay,
            deltaTime = Time.deltaTime,
            nodes = fieldNodes
        };
        CalculateHeightJob calculateHeightJob = new CalculateHeightJob
        {
            size = fieldSize,
            magnitude = waveMagnitude,
            nodes = fieldNodes,
            heights = heightOutput
        };
        ApplyHeightJob applyHeightJob = new ApplyHeightJob
        {
            meshOutput = vertexOperationArray,
            heights = heightOutput,
            nodes = fieldNodes
        };
        // Schedule all jobs in order.
        entropyJobHandle = entropyJob.Schedule(fieldNodes.Length, 64);
        neighborInfluenceHandle = neighborJob.Schedule(fieldNodes.Length, 64, entropyJobHandle);
        applyFlowHandle = applyFlowJob.Schedule(fieldNodes.Length, 64, neighborInfluenceHandle);
        calculateHeightHandle = calculateHeightJob.Schedule(fieldNodes.Length, 64, applyFlowHandle);
        applyHeightHandle = applyHeightJob.Schedule(fieldNodes.Length, 64, calculateHeightHandle);
    }
    private void LateUpdate()
    {
        neighborInfluenceHandle.Complete();
        applyFlowHandle.Complete();
        calculateHeightHandle.Complete();
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
        [ReadOnly] public float deltaEntropyAngle;
        [ReadOnly] public float deltaEntropyMagnitude;
        [ReadOnly] public float maxEntropyMagnitude;
        public NativeArray<FieldNode> nodes;

        public void Execute(int index)
        {
            FieldNode node = nodes[index];
            node.entropyAngle += random.NextFloat(-1f, 1f) * deltaEntropyAngle * deltaTime;
            node.entropyMagnitude += random.NextFloat(-1f, 1f) * deltaEntropyMagnitude * deltaTime;
            node.entropyMagnitude =
                math.clamp(node.entropyMagnitude, -maxEntropyMagnitude, maxEntropyMagnitude);
            nodes[index] = node;
        }
    }
    [BurstCompile]
    private struct NeighborInfluenceJob : IJobParallelFor
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public int size;
        [ReadOnly] public float neighborFactor;
        [ReadOnly] public NativeArray<FieldNode> nodes;
        [WriteOnly] public NativeArray<float2> influences;
        public void Execute(int index)
        {
            FieldNode node = nodes[index];
            float2 influence = float2.zero;
            if (node.coordinate.x != 0)
                influence += nodes[index - 1].flowDirection;
            if (node.coordinate.x != size - 1)
                influence += nodes[index + 1].flowDirection;
            if (node.coordinate.y != 0)
                influence += nodes[index - size].flowDirection;
            if (node.coordinate.y != size - 1)
                influence += nodes[index + size].flowDirection;
            influence *= deltaTime * neighborFactor;

            influence += deltaTime * new float2
            {
                x = math.cos(node.entropyAngle) * node.entropyMagnitude,
                y = math.sin(node.entropyAngle) * node.entropyMagnitude
            };

            influences[index] = influence;
        }
    }
    [BurstCompile]
    private struct ApplyFlowJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float2> influence;
        [ReadOnly] public float decay;
        [ReadOnly] public float deltaTime;
        public NativeArray<FieldNode> nodes;
        public void Execute(int index)
        {
            // Apply new flow influences to the field.
            FieldNode node = nodes[index];
            node.flowDirection += influence[index];
            float magnitude = math.sqrt(node.flowDirection.x * node.flowDirection.x +
                node.flowDirection.y * node.flowDirection.y);

            if (magnitude > 0f)
                node.flowDirection *= Mathf.Max(0f, (magnitude - deltaTime * decay) / magnitude);
            nodes[index] = node;
        }
    }
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
    [BurstCompile]
    private struct ApplyHeightJob : IJobParallelFor
    {
        // Apply new flow heights to the field.
        [ReadOnly] public NativeArray<float> heights;
        public NativeArray<FieldNode> nodes;
        public NativeArray<Vector3> meshOutput;
        public void Execute(int index)
        {
            FieldNode node = nodes[index];
            node.height = heights[index];
            nodes[index] = node;

            Vector3 vert = meshOutput[index];
            vert.y = heights[index];
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
