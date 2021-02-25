using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;
using Substrate;
using Substrate.Core;
using System.Threading;

namespace Voxelis.Minecraft
{
    // FIXME: Black magic in use
    // see https://qiita.com/tatsunoru/items/611d0378086dc5986249
    public struct MCALoaderChunkGeneratorJobWrapper : IJob
    {
        public int cx, cz, cySize;
        public GCHandle<List<Chunk>> chunks;
        public GCHandle<Matryoshka.Utils.PerThreadPool<IChunkManager>> cmPoolHolder;

        public void Execute()
        {
            IChunkManager cm = cmPoolHolder.Target.Get(Thread.CurrentThread.ManagedThreadId);

            // Get chunks
            for (int mc_cx = 0; mc_cx < 2; mc_cx ++)
            {
                for(int mc_cz = 0; mc_cz < 2; mc_cz ++)
                {
                    if(cm.ChunkExists(cx * 2 + mc_cx, cz * 2 + mc_cz))
                    {
                        var mc_blocks = cm.GetChunkRef(cx * 2 + mc_cx, cz * 2 + mc_cz).Blocks;
                        if(mc_blocks == null) { continue; }

                        for (int cy = 0; cy < cySize; cy++)
                        {
                            Chunk chunk = chunks.Target[cy];
                            BoundsInt b = new BoundsInt(0, 0, 0, 16, 32, 16);
                            foreach (var pos in b.allPositionsWithin)
                            {
                                //if (pos.y + cy * 32 < 5)
                                //{
                                //    chunk.chunkData[pos.x * 1024 + pos.y * 32 + pos.z] = 0xffffffff;
                                //}
                                //else
                                //{
                                //    chunk.chunkData[pos.x * 1024 + pos.y * 32 + pos.z] = 0x00000000;
                                //}

                                AlphaBlock blk = mc_blocks.GetBlock(pos.x, pos.y + cy * 32, pos.z);
                                int ix = (pos.x + mc_cx * 16) * 1024 + pos.y * 32 + (pos.z + mc_cz * 16);
                                uint cbk = 0x00000000;

                                switch(blk.ID)
                                {
                                    // air
                                    case 0:
                                        cbk = 0;
                                        break;

                                    // stone
                                    case 1:
                                        cbk = 0x6b6b6bff;
                                        break;

                                    // grass
                                    case 2:
                                        cbk = 0x71a32aff;
                                        break;

                                    // dirt
                                    case 3:
                                        cbk = 0x694429ff;
                                        break;

                                    // log
                                    case 17:
                                        cbk = 0x96623bff;
                                        break;

                                    // leaves
                                    case 18:
                                        cbk = 0x147a23ff;
                                        break;

                                    // water
                                    case 9:
                                        cbk = 0x0000FFFF;
                                        break;

                                    default:
                                        cbk = 0xFFFFFFFF;
                                        break;
                                }

                                chunk.blockData[ix] = Block.From32bitColor(cbk);
                            }

                            chunk._PopulateFinish();
                        }
                    }
                }
            }
        }
    }

    public class MCALoaderChunkGeneratorJob : CustomJobs.MultipleChunkJob
    {
        int x, ySize, z;
        MCALoaderChunkGeneratorJobWrapper job;
        public static Matryoshka.Utils.PerThreadPool<IChunkManager> cmPool;
        //NbtWorld nbtWorld;

        public MCALoaderChunkGeneratorJob(NbtWorld nbtWorld, string savePath, int x, int ySize, int z, List<Chunk> chunksFromYBottomToTop) : base(chunksFromYBottomToTop)
        {
            this.x = x;
            this.ySize = ySize;
            this.z = z;
            //this.nbtWorld = nbtWorld;
            if(cmPool == null)
            {
                cmPool = new Matryoshka.Utils.PerThreadPool<IChunkManager>(() =>
                {
                    //return NbtWorld.Open(savePath).GetChunkManager();
                    return nbtWorld.GetChunkManager();
                });
            }
            this.isUnique = false;
        }

        public override string ToString()
        {
            return $"Load MCA for column {x}, {z}";
        }

        protected override void OnExecute()
        {
            job = new MCALoaderChunkGeneratorJobWrapper()
            {
                cx = x,
                cySize = ySize,
                cz = z
            };

            job.chunks.Create(chunks as List<Chunk>);
            job.cmPoolHolder.Create(cmPool);

            jobHandle = job.Schedule();
            //job.Execute();
        }

        protected override void OnFinish()
        {
            base.OnFinish();

            job.chunks.Dispose();
            job.cmPoolHolder.Dispose();
        }
    }
}