using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxelis.WorldGen
{
    public class SimplePerlin : ChunkGenerator
    {
        protected float Height(int x, int z)
        {
            return
                Mathf.PerlinNoise(((float)x - 0.1f) / 80.0f, ((float)z + 0.5f) / 80.0f) * 32 +
                Mathf.PerlinNoise(((float)x - 10.1f) / 40.0f, ((float)z - 0.5f) / 40.0f) * 16 +
                Mathf.PerlinNoise(((float)x + 20.15f) / 20.0f, ((float)z + 3.1f) / 20.0f) * 8 +
                Mathf.PerlinNoise(((float)x - 0.283f) / 10.0f, ((float)z - 0.8f) / 10.0f) * 4 +
                (-24);
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
                        {
                            if (y < 2)
                            {
                                chunk.blockData[idx] = Block.From32bitColor(GetID(Random.Range(219, 222), Random.Range(204, 206), Random.Range(104, 151), 255));
                            }
                            else
                            {
                                chunk.blockData[idx] = Block.From32bitColor(GetID(Random.Range(126, 187), Random.Range(204, 217), Random.Range(90, 145), 255));
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
