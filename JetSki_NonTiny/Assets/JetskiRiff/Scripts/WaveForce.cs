using Unity.Mathematics;

public struct WaveForce
{
    public readonly float2 origin;
    public readonly float startTime;
    public readonly float baseMagnitude;
    public readonly float fallOff;

    public WaveForce(float2 origin, float startTime, float baseMagnitude, float fallOff)
    {
        this.origin = origin;
        this.startTime = startTime;
        this.baseMagnitude = baseMagnitude;
        this.fallOff = fallOff;
    }
}
