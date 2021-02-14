using UnityEngine;
using System.Collections;

public static class StructureSeedPopulationPass
{
    public static void DoChunk(Chunk chunk, World world)
    {
        if (chunk._structures_started) { return; }
        chunk._structures_started = true;

        CustomJobs.JobWrapper wrapper = new CustomJobs.JobWrapper(() => { chunk._structures_ok = true; });

        FastNoiseLite noise = new FastNoiseLite();
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2S);
        noise.SetFractalType(FastNoiseLite.FractalType.FBm);
        noise.SetFractalOctaves(3);
        noise.SetFrequency(0.1f);

        foreach (var structureSeed in chunk.structureDescriptors)
        {
            // Use graph if any
            if(world.structureGraphs[(int)structureSeed.structureType] != null)
            {
                BoundsInt b = world.structureGraphs[(int)structureSeed.structureType].GetBounds(structureSeed.worldPos);
                wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, world.structureGraphs[(int)structureSeed.structureType].NewGenerator(), b)));
                continue;
            }

            #region Fallbacks

            // Special case, load test graph
            if (structureSeed.structureType == WorldGen.StructureType.Matryoshka)
            {
#if PROFILE
                UnityEngine.Profiling.Profiler.BeginSample("Matryoshka");
#endif
                // Calculate bounds and schedule the job
                BoundsInt b = world.matryoshkaGraph.GetBounds(structureSeed.worldPos);
                wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, world.matryoshkaGraph.NewGenerator(), b)));
                
                // Run on main thread
                // world.matryoshkaGraph.NewGenerator().Generate(b, world);
#if PROFILE
                UnityEngine.Profiling.Profiler.EndSample();
#endif
                continue;
            }

            BoundsInt seedBound = new BoundsInt(structureSeed.worldPos - WorldGen.Consts.structureSeedGenerationSizes[(int)structureSeed.structureType].min, WorldGen.Consts.structureSeedGenerationSizes[(int)structureSeed.structureType].size);

            switch (structureSeed.structureType)
            {
                // TODO: Use following pipeline instead:
                // Seed -> Primitives (readonly to chunks) -> Apply primitives (SingleChunkJob, Worker / GPU)
                case WorldGen.StructureType.Sphere:
                    wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, new UglySphere(), seedBound)));
                    break;
                case WorldGen.StructureType.Tree:
                    wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, new TestTree(1, 0.5f), seedBound)));
                    break;
                case WorldGen.StructureType.HangMushroom:
                    // For test purpose, a single MultipleChunkJob is used first.
                    wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, new HangMushroom_test(), seedBound)));
                    break;

                // Moonlight Forest
                case WorldGen.StructureType.MoonForestGiantTree:
                    wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(
                        world,
                        new TestTree(3, 1.0f)
                            .SetGrowth(UnityEngine.Random.Range(100.0f, 140.0f))
                            .SetShape(2.35f, 1.3f, 2.1f, 0.5f)
                            .SetColors(0x76573cff, 0x76573cff, 0x96c78cff)
                            .SetLeaf(new TestTree.LeafRenderingSetup() {
                                isNoisy = true,
                                noise = noise,
                                leaf = 0x317d18ff,
                                leafVariant = 0x51991aff,
                                variantRate = 0.3f,
                                noiseScale = 5.0f
                            }),
                        seedBound
                    )));
                    break;
                case WorldGen.StructureType.MoonForestTree:
                    wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(
                        world,
                        new TestTree(1, 1.0f)
                            .SetShape(1.75f, 1.0f, 1.35f, 0.15f)
                            .SetGrowth(UnityEngine.Random.Range(24.0f, 64.0f))
                            .SetColors(0x584a4dff, 0x584a4dff, 0x96c78cff),
                        seedBound
                    )));
                    break;
                case WorldGen.StructureType.MoonFlower:
                    wrapper.Depends(CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, new UglySphere(), seedBound)));
                    // TODO
                    break;
                case WorldGen.StructureType.MoonLightbulb:
                    // TODO : just for test
                    GameObject obj = new GameObject();
                    Light light = obj.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.shadows = LightShadows.None;
                    light.transform.position = seedBound.center;
                    break;

                default:
                    break;
            }

            #endregion
        }

        // Finally we notify the chunk that all structures have been done.
        CustomJobs.CustomJob.TryAddJob(wrapper);
    }
}
