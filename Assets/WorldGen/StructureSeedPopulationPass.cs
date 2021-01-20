using UnityEngine;
using System.Collections;

public static class StructureSeedPopulationPass
{
    public static void DoChunk(Chunk chunk, World world)
    {
        foreach (var structureSeed in chunk.structureDescriptors)
        {
            BoundsInt seedBound = new BoundsInt(structureSeed.worldPos - WorldGen.Consts.structureSeedGenerationSizes[(int)structureSeed.structureType].min, WorldGen.Consts.structureSeedGenerationSizes[(int)structureSeed.structureType].size);

            switch (structureSeed.structureType)
            {
                // TODO: Use following pipeline instead:
                // Seed -> Primitives (readonly to chunks) -> Apply primitives (SingleChunkJob, Worker / GPU)
                case WorldGen.StructureType.Sphere:
                    CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, new UglySphere(), seedBound));
                    break;
                case WorldGen.StructureType.Tree:
                    CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, new TestTree(1, 0.5f), seedBound));
                    break;
                case WorldGen.StructureType.HangMushroom:
                    // For test purpose, a single MultipleChunkJob is used first.
                    CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(world, new HangMushroom_test(), seedBound));
                    break;
                default:
                    break;
            }
        }
    }
}
