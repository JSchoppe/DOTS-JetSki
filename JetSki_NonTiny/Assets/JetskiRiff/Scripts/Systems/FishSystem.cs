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
        float t = (float)Time.ElapsedTime;
        Random rand = new Random((uint)UnityEngine.Random.Range(0, int.MaxValue));
        // Process each entity.
        Entities.ForEach((ref LocalToWorld localToWorld, ref FishComponent fish, in WaveComponent wave) =>
        {
            float time = t * wave.ambientFlowSpeed;
            float waveHeight = wave.ambientFlowHeight * noise.snoise(new float2
            {
                x = localToWorld.Position.x * wave.ambientFlowSpread + time,
                y = localToWorld.Position.z * wave.ambientFlowSpread + time
            });
            /*
            float waveHeight = wave.WaveHeightAt(localToWorld.Position, ref time);
            */

            float3 heightAdjustedPosition = new float3
            {
                x = localToWorld.Position.x,
                y = waveHeight,
                z = localToWorld.Position.z
            };

            // Get the direction to the current target.
            float3 direction = math.normalize(fish.target - heightAdjustedPosition);
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
        }).WithoutBurst().Run();
    }
}
