using UnityEngine;
using System.Collections;

namespace WorldGen
{
    public struct StructureSeedDescriptor
    {
        public StructureType structureType;
        public Vector3Int worldPos;
    }

    public enum StructureType
    {
        Sphere = 0,
        Tree = 1,
        HangMushroom = 2,
    }

    public static class Consts
    {
        public static readonly BoundsInt[] structureSeedGenerationSizes =
        {
            // Center Pos: position of seed (seed.worldPos) in the bound that it may access (requires geometry indep. pass)
            // Total Size: size of the bound it may access
            //            Center Pos||Total Size
            //             x   y   z|| x   y   z
            new BoundsInt( 2,  2,  2,  5,  5,  5), // Sphere
            new BoundsInt(15,  5, 15, 31, 48, 31), // Tree
            new BoundsInt( 2, 19,  2,  5, 19,  5), // Hang Mushroom
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
        };
    }
}
