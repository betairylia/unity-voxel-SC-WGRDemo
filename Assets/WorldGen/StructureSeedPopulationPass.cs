#define PROFILE

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Voxelis.WorldGen
{
    public static class StructureSeedPopulationPass
    {
        // TODO: resolve multiple dependencies
        public static void DoSeed(Chunk chunk, World world, WorldGen.StructureSeedDescriptor structureSeed, CustomJobs.CustomJob since = null, CustomJobs.CustomJob then = null, FastNoiseLite noise = null)
        {
            BoundsInt b;
            IStructureGenerator sg = null;

            // Use graph if any
            if (world.structureGraphs[(int)structureSeed.structureType] != null)
            {
                b = world.structureGraphs[(int)structureSeed.structureType].GetBounds(structureSeed.worldPos);
                sg = world.structureGraphs[(int)structureSeed.structureType].NewGenerator();
            }
            else
            {
                #region Fallbacks

                // Special case, load test graph
                if (structureSeed.structureType == WorldGen.StructureType.Matryoshka)
                {
#if PROFILE
                UnityEngine.Profiling.Profiler.BeginSample("StructureSeedPopulationPass: Matryoshka");
#endif
                    // Calculate bounds and schedule the job
                    b = world.matryoshkaGraph.GetBounds(structureSeed.worldPos);
                    sg = world.matryoshkaGraph.NewGenerator();

                    // Run on main thread
                    // world.matryoshkaGraph.NewGenerator().Generate(b, world);
#if PROFILE
                UnityEngine.Profiling.Profiler.EndSample();
#endif
                    return;
                }

                b = new BoundsInt(structureSeed.worldPos - WorldGen.Consts.structureSeedGenerationSizes[(int)structureSeed.structureType].min, WorldGen.Consts.structureSeedGenerationSizes[(int)structureSeed.structureType].size);

                switch (structureSeed.structureType)
                {
                    // TODO: Use following pipeline instead:
                    // Seed -> Primitives (readonly to chunks) -> Apply primitives (SingleChunkJob, Worker / GPU)
                    case WorldGen.StructureType.Sphere:
                        sg = new UglySphere();
                        break;
                    case WorldGen.StructureType.Tree:
                        sg = new TestTree(1, 0.5f);
                        break;
                    case WorldGen.StructureType.HangMushroom:
                        // For test purpose, a single MultipleChunkJob is used first.
                        sg = new HangMushroom_test();
                        break;

                    // Moonlight Forest
                    case WorldGen.StructureType.MoonForestGiantTree:
                        sg = new TestTree(3, 1.0f)
                                .SetGrowth(UnityEngine.Random.Range(100.0f, 140.0f))
                                .SetShape(2.35f, 1.3f, 2.1f, 0.5f)
                                .SetColors(0x76573cff, 0x76573cff, 0x96c78cff)
                                .SetLeaf(new TestTree.LeafRenderingSetup()
                                {
                                    isNoisy = true,
                                    noise = noise,
                                    leaf = 0x317d18ff,
                                    leafVariant = 0x51991aff,
                                    variantRate = 0.3f,
                                    noiseScale = 5.0f
                                });
                        break;
                    case WorldGen.StructureType.MoonForestTree:
                        sg = new TestTree(1, 1.0f)
                                .SetShape(1.75f, 1.0f, 1.35f, 0.15f)
                                .SetGrowth(UnityEngine.Random.Range(24.0f, 64.0f))
                                .SetColors(0x584a4dff, 0x584a4dff, 0x96c78cff);
                        break;
                    case WorldGen.StructureType.MoonFlower:
                        sg = new UglySphere();
                        // TODO
                        break;
                    case WorldGen.StructureType.MoonFlowerVine:
                        // TODO : just for test
                        GameObject obj = new GameObject();
                        Light light = obj.AddComponent<Light>();
                        light.type = LightType.Point;
                        light.shadows = LightShadows.None;
                        light.transform.position = b.center;
                        break;

                    default:
                        break;
                }

                #endregion
            }

            if (sg != null)
            {
                var job = new WorldGen.GenericStructureGeneration(world, sg, b);

                // resolve dependencies and schedule the job
                if (since != null)
                {
                    job.Depends(since);
                }
                job = (WorldGen.GenericStructureGeneration)CustomJobs.CustomJob.TryAddJob(job);
                then?.Depends(job);
            }

            return;
        }

        // TODO: Move this into a Job and let structures depends on other chunk's prior structures Orz
        public static void DoChunk(Chunk chunk, World world)
        {
            if (chunk._structures_started) { return; }
            chunk._structures_started = true;

            FastNoiseLite noise = new FastNoiseLite();
            noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(3);
            noise.SetFrequency(0.1f);

            // Collect all jobs ordered by priorities
            SortedList<int, List<WorldGen.StructureSeedDescriptor>> prioredSeeds = new SortedList<int, List<WorldGen.StructureSeedDescriptor>>();
            foreach (var structureSeed in chunk.structureDescriptors)
            {
                int p = WorldGen.Consts.structureSeedGenerationPriority[(int)structureSeed.structureType];
                if (!prioredSeeds.ContainsKey(p))
                {
                    prioredSeeds.Add(p, new List<WorldGen.StructureSeedDescriptor>());
                }

                prioredSeeds[p].Add(structureSeed);
            }

            CustomJobs.CustomJob prevPrior = null;

            // Run seed tasks with dependencies between priorities
            foreach (var priority in prioredSeeds)
            {
                // Cache current wrapper and create a new wrapper represents the dependency of current priority
                CustomJobs.CustomJob currentPrior = new CustomJobs.JobWrapper(() => { });

                // Assign all jobs to current wrapper
                foreach (var structureSeed in priority.Value)
                {
                    DoSeed(chunk, world, structureSeed, prevPrior, currentPrior, noise);
                }

                // Schedule current jobs
                prevPrior = CustomJobs.CustomJob.TryAddJob(currentPrior);
            }

            // Finally we notify the chunk that all structures have been done.
            // wrapper now depends on final priority's jobs
            if (prevPrior != null)
            {
                CustomJobs.JobWrapper wrapper = new CustomJobs.JobWrapper(() => { chunk._structures_ok = true; });
                wrapper.Depends(prevPrior);
                CustomJobs.CustomJob.TryAddJob(wrapper);
            }
            else
            {
                chunk._structures_ok = true;
            }

            //CustomJobs.CustomJob a = new CustomJobs.JobWrapper(() => { });
        }
    }
}
