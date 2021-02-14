using UnityEngine;
using System.Collections;

public abstract class ChunkGenerator
{
    protected uint GetID(int r, int g, int b, int a)
    {
        return (((uint)r) << 24) + (((uint)g) << 16) + (((uint)b) << 8) + ((uint)a);
    }

    protected uint GetID(Color color)
    {
        return (((uint)(color.r * 255.0f)) << 24) + (((uint)(color.g * 255.0f)) << 16) + (((uint)(color.b * 255.0f)) << 8) + ((uint)(color.a * 255.0f));
    }

    public abstract bool Generate(Chunk chunk, World world);
}

// A Generator that utilizes compute shaders.
public class CSGenerator : ChunkGenerator
{
    public override bool Generate(Chunk chunk, World world)
    {
        // Geometry pass
        if(chunk._geometry_pass_ok)
        {
            StructureSeedPopulationPass.DoChunk(chunk, world);
        }
        else
        {
            CustomJobs.CustomJob.TryAddJob(new WorldGen.GeometryIndependentPass(chunk, world), (CustomJobs.CustomJob job) =>
            {
                StructureSeedPopulationPass.DoChunk(chunk, world);
            });

        }

        // TODO: structures that intersects with chunk

        return false;
    }
}
