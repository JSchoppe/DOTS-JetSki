using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

/// <summary>
/// Abstract class for planes that incorporate noise.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public abstract class NoisyPlane : MonoBehaviour, IValidateOnTransformChange
{
    #region Private Fields
    protected Mesh dynamicMesh;
    protected NativeArray<Vector2> octaveOperationArray;
    protected NativeArray<Vector3> vertexOperationArray;
    protected JobHandle verticesJobHandle;
    protected Vector3[] verticesJobOutput;
    protected NativeArray<Vector2> uvOperationArray;
    protected UVUpdateJob uvUpdateJob;
    protected JobHandle uvJobHandle;
    protected Vector2[] uvJobOutput;
    #endregion
    #region Inspector Fields
    [Header("Ambient Generation Parameters")]
    [Tooltip("The x component describes the frequency, the y component describes the amplitude.")]
    [SerializeField] protected Vector2[] octaves = null;
    [Header("Mesh Generation Parameters")]
    [Tooltip("How far the mesh extends out.")]
    [SerializeField] private float horizonDistance = 30f;
    [Tooltip("Distance between mesh subdivisions.")]
    [SerializeField] private float detailLevel = 0.25f;
    [Tooltip("How much the UVs are scaled by when generated.")]
    [SerializeField] private float uvMapScale = 1f;
    #endregion
#if DEBUG
    #region Debug Inspector Fields
    [Header("Debug")]
    [Tooltip("The color used to render the gizmo wireframe preview.")]
    [SerializeField] private Color wireframeColor = Color.white;
    #endregion
    #region Editor Functions
    public virtual void OnValidate()
    {
        // Clamp inspector field values.
        if (horizonDistance < 1f)
            horizonDistance = 1f;
        if (detailLevel > horizonDistance)
            detailLevel = horizonDistance;
        else if (detailLevel < 0.05f)
            detailLevel = 0.05f;
        // Update the gizmo geometry.
        Mesh mesh = MeshHelper.GenerateQuadGrid(detailLevel, new PlaneDefinition
        {
            origin = Vector3.left * (horizonDistance / 2f),
            axisA = Vector3.right,
            axisB = Vector3.forward,
            lengthA = horizonDistance,
            lengthB = horizonDistance
        }, 1f, true);
        gizmosTris = mesh.triangles;
        gizmoVerts = mesh.vertices;
        for (int i = 0; i < gizmoVerts.Length; i++)
        {
            Vector3 globalCoord = transform.localToWorldMatrix.MultiplyPoint3x4(gizmoVerts[i]);
            gizmoVerts[i].y = GetElevation(new Vector2(globalCoord.x, globalCoord.z));
        }
    }
    protected Vector3[] gizmoVerts;
    protected int[] gizmosTris;
    private void OnDrawGizmosSelected()
    {
        // Draw a preview of the mesh if the preview has generated.
        if (gizmoVerts != null && gizmosTris != null)
        {
            Gizmos.color = wireframeColor;
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
    #region Mesh and Jobs Initialization
    protected virtual void Start()
    {
        // Create a new plane for our perlin noise to populate.
        dynamicMesh = MeshHelper.GenerateQuadGrid(detailLevel, new PlaneDefinition
        {
            origin = Vector3.left * (horizonDistance / 2f),
            axisA = Vector3.right,
            axisB = Vector3.forward,
            lengthA = horizonDistance,
            lengthB = horizonDistance
        }, uvMapScale);
        dynamicMesh.MarkDynamic();
        GetComponent<MeshFilter>().mesh = dynamicMesh;

        // Move octaves into fast memory.
        octaveOperationArray = new NativeArray<Vector2>(octaves, Allocator.Persistent);
        // Allocate space for the job that will act on the vertices.
        vertexOperationArray = new NativeArray<Vector3>(dynamicMesh.vertices, Allocator.Persistent);
        verticesJobOutput = new Vector3[vertexOperationArray.Length];
        // Allocate space for the uv updates job.
        uvOperationArray = new NativeArray<Vector2>(dynamicMesh.uv, Allocator.Persistent);
        uvJobOutput = new Vector2[uvOperationArray.Length];
    }
    #endregion
    #region Memory Disposal
    /// <summary>
    /// Override this if there is additional memory to dispose of.
    /// </summary>
    protected virtual void OnDestroy()
    {
        // Manually dispose of unmanaged memory.
        vertexOperationArray.Dispose();
        uvOperationArray.Dispose();
        octaveOperationArray.Dispose();
    }
    #endregion
    #region Update UVs Job
    [BurstCompile]
    protected struct UVUpdateJob : IJobParallelFor
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
    #region Subclass Requirements
    /// <summary>
    /// Gets the water height at a given location in world space
    /// </summary>
    /// <param name="location">The location where x and y correspond to the x and z coordinates</param>
    /// <returns>The water elevation at the given location</returns>
    public abstract float GetElevation(Vector2 location);
    #endregion
}
