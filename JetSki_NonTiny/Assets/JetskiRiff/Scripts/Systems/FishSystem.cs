using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles the movement of fish actors in the scene.
/// </summary>
public sealed class FishSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Define loop constants.
        float dt = Time.DeltaTime;
        Random rand = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue));
        // Process each entity.
        Entities.ForEach((ref LocalToWorld localToWorld, ref FishComponent fish) =>
        {
            // Get the direction to the current target.
            float3 direction = math.normalize(fish.target - localToWorld.Position);
            // Apply the new matrix data to the fish.
            localToWorld = new LocalToWorld
            {
                Value = float4x4.TRS(
                    // Apply movement towards the target.
                    new float3(localToWorld.Position + (direction * fish.speed * dt)),
                    // Look in the direction of the target.
                    quaternion.LookRotationSafe(direction, math.up()),
                    fish.scale
                )
            };

            // If the fish is within a certain distance of the target
            // then recalculate a new target.
            if (math.distancesq(localToWorld.Position, fish.target) < 1f)
            {
                float3 target = fish.target;
                target += new float3
                {
                    // Choose a new random target.
                    x = rand.NextFloat(-1f, 1f) * fish.wanderMagnitude,
                    z = rand.NextFloat(-1f, 1f) * fish.wanderMagnitude
                };
                fish.target = target;
            }
        }).ScheduleParallel();
    }
}
