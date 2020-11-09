using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Component for entities that calculate ambient waves.
/// </summary>
public struct WaveComponent : IComponentData
{
    #region Component Fields
    /// <summary>
    /// The speed that the waves flow relative to time.
    /// </summary>
    public float ambientFlowSpeed;
    /// <summary>
    /// The scaling of the simplex noise.
    /// </summary>
    public float ambientFlowSpread;
    /// <summary>
    /// The elevation amplitude of the simplex noise.
    /// </summary>
    public float ambientFlowHeight;
    #endregion
    #region Helper Methods
    /// <summary>
    /// Calculates the wave height relative to position and time.
    /// </summary>
    /// <param name="planarPosition">The top down position relative to the water plane.</param>
    /// <param name="scaledTime">The time (must be scaled by ambient flow speed before passing).</param>
    /// <returns>The local elevation of the waves.</returns>
    public float WaveHeightAt(float2 planarPosition, float scaledTime)
    {
        return ambientFlowHeight * noise.snoise(new float2
        {
            x = planarPosition.x * ambientFlowSpread + scaledTime,
            y = planarPosition.y * ambientFlowSpread + scaledTime
        });
    }
    #endregion
}
