using System;
using System.Collections;
using UnityEngine;

namespace WorldGen.WorldSketch
{
    public class TerracedMountainsTest : IWorldSketcher
    {
        public void FillHeightmap(
            ref float[] heightMap,
            ref float[] erosionMap,
            ref float[] waterMap,
            int sizeX,
            int sizeY,

            float gain = 1.0f)
        {
            // Mountain / plains mask
            var isPlains = new FastNoiseLite();
            isPlains.SetFractalType(FastNoiseLite.FractalType.FBm);
            isPlains.SetFractalOctaves(2);
            isPlains.SetFrequency(0.001f);

            // noise generators
            var mountain = new FastNoiseLite();
            mountain.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            //mountain.SetFractalType(FastNoiseLite.FractalType.Ridged);

            mountain.SetFractalType(FastNoiseLite.FractalType.FBm);
            mountain.SetFractalOctaves(5);
            mountain.SetFractalGain(0.5f);
            //mountain.SetFrequency(0.00078f);
            mountain.SetFrequency(0.008f);
            mountain.SetFractalLacunarity(2.0f);

            // noise generators
            var water = new FastNoiseLite();
            water.SetSeed(123555);
            water.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            water.SetFractalType(FastNoiseLite.FractalType.Ridged);

            water.SetFractalOctaves(1);
            water.SetFractalGain(0.5f);
            water.SetFrequency(0.02f);
            water.SetFractalLacunarity(0.5f);

            // base shape
            float minv = 100.0f, maxv = -100.0f;
            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    waterMap[i * sizeY + j] = water.GetNoise(i, j) - 0.85f;
                    float isRiver = Mathf.Max(0.0f, waterMap[i * sizeY + j]) * 10.0f;
                    heightMap[i * sizeY + j] = mountain.GetNoise(i, j) * isPlains.GetNoise(i, j);
                    minv = Mathf.Min(heightMap[i * sizeY + j], minv);
                    maxv = Mathf.Max(heightMap[i * sizeY + j], maxv);
                }
            }

            Debug.Log($"Range: {minv} ~ {maxv}");

            // normalize
            for (int i = 0; i < sizeX; i++)
            {
                for (int j = 0; j < sizeY; j++)
                {
                    heightMap[i * sizeY + j] = (heightMap[i * sizeY + j] - minv) / (maxv - minv);
                    heightMap[i * sizeY + j] *= 0.65f;
                    //heightMap[i * sizeY + j] -= Mathf.Max(-0.06f, waterMap[i * sizeY + j]) * 0.04f;
                    heightMap[i * sizeY + j] = Mathf.Max(heightMap[i * sizeY + j], 0.0f) + 0.1f;
                }
            }

            // backup for erosion map calculation
            Array.Copy(heightMap, erosionMap, sizeX * sizeY);

            // erosion
            var erosionDevice = new HydraulicErosionGPU();
            erosionDevice.Erode(ref heightMap, sizeY); // FIXME: adjust the erosion code for non-square maps

            for (int i = 0; i < sizeX * sizeY; i++)
            {
                // positive means the part is raised during erosion step.
                erosionMap[i] = heightMap[i] - erosionMap[i];
            }
        }
    }
}