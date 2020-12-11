﻿using UnityEngine;
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

    private IndexWrap2D wrap;

    private NativeArray<FieldNode> fieldNodes;
    private NativeArray<float> influenceOutput;
    private NativeArray<float> heightOutput;

    private NativeArray<Vector3> vertexOperationArray;
    private Vector3[] verticesJobOutput;

    private JobHandle neighborInfluenceHandle;
    private JobHandle applyFlowHandle;
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
        wrap = new IndexWrap2D(fieldSize);

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
            coefficient = heightStep
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
