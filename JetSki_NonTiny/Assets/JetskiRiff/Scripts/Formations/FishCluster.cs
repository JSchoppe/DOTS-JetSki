using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using System.Collections;

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
    [SerializeField] private float spawnRadius = 10f;
    [Tooltip("The range of scales the fish can spawn with.")]
    [SerializeField] private Vector2 scale = new Vector2(0.9f, 1.1f);
    [Tooltip("The range of speeds the fish can spawn with.")]
    [SerializeField] private Vector2 speed = new Vector2(0.7f, 1.5f);
    [Tooltip("The distance of random travel for each behavior step.")]
    [SerializeField] private Vector2 wander = new Vector2(4f, 6f);
    [Tooltip("The vertical distance that a fish can travel from the surface.")]
    [SerializeField] private Vector2 depthWander = new Vector2(0f, 3f);
    #endregion
    #region Editor Functions
    private void OnValidate()
    {
        // Clamp inspector fields.
        if (spawnRadius < 0f)
            spawnRadius = 0f;
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
    #region Entity Instantiation
    private void Start()
    {
        SpawnSomeDamnFish();
        StartCoroutine(SpawnSomeDamnFishAfterAFrickinWhile());
    }

    private IEnumerator SpawnSomeDamnFishAfterAFrickinWhile()
    {
        yield return new WaitForSeconds(5f);
        SpawnSomeDamnFish();
    }

    public void SpawnSomeDamnFish()
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
            float2 locationDir = Float2Helper.RandomDirection(0f, spawnRadius);
            float3 location = new float3
            {
                x = transform.localPosition.x + locationDir.x,
                z = transform.localPosition.z + locationDir.y
            };
            manager.AddComponentData(newFish, new Translation
            {
                Value = location
            });
            manager.AddComponentData(newFish, new LocalToWorld
            {
                Value = transform.localToWorldMatrix
            });
            manager.AddComponentData(newFish, new RenderBounds
            {
                Value = GetComponent<MeshFilter>().mesh.bounds.ToAABB()
            });
            float2 targetDir = Float2Helper.RandomDirection(0f, spawnRadius);
            manager.AddComponentData(newFish, new FishComponent
            {
                speed = Mathf.Lerp(speed.x, speed.y, UnityEngine.Random.value),
                scale = Mathf.Lerp(scale.x, scale.y, UnityEngine.Random.value),
                wanderMagnitude = Mathf.Lerp(wander.x, wander.y, UnityEngine.Random.value),
                depthWanderMagnitude = Mathf.Lerp(depthWander.x, depthWander.y, UnityEngine.Random.value),
                target = location + new float3
                {
                    x = targetDir.x,
                    z = targetDir.y
                }
            });
        }
    }

    #endregion
}
