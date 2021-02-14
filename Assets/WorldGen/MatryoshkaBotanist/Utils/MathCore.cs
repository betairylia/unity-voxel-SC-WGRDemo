using System.Collections;
using UnityEngine;

namespace Matryoshka.Utils
{
    public static class MathCore
    {
        public static float CosineInterpolation(float a, float b, float x)
        {
            return a * (1 - (1 - Mathf.Cos(x * Mathf.PI)) / 2) + b * (1 - Mathf.Cos(x * Mathf.PI)) / 2;
        }

        public static float RandomRange(System.Random random, float min, float max)
        {
            return (float)random.NextDouble() * (max - min) + min;
        }
    }
}