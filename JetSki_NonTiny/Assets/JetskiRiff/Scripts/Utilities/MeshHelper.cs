using UnityEngine;

#region Geometry Structs
/// <summary>
/// Defines the geometry of a plane in 3D space.
/// </summary>
public struct PlaneDefinition
{
    /// <summary>
    /// The starting point of the plane.
    /// </summary>
    public Vector3 origin;
    /// <summary>
    /// The direction of the first axis of the plane.
    /// </summary>
    public Vector3 axisA;
    /// <summary>
    /// The direction of the second axis of the plane.
    /// </summary>
    public Vector3 axisB;
    /// <summary>
    /// The length of the plane along the first axis.
    /// </summary>
    public float lengthA;
    /// <summary>
    /// The length of the plane along the second axis.
    /// </summary>
    public float lengthB;
}
#endregion

/// <summary>
/// Contains methods for generating geometry.
/// </summary>
public static class MeshHelper
{
    #region Generate Quad Grid Overloads
    /// <summary>
    /// Generates a new quad grid mesh with the given parameters.
    /// </summary>
    /// <param name="cuts">The number of subdivisions along each axis.</param>
    /// <param name="plane">Defines the geometry of where the plane is drawn.</param>
    /// <returns>The generated quad grid mesh.</returns>
    public static Mesh GenerateQuadGrid(int cuts, PlaneDefinition plane)
    { return GenerateQuadGrid(cuts, plane, 1f); }
    /// <summary>
    /// Generates a new quad grid mesh with the given parameters.
    /// </summary>
    /// <param name="cuts">The number of subdivisions along each axis.</param>
    /// <param name="plane">Defines the geometry of where the plane is drawn.</param>
    /// <param name="uvScale">Defines the baseline uv coordinates scaling.</param>
    /// <returns>The generated quad grid mesh.</returns>
    public static Mesh GenerateQuadGrid(int cuts, PlaneDefinition plane, float uvScale)
    { return GenerateQuadGrid(plane, cuts, cuts, uvScale, false); }
    /// <summary>
    /// Generates a new quad grid mesh with the given parameters.
    /// </summary>
    /// <param name="cuts">The number of subdivisions along each axis.</param>
    /// <param name="plane">Defines the geometry of where the plane is drawn.</param>
    /// <param name="uvScale">Defines the baseline uv coordinates scaling.</param>
    /// <param name="forGizmo">When this is for a gizmo, some steps are skipped.</param>
    /// <returns>The generated quad grid mesh.</returns>
    public static Mesh GenerateQuadGrid(int cuts, PlaneDefinition plane, float uvScale, bool forGizmo)
    { return GenerateQuadGrid(plane, cuts, cuts, uvScale, forGizmo); }
    /// <summary>
    /// Generates a new quad grid mesh with the given parameters.
    /// </summary>
    /// <param name="maxEdgeLength">The length of the cell edge will be less than this.</param>
    /// <param name="plane">Defines the geometry of where the plane is drawn.</param>
    /// <returns>The generated quad grid mesh.</returns>
    public static Mesh GenerateQuadGrid(float maxEdgeLength, PlaneDefinition plane)
    { return GenerateQuadGrid(maxEdgeLength, plane, 1f); }
    /// <summary>
    /// Generates a new quad grid mesh with the given parameters.
    /// </summary>
    /// <param name="maxEdgeLength">The length of the cell edge will be less than this.</param>
    /// <param name="plane">Defines the geometry of where the plane is drawn.</param>
    /// <param name="uvScale">Defines the baseline uv coordinates scaling.</param>
    /// <returns>The generated quad grid mesh.</returns>
    public static Mesh GenerateQuadGrid(float maxEdgeLength, PlaneDefinition plane, float uvScale)
    { return GenerateQuadGrid(maxEdgeLength, plane, uvScale, false); }
    /// <summary>
    /// Generates a new quad grid mesh with the given parameters.
    /// </summary>
    /// <param name="maxEdgeLength">The length of the cell edge will be less than this.</param>
    /// <param name="plane">Defines the geometry of where the plane is drawn.</param>
    /// <param name="uvScale">Defines the baseline uv coordinates scaling.</param>
    /// <param name="forGizmo">When this is for a gizmo, some steps are skipped.</param>
    /// <returns>The generated quad grid mesh.</returns>
    public static Mesh GenerateQuadGrid(float maxEdgeLength, PlaneDefinition plane, float uvScale, bool forGizmo)
    {
        int cutsA = Mathf.CeilToInt(plane.lengthA / maxEdgeLength) + 1;
        int cutsB = Mathf.CeilToInt(plane.lengthB / maxEdgeLength) + 1;
        return GenerateQuadGrid(plane, cutsA, cutsB, uvScale, forGizmo);
    }
    #endregion
    #region Generate Quad Grid Implementation
    private static Mesh GenerateQuadGrid(PlaneDefinition plane, int cutsA, int cutsB, float uvScale, bool forGizmo)
    {
        // Precalculate steps along each of the given axes.
        Vector3 stepA = plane.axisA.normalized * (plane.lengthA / (cutsA + 1));
        Vector3 stepB = plane.axisB.normalized * (plane.lengthB / (cutsB + 1));
        float stepALength = stepA.magnitude;
        float stepBLength = stepB.magnitude;
        // Apply UV scaling to accomodate larger dimension.
        uvScale /= Mathf.Max(plane.lengthA, plane.lengthB);
        // Define iteration variables used multiple times.
        int a, b, i;

        // Generate the grid of vertices and handle the UV
        // coordinates using a flat projected mapping.
        Vector3[] vertices = new Vector3[(cutsA + 2) * (cutsB + 2)];
        Vector2[] uvs = new Vector2[vertices.Length];
        for (a = 0; a <= cutsA + 1; a++)
        {
            for (b = 0; b <= cutsB + 1; b++)
            {
                vertices[a * (cutsA + 2) + b] = plane.origin + stepA * a + stepB * b;
                uvs[a * (cutsA + 2) + b] = new Vector2
                {
                    x = a * stepALength * uvScale,
                    y = b * stepBLength * uvScale
                };
            }
        }

        // Fill each of the tiles of the grid with two triangles.
        int[] triangles = new int[(cutsA + 1) * (cutsB + 1) * 6];
        i = 0;
        for (a = 0; a < cutsA + 1; a++)
        {
            for (b = 0; b < cutsB + 1; b++)
            {
                // Calculate the indices of the four corners of the quad.
                int w = a * (cutsA + 2) + b;
                int x = w + 1;
                int y = (a + 1) * (cutsB + 2) + b;
                int z = y + 1;
                // Generate the first triangle.
                triangles[i] = w; i++;
                triangles[i] = x; i++;
                triangles[i] = y; i++;
                // Generate the complementary triangle.
                triangles[i] = z; i++;
                triangles[i] = y; i++;
                triangles[i] = x; i++;
            }
        }

        // Apply geometry to the mesh.
        Mesh mesh = new Mesh
        {
            vertices = vertices,
            triangles = triangles
        };
        // Skip some stuff if this is only for the gizmo.
        if (!forGizmo)
        {
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
        return mesh;
    }
    #endregion
}
