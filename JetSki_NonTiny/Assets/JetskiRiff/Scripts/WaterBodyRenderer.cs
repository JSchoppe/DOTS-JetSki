using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Renders a dynamic body of water in a specific region
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class WaterBodyRenderer : MonoBehaviour
{
    #region Private Fields
    private Mesh dynamicMesh;
    private NativeArray<Vector3> vertexOperationArray;
    private WaveSimulationJob waveSimJob;
    private JobHandle waveJobHandle;
    private Vector3[] waveJobOutput;
    private NativeArray<Vector2> uvOperationArray;
    private UVUpdateJob uvUpdateJob;
    private JobHandle uvJobHandle;
    private Vector2[] uvJobOutput;
    #endregion
    #region Inspector Fields
    [Header("Ambient Wave Parameters")]
    [Tooltip("The rate the waves move naturally.")]
    [SerializeField] private float ambientFlowSpeed = 1f;
    [Tooltip("The distance between peaks and valleys of waves.")]
    [SerializeField] private float ambientFlowSpread = 1f;
    [Tooltip("The max height of natural wave peaks.")]
    [SerializeField] private float ambientFlowHeight = 1f;
    [Header("Mesh Generation Parameters")]
    [Tooltip("How far the mesh extends out.")]
    [SerializeField] private float horizonDistance = 30f;
    [Tooltip("Distance between mesh subdivisions.")]
    [SerializeField] private float detailLevel = 0.25f;
    #endregion
#if DEBUG
    #region Editor Functions
    private void OnValidate()
    {
        // Clamp inpsecotr field values.
        if (horizonDistance < 1f)
            horizonDistance = 1f;
        if (detailLevel > horizonDistance)
            detailLevel = horizonDistance;
        else if (detailLevel < 0.05f)
            detailLevel = 0.05f;
        if (ambientFlowHeight < 0f)
            ambientFlowHeight = 0f;
        if (ambientFlowSpread < 0.001f)
            ambientFlowSpread = 0.001f;
        // Recalculate the preview for the generated geometry.
        int tiles = (int)(horizonDistance / detailLevel);
        gizmoVerts = GenerateMeshVertices(tiles);
        gizmosTris = GenerateMeshTriangles(tiles);
    }
    private Vector3[] gizmoVerts;
    private int[] gizmosTris;
    private void OnDrawGizmos()
    {
        // Draw a preview of the mesh if the preview has generated.
        if (gizmosTris != null && gizmoVerts != null)
        {
            Gizmos.color = Color.blue;
            for (int i = 0; i < gizmosTris.Length; i += 3)
            {
                Vector3 a = transform.TransformPoint(gizmoVerts[gizmosTris[i]]);
                Vector3 b = transform.TransformPoint(gizmoVerts[gizmosTris[i + 1]]);
                Vector3 c = transform.TransformPoint(gizmoVerts[gizmosTris[i + 2]]);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, c);
                Gizmos.DrawLine(a, c);
            }
        }
    }
    #endregion
#endif

    #region Mesh and Job Initialization
    private void Start()
    {
        // Create a new mesh for our waves.
        dynamicMesh = new Mesh();
        dynamicMesh.MarkDynamic();

        // Calculate how many quad tiles will be in our mesh.
        int tiles = (int)(horizonDistance / detailLevel);
        // Generate the mesh. TODO: Change generation to favor nearby details.
        Vector3[] verts = GenerateMeshVertices(tiles);
        int[] tris = GenerateMeshTriangles(tiles);
        Vector3[] norms = GenerateMeshNormals(tiles);
        Vector2[] UVs = GenerateMeshUVs(ref verts);
        // Apply the mesh to the filter in the scene.
        dynamicMesh.vertices = verts;
        dynamicMesh.triangles = tris;
        dynamicMesh.normals = norms;
        dynamicMesh.uv = UVs;
        GetComponent<MeshFilter>().mesh = dynamicMesh;

        // Allocate space for the wave simulation job.
        vertexOperationArray = new NativeArray<Vector3>(dynamicMesh.vertices, Allocator.Persistent);
        waveJobOutput = new Vector3[vertexOperationArray.Length];
        // Allocate space for the uv updates job.
        uvOperationArray = new NativeArray<Vector2>(dynamicMesh.uv, Allocator.Persistent);
        uvJobOutput = new Vector2[uvOperationArray.Length];
    }
    #endregion
    #region Job Update Loop
    private void Update()
    {
        // Create a new wave simulation job.
        waveSimJob = new WaveSimulationJob()
        {
            localToWorld = transform.localToWorldMatrix,
            spreadFactor = ambientFlowSpread,
            vertices = vertexOperationArray,
            time = Time.time * ambientFlowSpeed,
            height = ambientFlowHeight
        };
        // Divide the task among multiple threads.
        waveJobHandle = waveSimJob.Schedule(vertexOperationArray.Length, 64);

        // Create a new uv update job.
        uvUpdateJob = new UVUpdateJob()
        {
            localToWorld = transform.localToWorldMatrix,
            vertices = vertexOperationArray,
            uvs = uvOperationArray
        };
        // Schedule to update the uvs after the vertices have been recalculated.
        // TODO: could this be done at the same time? Since this only reads the vertices.
        uvJobHandle = uvUpdateJob.Schedule(uvOperationArray.Length, 64, waveJobHandle);
    }
    private void LateUpdate()
    {
        // Wait for all jobs to complete.
        waveJobHandle.Complete();
        uvJobHandle.Complete();
        // Apply the new data to the mesh.
        waveSimJob.vertices.CopyTo(waveJobOutput);
        dynamicMesh.vertices = waveJobOutput;
        uvUpdateJob.uvs.CopyTo(uvJobOutput);
        dynamicMesh.uv = uvJobOutput;
        // Update the lightning normals. TODO: See if this can be done manually as a job.
        dynamicMesh.RecalculateNormals();
    }
    private void OnDestroy()
    {
        // Manually dispose of unmanaged memory.
        vertexOperationArray.Dispose();
        uvOperationArray.Dispose();
    }
    #endregion
    #region Wave Simulation Job
    [BurstCompile]
    private struct WaveSimulationJob : IJobParallelFor
    {
        public NativeArray<Vector3> vertices;
        public Matrix4x4 localToWorld;
        public float time;
        public float spreadFactor;
        public float height;
        public void Execute(int i)
        {
            Vector3 vertex = vertices[i];
            // Transform from mesh space to world space.
            Vector3 global = localToWorld.MultiplyPoint3x4(vertex);
            // Apply perlin noise relative to the spread factor.
            vertex.y = height * noise.snoise(new float2(global.x * spreadFactor + time, global.z * spreadFactor + time));
            vertices[i] = vertex;
        }
    }
    #endregion
    #region Update UVs Job
    [BurstCompile]
    private struct UVUpdateJob : IJobParallelFor
    {
        public NativeArray<Vector3> vertices;
        public NativeArray<Vector2> uvs;
        public Matrix4x4 localToWorld;
        public void Execute(int i)
        {
            Vector3 vertex = vertices[i];
            // Transform from mesh space to world space.
            Vector3 global = localToWorld.MultiplyPoint3x4(vertex);
            // Update UVs to correspond to world position.
            Vector2 uv = uvs[i];
            uv.x = global.x;
            uv.y = global.z;
            uvs[i] = uv;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Gets the water height at a given location in world space
    /// </summary>
    /// <param name="location">The location where x and y correspond to the x and z coordinates</param>
    /// <returns>The water elevation at the given location</returns>
    public float GetElevation(Vector2 location)
    {
        // Important that this matches how it is done by the job.
        float time = Time.time * ambientFlowSpeed;
        Vector3 global = transform.localToWorldMatrix.MultiplyPoint(location);
        return noise.snoise(new float2
        {
            x = global.x * ambientFlowSpread + time,
            y = global.z * ambientFlowSpread + time
        });
    }
    #endregion
    #region Mesh Generation Functions TODO: Abstract this
    private Vector3[] GenerateMeshVertices(int tiles)
    {
        // Allocate space for vertices.
        Vector3[] verts = new Vector3[(tiles + 1) * (tiles + 1)];
        // Set the origin to center the back edge of the rect at the origin.
        Vector3 offset = new Vector3
        {
            x = -(horizonDistance / 2f),
            z = 0f
        };
        // Generate the grid of vertices with detailLevel as the step.
        for (int x = 0; x <= tiles; x++)
            for (int z = 0; z <= tiles; z++)
                verts[x * (tiles + 1) + z] = offset +
                    new Vector3(x * detailLevel, 0f, z * detailLevel);
        return verts;
    }
    private int[] GenerateMeshTriangles(int tiles)
    {
        // Allocate space for triangle indices.
        int[] triangles = new int[tiles * tiles * 2 * 3];
        int i = 0;
        // At each tile fill the two triangles.
        for (int x = 0; x < tiles; x++)
        {
            for (int z = 0; z < tiles; z++)
            {
                // Calculate the indices of the four corners of the quad.
                int a = x * (tiles + 1) + z;
                int b = a + 1;
                int c = (x + 1) * (tiles + 1) + z;
                int d = c + 1;
                // Generate the first triangle.
                triangles[i] = a; i++;
                triangles[i] = b; i++;
                triangles[i] = c; i++;
                // Generate the complementary triangle.
                triangles[i] = d; i++;
                triangles[i] = c; i++;
                triangles[i] = b; i++;
            }
        }
        return triangles;
    }
    private Vector3[] GenerateMeshNormals(int tiles)
    {
        // Allocate space for normals.
        Vector3[] normals = new Vector3[(tiles + 1) * (tiles + 1)];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.up;
        return normals;
    }
    private Vector2[] GenerateMeshUVs(ref Vector3[] verts)
    {
        // Allocate space for UVs.
        Vector2[] UVs = new Vector2[verts.Length];
        // Align UV's to the vertex grid.
        for (int i = 0; i < verts.Length; i++)
            UVs[i] = new Vector2(verts[i].x, verts[i].z);
        return UVs;
    }
    #endregion
}
