using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

public static class Float2Helper
{
    public static float2 RandomDirection(float magnitudeMin, float magnitudeMax)
    {
        float randomAngle = UnityEngine.Random.Range(0f, 2f * math.PI);
        float magnitude = UnityEngine.Random.Range(magnitudeMin, magnitudeMax);
        return new float2
        {
            x = math.cos(randomAngle) * magnitude,
            y = math.sin(randomAngle) * magnitude
        };
    }
    public static float2 RandomDirection(float magnitude)
    {
        return RandomDirection(magnitude, magnitude);
    }
    public static float2 RandomDirection()
    {
        return RandomDirection(1f);
    }
}
