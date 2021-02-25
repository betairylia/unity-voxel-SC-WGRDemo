#define PROFILE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Substrate;
using Substrate.Core;

namespace Voxelis.Minecraft
{
    public class MCAWorld : World
    {
        public string MCAPath = @"D:\mcserver_backup\minecraft\server\world";
        NbtWorld m_MCAWorld;
        MCALoaderChunkGeneratorJob serialJob;
        IChunkManager m_cm;

        protected void SetColumn(int x, int z)
        {
#if PROFILE
            UnityEngine.Profiling.Profiler.BeginSample("SetColumn: Fill List");
#endif
            List<Chunk> column = new List<Chunk>();
            for(int y = 0; y < worldHeight; y++)
            {
                Vector3Int dest = new Vector3Int(x, y, z);
                Chunk ch = GetChunk(dest);
                ch._PopulateStart(dest * 32);
                column.Add(ch);
            }
#if PROFILE
            UnityEngine.Profiling.Profiler.BeginSample("SetColumn: TryAddJob");
#endif
            // Single-threaded
            MCALoaderChunkGeneratorJob pastJob = serialJob;
            serialJob = new MCALoaderChunkGeneratorJob(m_MCAWorld, MCAPath, x, worldHeight, z, column);
            if(pastJob != null)
            {
                serialJob.Depends(pastJob);
            }
            CustomJobs.CustomJob.TryAddJob(serialJob);
#if PROFILE
            UnityEngine.Profiling.Profiler.EndSample();
#endif
#if PROFILE
            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

        protected override void SetWorld()
        {
            // Open MCA world
            m_MCAWorld = NbtWorld.Open(MCAPath);
            //m_cm = m_MCAWorld.GetChunkManager();

            // Sanity check
            //int tmp = 0;
            //foreach(ChunkRef ch in m_cm)
            //{
            //    tmp++;
            //    if(tmp > 100) { break; }
            //    if(ch.Blocks == null) { continue; }
            //    Debug.Log($"Good chunk at {ch.X}, {ch.Z}");
            //}
        }

        // Modified from World.cs
        protected override IEnumerator BuildTasks()
        {
            Vector3Int currentChunk = new Vector3Int((int)(follows.position.x / 32), 0, (int)(follows.position.z / 32));
            int range = Mathf.CeilToInt(showDistance * 1.5f / 32.0f);

            // Heavy; no need to check every chunk each frame. 512 render distance = 9604 iterations
            // Build new chunks
            for (int cX = -range; cX <= range; cX++)
            {
                for (int cZ = -range; cZ <= range; cZ++)
                {
                    Vector3Int dest = currentChunk + new Vector3Int(cX, 0, cZ);

                    // Profiler: ShouldPrepareData 51.33% of BuildTasks()
                    if (ShouldPrepareData(dest))
                    {
                        // Profiler: 21.16% of BuildTasks()
#if PROFILE
                    UnityEngine.Profiling.Profiler.BeginSample("chunks.TryGetValue / Generation");
#endif
                        Chunk chk;
                        if (!chunks.TryGetValue(dest, out chk))
                        {
                            for(int cY = 0; cY < worldHeight; cY++)
                            {
                                chk = CreateChunk(dest + Vector3Int.up * cY);
                            }
                        }
#if PROFILE
                    UnityEngine.Profiling.Profiler.EndSample();
#endif

                        // Let the chunk populate itself if the chunk is not prepared
                        if (!chk.prepared && !chk.populating)
                        {
                            SetColumn(dest.x, dest.z);
                        }
                    }

                    if ((Time.realtimeSinceStartup - startTime) > (budgetMS / 1000.0f))
                    {
                        yield return null;
                    }
                }
            }

            currentWorldUpdateStage = WorldUpdateStage.REFRESH_RENDERABLES;
        }
    }
}