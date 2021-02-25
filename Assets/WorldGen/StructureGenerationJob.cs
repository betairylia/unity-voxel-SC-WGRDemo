using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

namespace Voxelis.WorldGen
{
    // FIXME: Black magic in use
    // see https://qiita.com/tatsunoru/items/611d0378086dc5986249
    public struct GenericStructureGeneratorJobWrapper : IJob
    {
        public GCHandle<IStructureGenerator> generator;
        public GCHandle<World> world;
        public BoundsInt bound;

        public void Execute()
        {
            generator.Target.Generate(bound, world.Target);
        }
    }

    public class GenericStructureGeneration : CustomJobs.MultipleChunkJob
    {
        static HashSet<BoundsInt> cache = new HashSet<BoundsInt>(); // For debug

        IStructureGenerator generator;
        BoundsInt bound;
        World world;

        GenericStructureGeneratorJobWrapper job;

        /// <summary>
        /// Return all chunks within the bound of given world.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="bound"></param>
        /// <returns></returns>
        private static List<Chunk> BoundWorldToChunks(World world, BoundsInt bound)
        {
            // Generate all chunks inside bound
            Vector3Int min = bound.min / 32;
            Vector3Int max = bound.max / 32;
            List<Chunk> result = new List<Chunk>();

            for (int cX = min.x; cX <= max.x; cX++)
            {
                for (int cY = min.y; cY <= max.y; cY++)
                {
                    for (int cZ = min.z; cZ <= max.z; cZ++)
                    {
                        Vector3Int dest = new Vector3Int(cX, cY, cZ);
                        Chunk chk = world.GetChunk(dest, true);
                        result.Add(chk);
                    }
                }
            }

            return result;
        }

        public GenericStructureGeneration(World world, IStructureGenerator generator, BoundsInt bound) : base (BoundWorldToChunks(world, bound))
        {
            this.generator = generator;
            this.bound = bound;
            this.world = world;

            //Debug.LogError($"StructureJob duplication check in use, heavy");
            //if(cache.Contains(bound))
            //{
            //    Debug.LogWarning($"Exact same structure populated at {bound}");
            //}
            cache.Add(bound);

            isUnique = false;
        }

        public override void InitJob()
        {
            // World gen Structures must be generated after geometry passes
            // TODO: Performance heavy; see https://stackoverflow.com/questions/18552669/memory-allocation-when-using-foreach-loops-in-c-sharp
            foreach (var chunk in chunks)
            {
                if(chunk == null)
                {
                    Debug.Log("Catched");
                    continue;
                }
                // TODO: Pool this ...
                this.Depends(TryAddJob(new GeometryIndependentPass(chunk, world)));
            }
        }

        public override string ToString()
        {
            return $"Structure at: {bound.ToString()}";
        }

        protected override void OnExecute()
        {
            job = new GenericStructureGeneratorJobWrapper()
            {
                bound = bound
            };
            job.generator.Create(generator);
            job.world.Create(world);

            jobHandle = job.Schedule();
        }

        protected override void OnFinish()
        {
            base.OnFinish();

            job.generator.Dispose();
            job.world.Dispose();
        }
    }
}
