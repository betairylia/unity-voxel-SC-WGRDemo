using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Voxelis.WorldGen
{
    // TODO: See StructureSeedPopulationPass.cs - will implement this later ...
    //public class HangMushroomJob : CustomJobs.MultipleChunkJob
    //{
    //    public HangMushroomJob(IEnumerable<Chunk> chunks) : base(chunks)
    //    {

    //    }

    //    protected override void OnExecute()
    //    {
    //        throw new System.NotImplementedException();
    //    }
    //}

    // Test purpose
    public class HangMushroom_test : IStructureGenerator
    {
        public static System.Random random = new System.Random();

        public void Generate(BoundsInt bound, World world)
        {
            Vector3Int root = bound.min + WorldGen.Consts.structureSeedGenerationSizes[(int)WorldGen.StructureType.HangMushroom].min;
            int minLenth = 4;
            int maxLenth = 18;

            for (int length = 1; length <= maxLenth; length++)
            {
                if (world.GetBlock(root + Vector3Int.down * length).IsSolid())
                {
                    maxLenth = length;
                    break;
                }
            }

            // Space too small, go back
            if (maxLenth < minLenth) { return; }

            int len = random.Next(minLenth, maxLenth + 1);
            for (int y = 1; y < len; y++)
            {
                world.SetBlock(root + Vector3Int.down * y, Block.From32bitColor((uint)Blocks.glowingBlue));
            }
        }
    }
}