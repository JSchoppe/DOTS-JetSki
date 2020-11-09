using Unity.Mathematics;

/// <summary>
/// Contains extension methods for float3 structs.
/// </summary>
public static class Float3Extensions
{
    #region Trim Functions
    /// <summary>
    /// Removes the x component, shifting y and z components forward.
    /// </summary>
    /// <param name="value">The three component value to trim.</param>
    /// <returns>A float2 with x and y components corresponding to y and z.</returns>
    public static float2 TrimX(this float3 value)
    {
        return new float2
        {
            x = value.y,
            y = value.z
        };
    }
    /// <summary>
    /// Removes the y component, shifting the z component forward.
    /// </summary>
    /// <param name="value">The three component value to trim.</param>
    /// <returns>A float2 with x and y components corresponding to x and z.</returns>
    public static float2 TrimY(this float3 value)
    {
        return new float2
        {
            x = value.x,
            y = value.z
        };
    }
    /// <summary>
    /// Removes the z component.
    /// </summary>
    /// <param name="value">The three component value to trim.</param>
    /// <returns>A float2 with x and y components.</returns>
    public static float2 TrimZ(this float3 value)
    {
        return new float2
        {
            x = value.x,
            y = value.y
        };
    }
    #endregion
}
