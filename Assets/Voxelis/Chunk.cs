using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Voxelis.Rendering;

// Representing the underlying data of a chunk, no LOD.
namespace Voxelis
{
    public class Chunk : IDisposable
    {
        // Data arrays
        public Unity.Collections.NativeArray<Block> blockData = new Unity.Collections.NativeArray<Block>(32768, Unity.Collections.Allocator.Persistent); 
        //public Unity.Collections.NativeArray<EnviormentBlock> envData = new Unity.Collections.NativeArray<EnviormentBlock>(32768, Unity.Collections.Allocator.Persistent);

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

        public ChunkRenderableBase renderer
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
            blockData.Dispose();
        }

        public void _PopulateStart(Vector3Int myPos)
        {
            populating = true;
            this.positionOffset = myPos;
        }

        public void _PopulateFinish()
        {
            _geometry_pass_ok = true;
            _geometry_pass_started = true;
            _structures_ok = true;
            _structures_started = true;
            dirty = true;
        }

        public void GetCSGeneration(Unity.Collections.NativeArray<Block> buf)
        {
            buf.CopyTo(this.blockData);
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

        public void SetBlock(int x, int y, int z, ushort id)
        {
            SetBlock(x, y, z, Block.FromID(id));
        }

        public void SetBlock(int x, int y, int z, Block blk)
        {
            dirty = true;
            if (x * 32 * 32 + y * 32 + z < 0 || x * 32 * 32 + y * 32 + z > 32767)
            {
                Debug.LogError("!");
            }
            blockData[x * 32 * 32 + y * 32 + z] = blk;
        }

        public Block GetBlock(int x, int y, int z)
        {
            return blockData[x * 32 * 32 + y * 32 + z];
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
}
