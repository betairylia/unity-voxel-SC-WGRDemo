using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxelis.WorldGen
{
    public class GaintSteps : ChunkGenerator
    {
        FastNoiseLite noise, cnoise;

        public GaintSteps()
        {
            // Setup noise generators
            cnoise = new FastNoiseLite();
            cnoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
            cnoise.SetFrequency(0.2f);
            cnoise.SetCellularReturnType(FastNoiseLite.CellularReturnType.CellValue);

            noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(3);
            noise.SetFractalGain(0.5f);
            noise.SetFractalWeightedStrength(0.5f);
        }

        protected float Height(int x, int z)
        {
            //return noise.GetNoise(x / 2.0f, z / 2.0f) * 30 + 16 + cnoise.GetNoise(x, z) * 4 + 16;
            return cnoise.GetNoise(x, z) * 4 + 16;
            //return 16;
        }

        public override bool Generate(Chunk chunk, World world)
        {
            // Generate the chunk
            for (int i = 0; i < 32; i++)
                for (int j = 0; j < 32; j++)
                {
                    int x = chunk.positionOffset.x + i;
                    int z = chunk.positionOffset.z + j;

                    float h = Height(x, z);

                    for (int k = 0; k < 32; k++)
                    {
                        int idx = i * 32 * 32 + k * 32 + j;

                        int y = chunk.positionOffset.y + k;

                        if (y < h)
                        //if(noise.GetNoise(x, y, z) > 0.3)
                        {
                            chunk.blockData[idx] = Block.From32bitColor((uint)Blocks.stone);
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

    public class Perlin3D : ChunkGenerator
    {
        FastNoiseLite noise, cnoise;

        public Perlin3D()
        {
            // Setup noise generators
            cnoise = new FastNoiseLite();
            cnoise.SetNoiseType(FastNoiseLite.NoiseType.Cellular);
            cnoise.SetFrequency(0.2f);
            cnoise.SetCellularReturnType(FastNoiseLite.CellularReturnType.CellValue);

            noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(3);
            noise.SetFractalGain(0.5f);
            noise.SetFractalWeightedStrength(0.5f);
        }

        public override bool Generate(Chunk chunk, World world)
        {
            // Generate the chunk
            for (int i = 0; i < 32; i++)
                for (int j = 0; j < 32; j++)
                {
                    int x = chunk.positionOffset.x + i;
                    int z = chunk.positionOffset.z + j;

                    for (int k = 0; k < 32; k++)
                    {
                        int idx = i * 32 * 32 + k * 32 + j;

                        int y = chunk.positionOffset.y + k;
                        int ground = Mathf.Max(0, 10 - y);
                        int sky = Mathf.Max(0, 30 - world.worldHeight * 32 - y);

                        if (noise.GetNoise(x, y, z) > 0.3 - (ground * 0.07) + (sky * 0.03))
                        {
                            if (ground > 0)
                            {
                                chunk.blockData[idx] = Block.From32bitColor((uint)Blocks.dirt);
                            }
                            else if (y < 80)
                            {
                                chunk.blockData[idx] = Block.From32bitColor((uint)Blocks.grass);
                            }
                            else
                            {
                                chunk.blockData[idx] = Block.From32bitColor((uint)Blocks.stone);
                            }
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
