using UnityEngine;
using System.Collections;

namespace Voxelis
{
    public interface IStructureGenerator
    {
        void Generate(BoundsInt bound, World world);
    }

    public class UglySphere : IStructureGenerator
    {
        Block blk;

        public UglySphere()
        {
            this.blk = Block.From32bitColor((uint)Blocks.grass);
        }

        public UglySphere(Block blk)
        {
            this.blk = blk;
        }

        public void Generate(BoundsInt bound, World world)
        {
            Vector3 center = bound.center;
            for (int x = bound.min.x; x <= bound.max.x; x++)
            {
                for (int y = bound.min.y; y <= bound.max.y; y++)
                {
                    for (int z = bound.min.z; z <= bound.max.z; z++)
                    {
                        Vector3Int pos = new Vector3Int(x, y, z);
                        if ((pos - center).magnitude < (bound.max.x - bound.min.x) / 2.0f)
                        {
                            world.SetBlock(pos, blk);
                        }
                    }
                }
            }
        }
    }
}
