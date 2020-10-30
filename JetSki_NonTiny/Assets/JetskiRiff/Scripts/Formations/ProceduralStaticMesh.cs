using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// A procedurally generated static mesh that converts to an entity.
/// </summary>
public abstract class ProceduralStaticMesh : MonoBehaviour, IConvertGameObjectToEntity
{
    #region Inspector Fields
    [Tooltip("The material that will be used for the generated mesh.")]
    [SerializeField] private Material renderMaterial = null;
    #endregion
    #region Conversion Implementation
    public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
    {
        // Generate the mesh for this static object.
        Mesh mesh = Generate();
        // Generate the components for this object.
        RenderMesh renderMesh = ConfigureRenderMesh(mesh);
        Translation translation = ConfigureTranslation();
        LocalToWorld localToWorld = new LocalToWorld
        {
            Value = transform.localToWorldMatrix
        };
        RenderBounds renderBounds = new RenderBounds
        {
            Value = mesh.bounds.ToAABB()
        };
        // Add the components to the entity.
        dstManager.AddSharedComponentData(entity, renderMesh);
        dstManager.AddComponentData(entity, translation);
        dstManager.AddComponentData(entity, localToWorld);
        dstManager.AddComponentData(entity, renderBounds);
    }
    #endregion
    #region SubClass Requirements
    /// <summary>
    /// Procedurally generates a new mesh to be rendered.
    /// </summary>
    /// <returns>The generated mesh.</returns>
    protected abstract Mesh Generate();
    #endregion
    #region SubClass Optionals
    /// <summary>
    /// Defines render mesh parameters. Override this to change default settings.
    /// </summary>
    /// <param name="mesh">The mesh to use.</param>
    /// <returns>The RenderMesh component data.</returns>
    protected virtual RenderMesh ConfigureRenderMesh(Mesh mesh)
    {
        // Default renderer settings.
        return new RenderMesh
        {
            mesh = mesh,
            castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true,
            material = renderMaterial
        };
    }
    /// <summary>
    /// Defines translation parameters. Override this to change default settings.
    /// </summary>
    /// <returns>The Translation component data.</returns>
    protected virtual Translation ConfigureTranslation()
    {
        // By default just return this transforms location.
        return new Translation
        {
            Value = new float3
            {
                x = transform.position.x,
                y = transform.position.y,
                z = transform.position.z
            }
        };
    }
    #endregion
}
