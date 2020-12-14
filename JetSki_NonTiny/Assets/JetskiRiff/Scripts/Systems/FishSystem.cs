using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

/// <summary>
/// Handles the movement of fish actors in the scene.
/// </summary>
public sealed class FishSystem : SystemBase
{
    public Translation LocationToAvoid;

    protected override void OnUpdate()
    {
        // Define loop constants.
        float dt = Time.DeltaTime;
        float t = (float)Time.ElapsedTime;
        Random rand = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue));
        float3 avoid = LocationToAvoid.Value;
        // Process each entity.
        Entities.ForEach((ref LocalToWorld localToWorld, ref FishComponent fish) =>
        {
            fish.depth = ((noise.snoise(new float3(t, localToWorld.Position.x, localToWorld.Position.z) * 0.005f) + 1f) / 2f) * fish.depthWanderMagnitude;

            float3 heightAdjustedPosition = new float3
            {
                x = localToWorld.Position.x,
                y = - fish.depth,
                z = localToWorld.Position.z
            };

            float3 direction;
            // If the fish is near the avoided location,
            // swim away from it.
            if (math.distancesq(localToWorld.Position.TrimY(), avoid.TrimY()) < 90f)
            {
                direction = math.normalize(new float3
                {
                    x = heightAdjustedPosition.x - avoid.x,
                    z = heightAdjustedPosition.z - avoid.z,
                });
            }
            else
            {
                // Get the direction to the current target.
                direction = math.normalize(new float3
                {
                    x = fish.target.x - heightAdjustedPosition.x,
                    z = fish.target.z - heightAdjustedPosition.z,
                });
            }
            // Apply the new matrix data to the fish.
            localToWorld = new LocalToWorld
            {
                Value = float4x4.TRS(
                    // Apply movement towards the target.
                    new float3(heightAdjustedPosition + (direction * fish.speed * dt)),
                    // Look in the direction of the target.
                    quaternion.LookRotationSafe(direction, math.up()),
                    fish.scale
                )
            };

            // If the fish is within a certain distance of the target
            // then recalculate a new target.
            if (math.distancesq(localToWorld.Position.TrimY(), fish.target.TrimY()) < 1f)
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
