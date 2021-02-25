using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxelis
{
    enum Blocks : uint
    {
        stone = 0x8a8a8aff,
        dirt = 0x784219ff,
        grass = 0x668f24ff,
        sand = 0xe8e1b0ff,
        cloud = 0xffffffff,
        wood = 0x4a1f00ff,
        leaf = 0x4fa61eff,
        deepGrass = 0x3a8515ff,

        glowingBlue = 0x00b7ce7f,
    }

    namespace WorldGen
    {
        public class MoonlightForest : ChunkGenerator
        {
            FastNoiseLite mountain, plain, mask, fbmNoise;

            Color cgrass = new Color(0.282f, 0.486f, 0.220f, 1);
            Color cdirt = new Color(0.420f, 0.290f, 0.169f, 1);
            Color cstone = new Color(0.365f, 0.365f, 0.388f, 1);
            Color csnow = new Color(0.957f, 0.984f, 0.996f, 1);

            public MoonlightForest()
            {
                // Setup noise generators
                mountain = new FastNoiseLite();
                mountain.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
                //mountain.SetFractalType(FastNoiseLite.FractalType.Ridged);
                mountain.SetFractalType(FastNoiseLite.FractalType.FBm);
                mountain.SetFractalOctaves(5);
                mountain.SetFractalGain(0.5f);
                //mountain.SetFrequency(0.00078f);
                mountain.SetFrequency(0.00144f);
                mountain.SetFractalLacunarity(2.0f);
                //noise.SetFractalWeightedStrength(0.5f);

                fbmNoise = new FastNoiseLite();
                fbmNoise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
                //fbmNoise.SetFractalType(FastNoiseLite.FractalType.FBm);
                //fbmNoise.SetFractalOctaves(3);
                //fbmNoise.SetFractalGain(0.5f);
                //fbmNoise.SetFractalWeightedStrength(0.5f);
            }

            protected float Height(int x, int z, out float value, out float slope, out float erosion)
            {
                float zero = mountain.GetNoise(x, z);
                float first = mountain.GetNumericGradient(x, z).magnitude;
                float second = mountain.GetNumericLaplacian(x, z);

                value = zero;
                slope = first;
                erosion = second;

                return (zero + 100 * Mathf.Pow(-Mathf.Min(second, 0), 2)) * 128 + 0;
                //return 16;
            }

            protected Vector2Int GridIdx(int x, int z, float size = 64)
            {
                Vector2 perturb = fbmNoise.GetNumericGradient(x / size / 2, z / size / 2) * size * 150;
                //Vector2 perturb = fbmNoise.GetNumericGradient(x / size / 2, z / size / 2);
                //perturb = perturb.normalized * size * 4;
                return new Vector2Int(Mathf.FloorToInt((float)(x + perturb.x) / size), Mathf.FloorToInt((float)(z + perturb.y) / size));
            }

            public override bool Generate(Chunk chunk, World world)
            {
                // Generate the chunk
                for (int i = 0; i < 32; i++)
                    for (int j = 0; j < 32; j++)
                    {
                        int x = chunk.positionOffset.x + i;
                        int z = chunk.positionOffset.z + j;

                        float v, s, e;

                        float h = Height(x, z, out v, out s, out e);
                        Vector2Int gix = GridIdx(x, z);
                        float rng = Hash1f.GetNoise(gix.x, gix.y);

                        float cf_sharpness = Mathf.Clamp(s * 250 - 0.5f, 0, 1);
                        float cf_height = Mathf.Clamp(v * 5 - 1.5f, 0, 1);
                        Color tileColor =
                            (cf_sharpness) * (cf_height) * cstone +
                            (1 - cf_sharpness) * (cf_height) * csnow +
                            (cf_sharpness) * (1 - cf_height) * cdirt +
                            (1 - cf_sharpness) * (1 - cf_height) * cgrass;

                        for (int k = 0; k < 32; k++)
                        {
                            int idx = i * 32 * 32 + k * 32 + j;

                            int y = chunk.positionOffset.y + k;

                            if (y < h)
                            //if(noise.GetNoise(x, y, z) > 0.3)
                            {
                                //if (y < 2)
                                //{
                                //    chunk.chunkData[idx] = (uint)Blocks.sand;
                                //}
                                //else
                                //{
                                //    chunk.chunkData[idx] = GetID((int)(Mathf.Clamp(s * 30, 0, 1) * 255), (int)(Mathf.Clamp((e * 100), 0, 1) * 255), (int)(Mathf.Clamp(v, 0, 1) * 255), 255);
                                //}

                                // Color by terrain
                                //chunk.chunkData[idx] = GetID(tileColor);
                                chunk.blockData[idx] = Block.From32bitColor((uint)Blocks.deepGrass);
                            }
                            else
                            {
                                chunk.blockData[idx] = Block.Empty;
                            }
                        }
                    }

                return true;
            }
        }
    }
}
