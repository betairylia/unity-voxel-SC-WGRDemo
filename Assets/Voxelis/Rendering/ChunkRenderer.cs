using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Voxelis.Rendering
{
    public class ChunkRenderer : ChunkRenderableBase
    {
        public static ComputeShader cs_chunkMeshPopulator;
        public static Material chunkMat;

        public Chunk chunk;
        public ComputeBuffer buffer, buffer_bak, indBuffer, inputBuffer;
        public MaterialPropertyBlock matProp;

        bool waiting = false;

        uint[] _ind;
        public uint vCount { get; private set; }

        protected Vector3Int myPos;

        public bool populated;

        public struct Vertex
        {
            Vector3 position;
            Vector3 normal;
            Vector2 uv;
            uint id;
        }

        // Start is called before the first frame update
        public ChunkRenderer()
        {
            populated = false;
            vCount = 0;
        }

        public override void Init(BlockGroup group, Chunk chunk)
        {
            this.chunk = chunk;

            // Duplicate the material
            // chunkMat = new Material(chunkMat);
            matProp = new MaterialPropertyBlock();

            // Draw it by invoking the CS

            indBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

            inputBuffer = new ComputeBuffer(this.chunk.blockData.Length, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Block)));

            // Update my initial position
            position = chunk.centerPos;
            renderPosition = chunk.positionOffset;

            //GenerateMesh(chunk);
        }

        public override void GenerateGeometry(BlockGroup group, Chunk chunk)
        {
            if (waiting) { return; }
            waiting = true;

            _ind = new uint[] { 0, 1, 0, 0, 0 };
            indBuffer.SetData(_ind);

            inputBuffer.SetData(this.chunk.blockData);

            // Set buffers for I/O
            cs_chunkMeshPopulator.SetBuffer(1, "indirectBuffer", indBuffer);
            cs_chunkMeshPopulator.SetBuffer(1, "chunkData", inputBuffer);

            // Get chunk vert count
            cs_chunkMeshPopulator.Dispatch(1, 32 / 8, 32 / 8, 32 / 8);
            indBuffer.GetData(_ind);

            int allocSize = (int)(_ind[0] * 1.25) + 1024;
            //int allocSize = 65536;
            //_ind[0] = 65536;

            // Need to extend buffer size
            if (vCount < _ind[0])
            {
                // Realloc
                if (buffer != null)
                {
                    buffer_bak = buffer;
                    matProp.SetBuffer("cs_vbuffer", buffer_bak);
                }

                // 1.0 - scale factor for potentially more blocks
                buffer = new ComputeBuffer(allocSize, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vertex)));
            }
            else if (_ind[0] == 0)
            {
                chunk.dirty = false;
                populated = true;
                waiting = false;
                return;
            }

            vCount = (uint)allocSize;
            _ind[0] = 0;
            indBuffer.SetData(_ind);

            buffer.SetCounterValue(0);
            cs_chunkMeshPopulator.SetBuffer(0, "vertexBuffer", buffer);
            cs_chunkMeshPopulator.SetBuffer(0, "indirectBuffer", indBuffer);
            cs_chunkMeshPopulator.SetBuffer(0, "chunkData", inputBuffer);

            // Invoke it
            cs_chunkMeshPopulator.Dispatch(0, 32 / 8, 32 / 8, 32 / 8);

            // Set an sync fence
            GPUDispatchManager.Singleton.AppendTask(this);

            //Vertex[] output = new Vertex[262144];
            //buffer.GetData(output);
            //indBuffer.GetData(_ind);

            //Debug.Log("CS finished with " + _ind[0] + " vertices");
            //Debug.Log(_ind);

            // Update my position
            position = chunk.centerPos;
            renderPosition = chunk.positionOffset;
            bound = new Bounds(chunk.centerPos, Vector3.one * 32.0f);

            chunk.dirty = false;
        }

        public virtual void OnBufferReady()
        {
            // Release input buffer
            // TODO: keep it in a pool ?
            // inputBuffer.Release();
            // inputBuffer = null;

            // Set material parameters
            matProp.SetBuffer("cs_vbuffer", buffer);

            // Release previous buffer
            if (buffer_bak != null)
            {
                buffer_bak.Dispose();
            }

            populated = true;
            waiting = false;
        }

        public override void Clean()
        {
            if (buffer != null)
            {
                buffer.Dispose();
                //buffer = null;
                //Debug.Log("Main buffer released.");
            }

            if (indBuffer != null)
            {
                indBuffer.Dispose();
                //indBuffer = null;
            }

            if (inputBuffer != null)
            {
                inputBuffer.Dispose();
                //inputBuffer = null;
            }

            populated = false;
            chunk.renderer = null;
        }

        public override void Render(BlockGroup group)
        {
            var mat = Matrix4x4.TRS(group.transform.position + renderPosition, group.transform.rotation, group.transform.lossyScale);
            matProp.SetMatrix("_LocalToWorld", mat);
            matProp.SetMatrix("_WorldToLocal", mat.inverse);

            Graphics.DrawProceduralIndirect(
                chunkMat,
                new Bounds(group.transform.position + position, group.transform.lossyScale * 32),
                MeshTopology.Triangles,
                indBuffer, 0, null, matProp);
        }

        public override uint GetVertCount()
        {
            return vCount;
        }

        public override bool IsReadyForPresent()
        {
            return populated && matProp != null && indBuffer != null && buffer != null;
        }

        public override Chunk GetChunk()
        {
            return chunk;
        }

        ~ChunkRenderer()
        {
            Clean();
            if (chunk != null)
            {
                chunk.renderer = null;
            }
        }
    }
}
