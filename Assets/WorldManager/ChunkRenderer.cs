using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChunkRenderer
{
    public Chunk chunk;
    public ComputeShader cs_chunkMeshPopulator;
    public ComputeBuffer buffer, indBuffer, inputBuffer;
    public Material chunkMat;
    public MaterialPropertyBlock matProp;

    public int[] _ind;

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
    }

    public virtual void Init(Chunk chunk)
    {
        this.chunk = chunk;

        // Duplicate the material
        // chunkMat = new Material(chunkMat);
        matProp = new MaterialPropertyBlock();

        // Draw it by invoking the CS
        buffer = new ComputeBuffer(65536, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vertex)));

        indBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        inputBuffer = new ComputeBuffer(this.chunk.chunkData.Length, sizeof(int));

        // Update my initial position
        position = chunk.centerPos;
        renderPosition = chunk.positionOffset;

        GenerateMesh(chunk);
    }

    public virtual void GenerateMesh(Chunk chunk)
    {
        buffer.SetCounterValue(0);

        _ind = new int[] { 0, 1, 0, 0 };
        indBuffer.SetData(_ind);

        inputBuffer.SetData(this.chunk.chunkData);

        // Set buffers for I/O
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
     
        populated = true;
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
