using UnityEngine;
using System.Collections;

public interface IStructureGenerator
{
    void Generate(BoundsInt bound, World world);
}

public class UglySphere : IStructureGenerator
{
    public void Generate(BoundsInt bound, World world) 
    {
        Vector3 center = bound.center;
        for(int x = bound.min.x; x <= bound.max.x; x++)
        {
            for (int y = bound.min.y; y <= bound.max.y; y++)
            {
                for (int z = bound.min.z; z <= bound.max.z; z++)
                {
                    Vector3Int pos = new Vector3Int(x, y, z);
                    if((pos - center).magnitude < (bound.max.x - bound.min.x) / 2.0f)
                    {
                        world.SetBlock(pos, (uint)Blocks.grass);
                    }
                }
            }
        }
    }
}
