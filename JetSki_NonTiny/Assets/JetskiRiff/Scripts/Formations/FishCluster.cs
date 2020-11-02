using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

/// <summary>
/// Creates a cluster of fish actors at a given location.
/// </summary>
public sealed class FishCluster : MonoBehaviour
{
    // TODO: Add fields that allow fish to follow the player.
    #region Inspector Fields
    [Header("Spawning Parameters")]
    [Tooltip("The number of fish to spawn.")]
    [SerializeField] private int fishCount = 50;
    [Tooltip("The range of scales the fish can spawn with.")]
    [SerializeField] private Vector2 scale = new Vector2(0.9f, 1.1f);
    [Tooltip("The range of speeds the fish can spawn with.")]
    [SerializeField] private Vector2 speed = new Vector2(0.7f, 1.5f);
    [Tooltip("The distance of random travel for each behavior step.")]
    [SerializeField] private Vector2 wander = new Vector2(4f, 6f);
    #endregion
#if DEBUG
    #region Editor Functions
    private void OnValidate()
    {
        // Clamp inspector fields.
        if (fishCount < 0)
            fishCount = 0;
        if (scale.y < scale.x)
            scale.y = scale.x;
        if (speed.y < speed.x)
            speed.y = speed.x;
        if (wander.y < wander.x)
            wander.y = wander.x;
    }
    #endregion
#endif
    #region Entity Instantiation
    public void Start()
    {
        // Retrieve the entity manager.
        EntityManager manager = World.DefaultGameObjectInjectionWorld.EntityManager;
        // Populate fish into the scene.
        for (int i = 0; i < fishCount; i++)
        {
            // Add the components to the entity.
            Entity newFish = manager.CreateEntity();
            manager.AddSharedComponentData(newFish, new RenderMesh
            {
                mesh = GetComponent<MeshFilter>().mesh,
                material = GetComponent<Renderer>().material,
                castShadows = UnityEngine.Rendering.ShadowCastingMode.On
            });
            manager.AddComponentData(newFish, new Translation
            {
                Value = new float3
                {
                    x = transform.position.x,
                    y = transform.position.y,
                    z = transform.position.z
                }
            });
            manager.AddComponentData(newFish, new LocalToWorld
            {
                Value = transform.localToWorldMatrix
            });
            manager.AddComponentData(newFish, new RenderBounds
            {
                Value = GetComponent<MeshFilter>().mesh.bounds.ToAABB()
            });
            manager.AddComponentData(newFish, new FishComponent
            {
                speed = Mathf.Lerp(speed.x, speed.y, UnityEngine.Random.value),
                scale = Mathf.Lerp(scale.x, scale.y, UnityEngine.Random.value),
                wanderMagnitude = Mathf.Lerp(wander.x, wander.y, UnityEngine.Random.value)
            });
        }
        Destroy(gameObject);
    }
    #endregion
}
