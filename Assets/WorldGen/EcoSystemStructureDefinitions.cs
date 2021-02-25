using UnityEngine;
using System.Collections;

namespace Voxelis.WorldGen
{
    public struct StructureSeedDescriptor
    {
        public StructureType structureType; //enum
        public Vector3Int worldPos;
    }

    public enum StructureType
    {
        Matryoshka = -1,
        
        Sphere = 0,
        Tree = 1,
        HangMushroom = 2,

        // Moonlight Forest
        MoonForestGiantTree = 3,
        MoonForestTree = 4,
        MoonFlower = 5,
        MoonFlowerVine = 6,
    }

    public static class Consts
    {
        public static readonly BoundsInt testBounds = new BoundsInt(5, 5, 5, 11, 11, 11);

        public static readonly BoundsInt[] structureSeedGenerationSizes =
        {
            // Center Pos: position of seed (seed.worldPos) in the bound that it may access (requires geometry indep. pass)
            // Total Size: size of the bound it may access
            //             Center Pos||Total Size
            //              x   y   z|| x   y   z
            new BoundsInt(  2,  2,  2,  5,  5,  5), // Sphere
            new BoundsInt( 15,  5, 15, 31, 48, 31), // Tree
            new BoundsInt(  2, 19,  2,  5, 19,  5), // Hang Mushroom

            new BoundsInt( 70,  5, 70,140,256,140), // Moonlight forest giant tree
            new BoundsInt( 15,  5, 15, 31, 63, 31), // Moonlight forest tree
            new BoundsInt(), // Moonlight forest flower bush
            new BoundsInt(  0,  0,  0,  0,  0,  0), // Moonlight forest lightbulb
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
            new BoundsInt(), // 
        };

        // Structures with larger numbers will be generated after all structures with smaller numbers (single chunk ...).
        public static readonly int[] structureSeedGenerationPriority =
        {
            0, // Sphere
            0, // Tree
            0, // Hang Mushroom

            0, // Moonlight forest giant tree
            0, // Moonlight forest tree
            1, // Moonlight forest flower bush
            2, // Moonlight forest flower vine
        };
    }
}
