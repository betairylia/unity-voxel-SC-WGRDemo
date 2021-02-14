using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

// Representing the underlying data of a chunk, no LOD.
public class Chunk : IDisposable
{
    public Unity.Collections.NativeArray<uint> chunkData = new Unity.Collections.NativeArray<uint>(32768, Unity.Collections.Allocator.Persistent);
    public Vector3Int positionOffset;

    public List<WorldGen.StructureSeedDescriptor> structureDescriptors = new List<WorldGen.StructureSeedDescriptor>();

    public bool _geometry_pass_ok = false;
    public bool _geometry_pass_started = false;
    public bool _structures_ok = false;
    public bool _structures_started = false;
    public bool dirty = true;

    public bool populating { get; private set; }

    public CustomJobs.CustomJob lastQueuedWriter;

    public bool prepared
    {
        get { return populated || (_geometry_pass_ok && _structures_ok); }
    }

    public Vector3 centerPos
    {
        get { return positionOffset + Vector3.one * 16; }
    }

    protected bool populated;

    public ChunkRenderer renderer
    {
        get;
        set;
    }
    
    public Chunk()
    {
        populated = false;
        populating = false;
    }

    ~Chunk()
    {
        Cleanup();
    }

    void Cleanup()
    {
        chunkData.Dispose();
    }

    public void Populate(Vector3Int myPos, ChunkGenerator generator, World world)
    {
        populating = true;
        this.positionOffset = myPos;

        populated = generator.Generate(this, world);
        //dirty = true;
    }

    public void GetCSGeneration(Unity.Collections.NativeArray<uint> buf)
    {
        buf.CopyTo(this.chunkData);
        dirty = true;
    }

    public bool isReadyForPresent()
    {
        return prepared;
    }

    public bool hasRenderer()
    {
        return (renderer != null);
    }

    public void SetBlock(int x, int y, int z, uint id)
    {
        dirty = true;
        if(x * 32 * 32 + y * 32 + z < 0 || x * 32 * 32 + y * 32 + z > 32767)
        {
            Debug.LogError("!");
        }
        chunkData[x * 32 * 32 + y * 32 + z] = id;
    }

    public uint GetBlock(int x, int y, int z)
    {
        return chunkData[x * 32 * 32 + y * 32 + z];
    }

    public void Dispose()
    {
        Cleanup();
    }

    public void AddStructureSeed(WorldGen.StructureSeedDescriptor seed)
    {
        //Debug.LogError($"Seed duplication check in use, heavy");
        //if(structureDescriptors.Contains(seed))
        //{
        //    Debug.LogWarning($"Seed already exist: {seed}");
        //}
        structureDescriptors.Add(seed);
    }
}
