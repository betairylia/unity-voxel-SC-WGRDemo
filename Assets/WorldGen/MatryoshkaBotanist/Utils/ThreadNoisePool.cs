using System;
using System.Collections;
using System.Collections.Concurrent;
using UnityEngine;

namespace Matryoshka.Utils
{
    public class PerThreadPool<T>
    {
        Func<T> newInstance;

        public PerThreadPool(Func<T> newInstance)
        {
            this.newInstance = newInstance;
        }

        public ConcurrentDictionary<int, T> instances { get; protected set; } = new ConcurrentDictionary<int, T>();

        public T Get(int threadIdx)
        {
            if(!instances.ContainsKey(threadIdx))
            {
                instances.TryAdd(threadIdx, newInstance());
            }
            return instances[threadIdx];
        }
    }

    public static class NoisePools
    {
        public static PerThreadPool<FastNoiseLite> OS2S_FBm_3oct_f0_1 = new PerThreadPool<FastNoiseLite>(
            () =>
            {
                FastNoiseLite noise = new FastNoiseLite();
                noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
                noise.SetFractalType(FastNoiseLite.FractalType.FBm);
                noise.SetFractalOctaves(3);
                noise.SetFrequency(0.1f);

                return noise;
            }
        );

        public static PerThreadPool<FastNoiseLite> OS2S_FBm_3oct_f1 = new PerThreadPool<FastNoiseLite>(
            () =>
            {
                FastNoiseLite noise = new FastNoiseLite();
                noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
                noise.SetFractalType(FastNoiseLite.FractalType.FBm);
                noise.SetFractalOctaves(3);
                noise.SetFrequency(1.0f);

                return noise;
            }
        );
    }
}