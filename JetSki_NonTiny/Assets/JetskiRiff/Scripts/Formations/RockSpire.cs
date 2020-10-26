using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// A procedurally generated rock spire rendered as an entity.
/// </summary>
public sealed class RockSpire : MonoBehaviour, IValidateOnTransformChange
{
    #region Inpsector Fields
    [Tooltip("The material that will be used to render this rock spire.")]
    [SerializeField] private Material renderMaterial = null;
    [Header("Generation Parameters")]
    [Tooltip("How many vertices exist in each ring loop.")]
    [SerializeField] private int ringSubdivisions = 4;
    [Tooltip("How many rings exist on this spire.")]
    [SerializeField] private int heightSubdivisions = 3;
    [Tooltip("The base floor radius to deviate from.")]
    [SerializeField] private float baseWidth = 7f;
    [Tooltip("Range of values this spire can generate with.")]
    [SerializeField] private Vector2 heightRange = new Vector2(1f, 6f);
    [Tooltip("Range of coefficients this spire uses to scale cross sections.")]
    [SerializeField] private Vector2 dilationFactorRange = new Vector2(0.9f, 1.1f);
    #endregion
#if DEBUG
    #region EditorFunctions
    public void OnValidate()
    {
        // Ensure valid geometry can generate.
        // Important for preventing Unity crashes.
        if (ringSubdivisions < 4)
            ringSubdivisions = 4;
        if (heightSubdivisions < 2)
            heightSubdivisions = 2;
        if (baseWidth < 0.005f)
            baseWidth = 0.005f;
        // Enforce logical min-max range order.
        if (heightRange.y < heightRange.x)
            heightRange.y = heightRange.x;
        if (dilationFactorRange.y < dilationFactorRange.x)
            dilationFactorRange.y = dilationFactorRange.x;
        // Regenerate the preview geometry.
        gizmoVerts = GenerateMeshVertices();
        gizmosTris = GenerateMeshTriangles();
    }
    private Vector3[] gizmoVerts;
    private int[] gizmosTris;
    private void OnDrawGizmos()
    {
        // Draw a preview of the mesh if the preview has generated.
        if (gizmosTris != null && gizmoVerts != null)
        {
            Gizmos.color = Color.yellow;
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

    #region Entity Instantiation
    private void Start()
    {
        // Generate the mesh for this spire.
        Mesh spireMesh = new Mesh();
        spireMesh.vertices = GenerateMeshVertices();
        spireMesh.triangles = GenerateMeshTriangles();
        spireMesh.uv = GenerateMeshUVs();
        spireMesh.RecalculateNormals();

        EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        // TODO probably should abstract this archetype,
        // will end up using it a lot.
        EntityArchetype renderedArchetype = manager.CreateArchetype(
            typeof(LocalToWorld), typeof(RenderMesh),
            typeof(Translation), typeof(RenderBounds));
        // Create the entity and apply its component values.
        Entity newEntity = manager.CreateEntity(renderedArchetype);
        manager.SetSharedComponentData(newEntity, new RenderMesh
        {
            mesh = spireMesh,
            castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true,
            material = renderMaterial
        });
        manager.SetComponentData(newEntity, new Translation
        {
            Value = new float3(transform.position.x, transform.position.y, transform.position.z)
        });
    }
    #endregion
    #region Mesh Generation Functions TODO: Abstract this
    private Vector3[] GenerateMeshVertices()
    {
        // Seed the random based on the location of this spire.
        UnityEngine.Random.InitState((int)transform.position.x +
            (int)transform.position.z * (int)transform.position.z);

        // Get a random height for this spire.
        float totalHeight = Mathf.Lerp(heightRange.x, heightRange.y, UnityEngine.Random.value);
        // Initialize spire dilation values to base width.
        float[] previousDilations = new float[ringSubdivisions];
        for (int i = 0; i < ringSubdivisions; i++)
            previousDilations[i] = baseWidth;

        // Allocate space for vertices.
        Vector3[] verts = new Vector3[ringSubdivisions * heightSubdivisions];
        // Generate the rock spire layer by layer.
        for (int y = 0; y < heightSubdivisions; y++)
        {
            float currentHeight = totalHeight * ((float)y / (heightSubdivisions - 1));
            for (int a = 0; a < ringSubdivisions; a++)
            {
                Vector2 direction = new Vector2
                {
                    x = Mathf.Cos((float)a / ringSubdivisions * Mathf.PI * 2),
                    y = Mathf.Sin((float)a / ringSubdivisions * Mathf.PI * 2)
                };
                float newDilation = previousDilations[a] *
                    Mathf.Lerp(dilationFactorRange.x, dilationFactorRange.y, UnityEngine.Random.value);
                verts[y * ringSubdivisions + a] = new Vector3
                {
                    x = direction.x * newDilation,
                    y = currentHeight,
                    z = direction.y * newDilation
                };
                previousDilations[a] = newDilation;
            }
        }
        return verts;
    }
    private int[] GenerateMeshTriangles()
    {
        // Allocate space for triangle indices.
        int[] triangles = new int[ringSubdivisions * (heightSubdivisions - 1) * 6];
        int i = 0;
        // Fill each cylinder ring.
        for (int y = 0; y < heightSubdivisions - 1; y++)
        {
            for (int r = 0; r < ringSubdivisions; r++)
            {
                // Calculate the indices of the four corners of the quad.
                int a = y * ringSubdivisions + r;
                int b = a + 1;
                int c = a + ringSubdivisions;
                int d = b + ringSubdivisions;
                // Wrap the final panel back around.
                if (r == ringSubdivisions - 1)
                {
                    b -= ringSubdivisions;
                    d -= ringSubdivisions;
                }
                // Generate the first triangle.
                triangles[i] = c; i++;
                triangles[i] = b; i++;
                triangles[i] = a; i++;
                // Generate the complementary triangle.
                triangles[i] = b; i++;
                triangles[i] = c; i++;
                triangles[i] = d; i++;
            }
        }
        return triangles;
    }
    private Vector2[] GenerateMeshUVs()
    {
        // Unroll the pillar with a seam along a vertical loop.
        Vector2[] UVs = new Vector2[ringSubdivisions * heightSubdivisions];
        // Align the uvs directly using a non-scaled cylinder unwrap.
        for (int y = 0; y < heightSubdivisions; y++)
            for (int r = 0; r < ringSubdivisions; r++)
                UVs[y * ringSubdivisions + r] = new Vector2
                {
                    x = (float)r / (ringSubdivisions - 1),
                    y = (float)y / (heightSubdivisions - 1)
                };
        return UVs;
    }
    #endregion
}