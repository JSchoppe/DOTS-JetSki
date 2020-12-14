using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

/// <summary>
/// Simulated fluid surface that uses a flow field approach.
/// </summary>
public sealed class FlowFieldFluid : MonoBehaviour
{
    #region Flow Field Node Unit
    private struct FieldNode
    {
        public float2 location;
        public float height;
        // This is stored so it does not need to recalculate
        // every update cycle.
        public int2 coordinate;
    }
    #endregion
    #region Index Wrap Struct (Embedded Implementation)
    /// <summary>
    /// Provides an implementation
    /// for wrapping an index into the range
    /// of a two-dimensional array.
    /// </summary>
    private struct IndexWrap2D
    {
        public readonly int lengthX;
        public readonly int lengthY;
        public IndexWrap2D(int length)
            : this(length, length) { }
        public IndexWrap2D(int lengthX, int lengthY)
        {
            this.lengthX = lengthX;
            this.lengthY = lengthY;
        }
        // Returns the index based on two dimensions given
        // where external indices will be wrapped into
        // internal indices.
        public int this[int x, int y]
        {
            get
            {
                while (x < 0)
                    x += lengthX;
                while (x > lengthX - 1)
                    x -= lengthX;
                while (y < 0)
                    y += lengthY;
                while (y > lengthY - 1)
                    y -= lengthY;
                return y * lengthX + x;
            }
        }
    }
    #endregion

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

    private float2 startingCorner;
    private IndexWrap2D wrap;

    private NativeArray<FieldNode> fieldNodes;
    private NativeArray<float> influenceOutput;
    private NativeArray<float> heightOutput;

    private NativeArray<Vector3> vertexOperationArray;
    private Vector3[] verticesJobOutput;

    private JobHandle neighborInfluenceHandle;
    private JobHandle applyFlowHandle;
    private JobHandle applyHeightHandle;

    public Vector2 Center
    {
        set
        {
            xStep = (int)(value.x / fieldStep);
            yStep = (int)(value.y / fieldStep);
            transform.position = new Vector3
            {
                x = xStep * fieldStep,
                y = transform.position.y,
                z = yStep * fieldStep
            };
        }
    }
    private int xStep;
    private int yStep;

    private Mesh dynamicMesh;

    private void Awake()
    {
        Center = Vector2.zero;

        float[] startingHeights = new float[fieldSize * fieldSize];
        for (int i = 0; i < startingHeights.Length - 1; i += 2)
        {
            startingHeights[i] = UnityEngine.Random.value * noiseIntensity;
            startingHeights[i + 1] = -startingHeights[i];
        }
        startingHeights.Shuffle();
        wrap = new IndexWrap2D(fieldSize);

        // Generate the flow field nodes.
        // Get the starting corner to iterate from.
        startingCorner = new float2
        {
            x = -(fieldSize - 1) * fieldStep * 0.5f,
            y = -(fieldSize - 1) * fieldStep * 0.5f
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
        // TEST creates an impulse in the fluid body.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            int2 center = new int2(fieldSize / 2, fieldSize / 2);
            for (int x = -5; x <= 5; x++)
            {
                for (int y = -5; y <= 5; y++)
                {
                    int index =
                        wrap[center.x + x, center.y + y];
                    FieldNode node = fieldNodes[index];
                    node.height -= 10f * Time.deltaTime;
                    fieldNodes[index] = node;
                }
            }
        }

        NeighborEnergyJob neighborJob = new NeighborEnergyJob
        {
            deltaTime = Time.deltaTime,
            size = fieldSize,
            neighborFactor = spreadRate,
            nodes = fieldNodes,
            influences = influenceOutput,
            wrap = wrap
        };
        ApplyFlowJob applyFlowJob = new ApplyFlowJob
        {
            influence = influenceOutput,
            neighborFactor = spreadRate,
            deltaTime = Time.deltaTime,
            nodes = fieldNodes
        };
        ApplyHeightJob applyHeightJob = new ApplyHeightJob
        {
            meshOutput = vertexOperationArray,
            nodes = fieldNodes,
            coefficient = heightStep,
            stepX = xStep,
            stepY = yStep,
            wrap = wrap
        };
        // Schedule all jobs in order.
        neighborInfluenceHandle = neighborJob.Schedule(fieldNodes.Length, 64);
        applyFlowHandle = applyFlowJob.Schedule(fieldNodes.Length, 64, neighborInfluenceHandle);
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
    private struct NeighborEnergyJob : IJobParallelFor
    {
        [ReadOnly] public float deltaTime;
        [ReadOnly] public int size;
        [ReadOnly] public float neighborFactor;
        [ReadOnly] public NativeArray<FieldNode> nodes;
        [ReadOnly] public IndexWrap2D wrap;
        [WriteOnly] public NativeArray<float> influences;
        public void Execute(int index)
        {
            FieldNode node = nodes[index];

            float leftHeight =
                nodes[wrap[node.coordinate.x - 1, node.coordinate.y]].height;
            float rightHeight =
                nodes[wrap[node.coordinate.x + 1, node.coordinate.y]].height;
            float downHeight =
                nodes[wrap[node.coordinate.x, node.coordinate.y - 1]].height;
            float upHeight =
                nodes[wrap[node.coordinate.x, node.coordinate.y + 1]].height;

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
    [BurstCompile]
    private struct ApplyHeightJob : IJobParallelFor
    {
        // Apply new flow heights to the field.
        [ReadOnly] public NativeArray<FieldNode> nodes;
        public NativeArray<Vector3> meshOutput;
        [ReadOnly] public float coefficient;
        [ReadOnly] public int stepX;
        [ReadOnly] public int stepY;
        [ReadOnly] public IndexWrap2D wrap;
        public void Execute(int index)
        {
            Vector3 vert = meshOutput[index];
            int wrappedIndex = wrap[nodes[index].coordinate.x + stepY, nodes[index].coordinate.y + stepX];
            vert.y = nodes[wrappedIndex].height * coefficient;
            meshOutput[index] = vert;
        }
    }

    public float GetElevation(Vector2 atLocation)
    {
        Vector2 indexLocation =
            (atLocation + (Vector2)startingCorner) / fieldStep;

        int xLeftIndex = Mathf.FloorToInt(indexLocation.x);
        int xRightIndex = Mathf.CeilToInt(indexLocation.x);
        int yDownIndex = Mathf.FloorToInt(indexLocation.y);
        int yUpIndex = Mathf.CeilToInt(indexLocation.y);

        float leftWeight = Mathf.Max(indexLocation.x - xLeftIndex, 0f);
        float rightWeight = Mathf.Max(xRightIndex - indexLocation.x, 0f);
        float downWeight = Mathf.Max(indexLocation.y - yDownIndex, 0f);
        float upWeight = Mathf.Max(yUpIndex - indexLocation.y, 0f);
        float totalWeight = leftWeight + rightWeight + downWeight + upWeight;
        leftWeight /= totalWeight;
        rightWeight /= totalWeight;
        downWeight /= totalWeight;
        upWeight /= totalWeight;

        return heightStep * 0.5f * (
            (leftWeight + downWeight) * fieldNodes[wrap[xLeftIndex, yDownIndex]].height +
            (leftWeight + upWeight) * fieldNodes[wrap[xLeftIndex, yUpIndex]].height +
            (rightWeight + upWeight) * fieldNodes[wrap[xRightIndex, yUpIndex]].height +
            (rightWeight + downWeight) * fieldNodes[wrap[xRightIndex, yDownIndex]].height
        );
    }

    public void ApplyImpulse(Vector2 atLocation, float magnitude)
    {
        // Wrap the location into the bounds of the flow field.
        while (atLocation.x > fieldSize * fieldStep * 0.5f)
            atLocation.x += fieldSize * fieldStep;
        while (atLocation.x < -fieldSize * fieldStep * 0.5f)
            atLocation.x -= fieldSize * fieldStep;
        while (atLocation.y > fieldSize * fieldStep * 0.5f)
            atLocation.y += fieldSize * fieldStep;
        while (atLocation.y < -fieldSize * fieldStep * 0.5f)
            atLocation.y -= fieldSize * fieldStep;


    }
}
