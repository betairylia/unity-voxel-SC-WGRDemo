using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.FractalSystemCore
{
    class MathCore
    {
        public static float CosineInterpolation(float a, float b, float x)
        {
            return a * (1 - (1 - Mathf.Cos(x * Mathf.PI)) / 2) + b * (1 - Mathf.Cos(x * Mathf.PI)) / 2;
        }

        public static float[] PerlinNoise(System.Random random, int count, int startStep, float startAmplitude, float[] offset = null)
        {
            //Debug.Log(startStep);

            int i, j, rCount = count;

            if (count % startStep != 0)
            {
                count = startStep * ((count / startStep) + 1);
            }
            count++;

            List<float> result = new List<float>();

            for (i = 0; i < count; i++)
            {
                result.Add(0);
            }

            while (startStep > 0)
            {
                for (i = 0; i < count; i += startStep)
                {
                    //随机
                    result[i] += RandomRange(random, -1.0f, 1.0f) * startAmplitude;

                    //插值
                    if (i > 0)
                    {
                        for (j = i - startStep + 1; j < i; j++)
                        {
                            result[j] = CosineInterpolation(result[i - startStep], result[i], (j - i + startStep - 1) / ((float)startStep));
                        }
                    }
                }

                //减小振幅
                startAmplitude /= 2;
                startStep /= 2;
            }

            if (offset != null)
            {
                //增加offset（“直流分量”）
                for (i = 0; i < rCount; i++)
                {
                    result[i] += offset[i];
                }
            }

            return result.ToArray();
        }

        public static float RandomRange(System.Random random, float min, float max)
        {
            return (float)random.NextDouble() * (max - min) + min;
        }
    }
}