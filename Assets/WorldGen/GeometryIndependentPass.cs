using UnityEngine;
using System.Collections;
using Unity.Jobs;
using System.Collections.Generic;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

namespace Voxelis.WorldGen
{
    // TODO: generating independent blockGroups
    public class GeometryIndependentPass : CustomJobs.SingleChunkGPUJob
    {
        World world;
        int hash;

        public GeometryIndependentPass(Chunk chunk, World world) : base(chunk)
        {
            this.world = world;
            this.hash = 0x00000000 ^ chunk.positionOffset.x ^ chunk.positionOffset.y ^ chunk.positionOffset.z;
        }

        public virtual void OnGPUFinish(Unity.Collections.NativeArray<Block> buf)
        {
            chunk._geometry_pass_ok = true;
            base.OnGPUFinish();
            chunk.GetCSGeneration(buf);
        }

        protected override bool AlreadyFinished()
        {
            return chunk._geometry_pass_ok;
        }

        public override string ToString()
        {
            return $"GeometryPass:[{chunk.positionOffset.x / 32}, {chunk.positionOffset.y / 32}, {chunk.positionOffset.z / 32}]";
        }

        public override int GetHashCode()
        {
            return hash;
        }

        public override bool Equals(object obj)
        {
            return chunk == ((GeometryIndependentPass)obj).chunk;
        }

        protected override void OnExecute()
        {
            GeometryIndependentPass.Submit(this);
        }

        /************ Static update methods ************/

        static LinkedList<GeometryIndependentPass> CSGenerationQueue;
        static GeometryIndependentPass[] CSGenerationQueue_waitForReadback;
        static public ComputeShader cs_generation;
        static public int cs_generation_batchsize = 512;
        static World def_world;

        static bool inited = false;

        const int MAX_STRUCTURES_PER_CHUNK = 256;

        static ComputeBuffer csgen_chkBuf, csgen_blkBuf, csgen_structureBuf, csgen_structureCountBuf;
        static int[] host_csgen_chkBuf;
        static uint host_csgen_structureCount;

        static bool csgen_readingBack = false;

        static bool blkBufOK = false, strBufOK = false, strlenBufOK = false;

        static StructureSeedDescriptor[] host_descriptors = new StructureSeedDescriptor[cs_generation_batchsize * MAX_STRUCTURES_PER_CHUNK];

        /// <summary>
        /// Submit a pass for batched GPU exec.
        /// </summary>
        /// <param name="pass"></param>
        static void Submit(GeometryIndependentPass pass)
        {
            CSGenerationQueue.AddLast(pass);
        }

        public static void SetWorld(World world)
        {
            Init();
            def_world = world;

            if(world.sketchMapTex != null)
            {
                cs_generation.SetTexture(0, "Sketch", world.sketchMapTex);
            }
        }

        public static void Init()
        {
            if(inited) { return; }
            inited = true;

            CSGenerationQueue = new LinkedList<GeometryIndependentPass>();

            csgen_chkBuf = new ComputeBuffer(cs_generation_batchsize * 3, sizeof(int)); // x, y, z for each chunk
            csgen_blkBuf = new ComputeBuffer(cs_generation_batchsize * 32768, Marshal.SizeOf(typeof(Block))); // every block
            csgen_structureBuf = new ComputeBuffer(cs_generation_batchsize * MAX_STRUCTURES_PER_CHUNK, System.Runtime.InteropServices.Marshal.SizeOf(typeof(StructureSeedDescriptor)), ComputeBufferType.Append); // structure "seeds" slot for all chunks
            csgen_structureCountBuf = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);

            cs_generation.SetBuffer(0, "chkBuf", csgen_chkBuf);
            cs_generation.SetBuffer(0, "blkBuf", csgen_blkBuf);
            cs_generation.SetBuffer(0, "structureBuf", csgen_structureBuf);

            blkBufOK = false;
            strBufOK = false;
            strlenBufOK = false;

            //host_csgen_blkBuf = new uint[cs_generation_batchsize * 32768];
            host_csgen_chkBuf = new int[cs_generation_batchsize * 3];
        }

        public static void Destroy()
        {
            csgen_chkBuf.Dispose();
            csgen_blkBuf.Dispose();
            csgen_structureBuf.Dispose();
            csgen_structureCountBuf.Dispose();
        }

        /// <summary>
        /// Handles batched GPU exec.
        /// </summary>
        public static void Update()
        {
            // Not Empty and not reading back stuffs
            if ((!(CSGenerationQueue.First == null)) && (!csgen_readingBack))
            {
                int _count = 0;
                var p = CSGenerationQueue.First;
                CSGenerationQueue_waitForReadback = new GeometryIndependentPass[cs_generation_batchsize];

                while (p != null && _count < cs_generation_batchsize)
                {
                    host_csgen_chkBuf[_count * 3 + 0] = p.Value.chunk.positionOffset.x;
                    host_csgen_chkBuf[_count * 3 + 1] = p.Value.chunk.positionOffset.y;
                    host_csgen_chkBuf[_count * 3 + 2] = p.Value.chunk.positionOffset.z;

                    CSGenerationQueue_waitForReadback[_count] = p.Value;

                    _count += 1;

                    var _p = p;
                    p = p.Next;
                    CSGenerationQueue.Remove(_p);
                }

                // Upload buffers to GPU
                csgen_chkBuf.SetData(host_csgen_chkBuf);
                csgen_structureBuf.SetCounterValue(0);

                // Run
                cs_generation.Dispatch(0, 4 * _count, 4, 4);
                //cs_generation.Dispatch(0, 4 * cs_generation_batchsize, 4, 4);
                ComputeBuffer.CopyCount(csgen_structureBuf, csgen_structureCountBuf, 0);

                // Set flags for reading back
                blkBufOK = false;
                strBufOK = false;
                strlenBufOK = false;
                csgen_readingBack = true;

                // Wait for readback
                UnityEngine.Rendering.AsyncGPUReadback.Request(csgen_blkBuf, batched_OnGPUComplete);
                UnityEngine.Rendering.AsyncGPUReadback.Request(csgen_structureBuf, batched_OnGPUComplete_structure);
                UnityEngine.Rendering.AsyncGPUReadback.Request(csgen_structureCountBuf, batched_OnGPUComplete_structureCount);
            }
        }

        static void batched_OnGPUComplete(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.Log("GPU readback error detected.");
                return;
            }

            var result = request.GetData<Block>();
            for (int i = 0; i < cs_generation_batchsize; i++)
            {
                if (CSGenerationQueue_waitForReadback[i] == null) { continue; }

                var chkResult = result.GetSubArray(32768 * i, 32768);
                CSGenerationQueue_waitForReadback[i].OnGPUFinish(chkResult);
            }

            blkBufOK = true;
            if (blkBufOK && strBufOK && strlenBufOK)
            {
                HandleStructures();
                csgen_readingBack = false;
            }
        }

        static void batched_OnGPUComplete_structure(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.Log("GPU readback error detected.");
                return;
            }

            request.GetData<StructureSeedDescriptor>().CopyTo(host_descriptors);
            strBufOK = true;

            if (blkBufOK && strBufOK && strlenBufOK)
            {
                HandleStructures();
                csgen_readingBack = false;
            }
        }

        static void batched_OnGPUComplete_structureCount(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
            {
                Debug.Log("GPU readback error detected.");
                return;
            }

            //Debug.Log($"Structure length: {request.GetData<uint>()[0]}");
            host_csgen_structureCount = request.GetData<uint>()[0];

            strlenBufOK = true;
            if (blkBufOK && strBufOK && strlenBufOK)
            {
                HandleStructures();
                csgen_readingBack = false;
            }
        }

        static void HandleStructures()
        {
            Debug.Log($"HandleStructures();");

            // Push structure seeds into chunk
            for(int i = 0; i < host_csgen_structureCount; i++)
            {
                var sd = host_descriptors[i];

                if (sd.worldPos == Vector3Int.zero)
                {
                    continue;
                }

                Vector3Int inner_pos;
                Vector3Int chkCoord = def_world.GetChunkCoord(sd.worldPos, out inner_pos);
                def_world.GetChunk(chkCoord, false)?.AddStructureSeed(sd);

                //if(sd.structureType == StructureType.HangMushroom)
                //{
                //    GameObject.FindObjectOfType<World>().CreateStructure(new BoundsInt(sd.worldPos, Vector3Int.one * 96), new TestTree());
                //}
            }
        }
    }
}
