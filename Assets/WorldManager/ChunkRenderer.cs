using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkRenderer
{
    public Chunk chunk;
    public ComputeShader cs_chunkMeshPopulator;
    public ComputeBuffer buffer, buffer_bak, indBuffer, inputBuffer;
    public Material chunkMat;
    public MaterialPropertyBlock matProp;

    bool waiting = false;

    uint[] _ind;
    public uint vCount { get; private set; }

    protected Vector3Int myPos;
    public Bounds bound
    {
        get;
        private set;
    }

    public bool populated;

    [HideInInspector]
    public Vector3 position, renderPosition;

    public struct Vertex
    {
        Vector3 position;
        Vector3 normal;
        uint id;
    }

    // Start is called before the first frame update
    public ChunkRenderer()
    {
        populated = false;
        vCount = 0;
    }

    public virtual void Init(Chunk chunk)
    {
        this.chunk = chunk;

        // Duplicate the material
        // chunkMat = new Material(chunkMat);
        matProp = new MaterialPropertyBlock();

        // Draw it by invoking the CS

        indBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

        inputBuffer = new ComputeBuffer(this.chunk.chunkData.Length, sizeof(int));

        // Update my initial position
        position = chunk.centerPos;
        renderPosition = chunk.positionOffset;

        GenerateMesh(chunk);
    }

    public virtual void GenerateMesh(Chunk chunk)
    {
        if (waiting) { return; }
        waiting = true;

        _ind = new uint[] { 0, 1, 0, 0, 0 };
        indBuffer.SetData(_ind);

        inputBuffer.SetData(this.chunk.chunkData);

        // Set buffers for I/O
        cs_chunkMeshPopulator.SetBuffer(1, "indirectBuffer", indBuffer);
        cs_chunkMeshPopulator.SetBuffer(1, "chunkData", inputBuffer);

        // Get chunk vert count
        cs_chunkMeshPopulator.Dispatch(1, 32 / 8, 32 / 8, 32 / 8);
        indBuffer.GetData(_ind);

        int allocSize = (int)(_ind[0] * 1.0) + 1024;

        // Need to extend buffer size
        if (vCount < _ind[0])
        {
            // Realloc
            if(buffer != null)
            {
                buffer_bak = buffer;
                matProp.SetBuffer("cs_vbuffer", buffer_bak);
            }

            // 1.0 - scale factor for potentially more blocks
            buffer = new ComputeBuffer(allocSize, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vertex)));
        }
        else if(_ind[0] == 0)
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
        //inputBuffer.Release();
        //inputBuffer = null;

        // Set material parameters
        matProp.SetBuffer("cs_vbuffer", buffer);

        // Release previous buffer
        if(buffer_bak != null)
        {
            buffer_bak.Dispose();
        }
     
        populated = true;
        waiting = false;
    }

    public void Clean()
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

        if(inputBuffer != null)
        {
            inputBuffer.Dispose();
            //inputBuffer = null;
        }

        populated = false;
        chunk.renderer = null;
    }

    ~ChunkRenderer()
    {
        Clean();
        if(chunk != null)
        {
            chunk.renderer = null;
        }
    }
}
