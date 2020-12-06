using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using System.Collections.Generic;

/// <summary>
/// Renders a dynamic body of water in a specific region.
/// </summary>
public sealed class WaterBodyRenderer : NoisyPlane
{
    #region Private Fields
    private WaveSimulationJob waveSimJob;
    private WaveComponent waveComponent;
    private List<WaveForce> forces;
    private NativeArray<WaveForce> forcesArray;
    #endregion
    #region Inspector Fields
    [Header("Ambient Wave Parameters")]
    [Tooltip("The rate the waves move naturally.")]
    [SerializeField] private float ambientFlowSpeed = 1f;
    [Tooltip("The distance between peaks and valleys of waves.")]
    [SerializeField] private float ambientFlowSpread = 1f;
    [Tooltip("The max height of natural wave peaks.")]
    [SerializeField] private float ambientFlowHeight = 1f;
    #endregion
    #region Properties
    /// <summary>
    /// An ECS component that encapsulates this
    /// fluid body's ambient wave parameters.
    /// </summary>
    public WaveComponent WaveComponent
    {
        // This accessor is slow, and is not meant
        // to be constantly accessed. It is safe from
        // script order errors.
        get
        {
            return new WaveComponent
            {
                ambientFlowHeight = ambientFlowHeight,
                ambientFlowSpeed = ambientFlowSpeed,
                ambientFlowSpread = ambientFlowSpread
            };
        }
    }
    #endregion
    #region Editor Functions
    public override void OnValidate()
    {
        base.OnValidate();
        // Clamp inspector field values.
        if (ambientFlowHeight < 0f)
            ambientFlowHeight = 0f;
        if (ambientFlowSpread < 0.001f)
            ambientFlowSpread = 0.001f;
        // Apply water elevation to preview.
        for (int i = 0; i < gizmoVerts.Length; i++)
        {
            Vector3 globalCoord = transform.localToWorldMatrix.MultiplyPoint(gizmoVerts[i]);
            gizmoVerts[i].y = GetElevation(new Vector2(globalCoord.x, globalCoord.z));
        }
    }
    #endregion
    #region Initialization
    protected override void Start()
    {
        base.Start();
        waveComponent = WaveComponent;
        forces = new List<WaveForce>();
    }
    #endregion
    #region Job Cycle
    private void Update()
    {
        // TODO Make this better.
        List<WaveForce> expiredWaves = new List<WaveForce>();
        foreach (WaveForce force in forces)
            if (force.baseMagnitude - ((Time.time - force.startTime) * force.fallOff) < 0f)
                expiredWaves.Add(force);
        for (int i = 0; i < expiredWaves.Count; i++)
            forces.Remove(expiredWaves[i]);
        forcesArray = new NativeArray<WaveForce>(forces.ToArray(), Allocator.TempJob);

        // Create a new wave simulation job.
        waveSimJob = new WaveSimulationJob()
        {
            localToWorld = transform.localToWorldMatrix,
            vertices = vertexOperationArray,
            scaledTime = Time.time * ambientFlowSpeed,
            time = Time.time,
            wave = waveComponent,
            forces = forcesArray
        };
        // Divide the task among multiple threads.
        verticesJobHandle = waveSimJob.Schedule(vertexOperationArray.Length, 64);

        // Create a new uv update job.
        uvUpdateJob = new UVUpdateJob()
        {
            localToWorld = transform.localToWorldMatrix,
            vertices = vertexOperationArray,
            uvs = uvOperationArray
        };
        // Schedule to update the uvs after the vertices have been recalculated.
        // TODO: could this be done at the same time? Since this only reads the vertices.
        uvJobHandle = uvUpdateJob.Schedule(uvOperationArray.Length, 64, verticesJobHandle);
    }
    private void LateUpdate()
    {
        // Wait for all jobs to complete.
        verticesJobHandle.Complete();
        uvJobHandle.Complete();
        // Apply the new data to the mesh.
        waveSimJob.vertices.CopyTo(verticesJobOutput);
        dynamicMesh.vertices = verticesJobOutput;
        uvUpdateJob.uvs.CopyTo(uvJobOutput);
        dynamicMesh.uv = uvJobOutput;
        // Update the lightning normals. TODO: See if this can be done manually as a job.
        dynamicMesh.RecalculateNormals();

        forcesArray.Dispose();
    }
    #endregion
    #region Wave Simulation Job
    [BurstCompile]
    private struct WaveSimulationJob : IJobParallelFor
    {
        public NativeArray<Vector3> vertices;
        [ReadOnly] public Matrix4x4 localToWorld;
        [ReadOnly] public WaveComponent wave;
        [ReadOnly] public float scaledTime;
        [ReadOnly] public float time;
        [ReadOnly] public NativeArray<WaveForce> forces;

        public void Execute(int i)
        {
            Vector3 vertex = vertices[i];
            // Transform from mesh space to world space.
            Vector3 global = localToWorld.MultiplyPoint(vertex);
            // Apply perlin noise relative to the spread factor.
            vertex.y = wave.WaveHeightAt(new float2(global.x, global.z), scaledTime);

            // Apply elevations from waves.
            for (int j = 0; j < forces.Length; j++)
            {
                float waveDistance = time - forces[j].startTime;
                float waveHeight = forces[j].baseMagnitude - (waveDistance * forces[j].fallOff);
                float waveWidth = waveDistance / 2f;

                float distance = Vector2.Distance(new Vector2(global.x, global.z), forces[j].origin);
                float localDist = Mathf.Abs(distance - waveDistance) / (0.5f * waveWidth);
                if (localDist < Mathf.PI)
                {
                    vertex.y += 0.5f * waveHeight * (Mathf.Sin(localDist + Mathf.PI * 0.5f) + 1f);
                }
            }

            vertices[i] = vertex;
        }
    }
    #endregion
    #region NoisyPlane Implementation
    /// <summary>
    /// Gets the water height at a given location in world space
    /// </summary>
    /// <param name="location">The location where x and y correspond to the x and z coordinates</param>
    /// <returns>The water elevation at the given location</returns>
    public override float GetElevation(Vector2 location)
    {
        // Important that this matches how it is done by the job.
        float time = Time.time * ambientFlowSpeed;
        return waveComponent.WaveHeightAt(location, time);
    }
    #endregion

    public void AddWave(WaveForce force)
    {
        forces.Add(force);
    }
}
