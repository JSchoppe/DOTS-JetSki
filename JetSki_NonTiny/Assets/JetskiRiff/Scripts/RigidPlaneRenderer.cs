using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Renders perlin noise relative to location with no other influences.
/// </summary>
public sealed class RigidPlaneRenderer : NoisyPlane
{
    #region Private Fields
    private NoiseMeshJob perlinMeshJob;
    #endregion
    #region Jobs Cycle
    private void Update()
    {
        // Create a new noise simulation job.
        perlinMeshJob = new NoiseMeshJob()
        {
            localToWorld = transform.localToWorldMatrix,
            vertices = vertexOperationArray,
            octaves = octaveOperationArray
        };
        // Divide the task among multiple threads.
        verticesJobHandle = perlinMeshJob.Schedule(vertexOperationArray.Length, 64);

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
        perlinMeshJob.vertices.CopyTo(verticesJobOutput);
        dynamicMesh.vertices = verticesJobOutput;
        uvUpdateJob.uvs.CopyTo(uvJobOutput);
        dynamicMesh.uv = uvJobOutput;
        // Update the lightning normals. TODO: See if this can be done manually as a job.
        dynamicMesh.RecalculateNormals();
    }
    #endregion
    #region Jobs Implementation
    [BurstCompile]
    private struct NoiseMeshJob : IJobParallelFor
    {
        public NativeArray<Vector3> vertices;
        public Matrix4x4 localToWorld;
        [ReadOnly] public NativeArray<Vector2> octaves;

        public void Execute(int i)
        {
            Vector3 vertex = vertices[i];
            // Transform from mesh space to world space.
            Vector3 global = localToWorld.MultiplyPoint3x4(vertex);
            // Apply perlin noise relative to the spread factor.
            vertex.y = 0f;
            for (int j = 0; j < octaves.Length; j++)
            {
                vertex.y += octaves[j].y * noise.snoise(new float2
                {
                    x = global.x * octaves[j].x,
                    y = global.z * octaves[j].x
                });
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
        // It is important that this matches the jobs version.
        // Make sure that if one is changed the other is also changed.
        float y = 0f;
        foreach (Vector2 octave in octaves)
        {
            y += octave.y * noise.snoise(new float2
            {
                x = location.x * octave.x,
                y = location.y * octave.x
            });
        }
        return y;
    }
    #endregion
}
