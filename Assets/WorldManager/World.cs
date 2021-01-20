//#define PROFILE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TypeReferences;
using System;
using Unity.Jobs;
using UnityEngine.Rendering;

public class World : MonoBehaviour
{
    static List<World> allGeneators = new List<World>();

    public GameObject chunkRendererPrefab;
    public Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
    public LinkedList<ChunkRenderer> renderables = new LinkedList<ChunkRenderer>();

    public int showDistance = 256, disappearDistance = 360;
    public int worldHeight = 4;
    public float waterHeight = 10.0f;

    [HideInInspector]
    public bool removeChunkInMemory = false;
    public int worldSize = 1024;

    float CoroutineStartTime = 0.0f;
    public float maxCoroutineSpendPerFrameMS = 1.0f;

    public TMPro.TextMeshProUGUI debugText;
    Coroutine worldGeneratingCoroutine;
    bool worldGenerationCRInProgress;

    [Inherits(typeof(ChunkGenerator))]
    public TypeReference generatorType;
    ChunkGenerator chunkGenerator;

    public Material chunkMat;
    public ComputeShader cs_chunkMeshPopulator;

    public Transform follows;
    public Transform groundPlane, capPlane;

    public WorldUpdateManager UpdateMgr;
    public ComputeShader cs_generation;
    public int cs_generation_batchsize = 512;

    [Space]
    public Camera mainCam;
    public bool viewCull = true;
    public bool fog = false;
    public Color fogColor;

    // Start is called before the first frame update
    void Start()
    {
        if(!allGeneators.Contains(this))
        {
            allGeneators.Add(this);
        }

        UpdateMgr = new WorldUpdateManager();

        if(fog)
        {
            RenderSettings.fog = true;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            RenderSettings.fogColor = fogColor;
            mainCam.backgroundColor = fogColor;
        }

        SetWorld();
    }

    void SetWorld()
    {
        // Setup generators
        chunkGenerator = (ChunkGenerator)System.Activator.CreateInstance(generatorType);
        UpdateMgr.Init(cs_generation, cs_generation_batchsize);

        for (int i = 0; i < 0; i++)
        //for (int i = 0; i < 64; i++)
        {
            Vector2Int pos = new Vector2Int(UnityEngine.Random.Range(-800, 800), UnityEngine.Random.Range(-800, 800));
            CreateStructure(new BoundsInt(pos.x, 5, pos.y, 96, 96, 96), new TestTree());
        }
        //CreateStructure(new BoundsInt(-122, 32, 225, 140, 120, 140), new TestTree());
        //CreateStructure(new BoundsInt(56, 10, 371, 140, 120, 140), new TestTree());
        //CreateStructure(new BoundsInt(143, 0, 177, 140, 120, 140), new TestTree());

        worldGeneratingCoroutine = StartCoroutine(WorldUpdateCoroutine());
    }

    // Update is called once per frame
    void Update()
    {
        //if (!worldGenerationCRInProgress)
        //{
        //    worldGeneratingCoroutine = StartCoroutine(WorldUpdateCoroutine());
        //}

        if(fog)
        {
            RenderSettings.fogColor = fogColor;
            mainCam.backgroundColor = fogColor;
        }

        // SUPER HEAVY - FIXME: Optimize it orz
        RefreshRenderables();
        UpdateMgr.Update();
        if (debugText != null)
        {
            debugText.text = $"" +
                $"CHUNKS:\n" +
                $"Loaded:   {chunks.Count} ({chunks.Count * sizeof(uint) * 32 / 1024} MB)\n" +
                $"Rendered: {renderables.Count} ({renderables.Count * System.Runtime.InteropServices.Marshal.SizeOf(typeof(ChunkRenderer.Vertex)) * (65536.0f / 1024.0f / 1024.0f)} MB)\n" +
                $"\n" +
                $"@ {(int)follows.position.x}, {(int)follows.position.y}, {(int)follows.position.z}\n" +
                $"\n" +
                $"FPS: {EMAFPS(1.0f / Time.unscaledDeltaTime).ToString("N1")}\n" +
                $"Render distance: {showDistance} blocks\n" +
                $" (discards from: {disappearDistance} blocks)\n" +
                $"\n" +
                $"Jobs:\n" +
                $"Total  Queued {CustomJobs.CustomJob.Count}\n" +
                $"Unique Queued {CustomJobs.CustomJob.queuedUniqueJobs.Count}\n" +
                $"Scheduled     {CustomJobs.CustomJob.scheduledJobs.Count}";
        }

        CoroutineStartTime = Time.realtimeSinceStartup;

        // Forced fence placement
        GPUDispatchManager.Singleton.CheckTasks();
        GPUDispatchManager.Singleton.SyncTasks();

        groundPlane.position = new Vector3(follows.position.x, waterHeight, follows.position.z);
        capPlane.position = new Vector3(follows.position.x, worldHeight * 32, follows.position.z);
    }

    float avgFPS = 0;
    float EMAFPS(float val)
    {
        float eps = 0.13f;
        avgFPS = eps * val + (1 - eps) * avgFPS;
        return avgFPS;
    }

    private void LateUpdate()
    {
        // I DON'T KNOW WHY BY PUTTING THIS INTO LATE UPDATE WILL NOT CAUSE ANY CRASH. BUT IT WORKS JUST FINE. OKAY BLAME UNITY (*@#^$&
        RenderAll();
    }

    IEnumerator WorldUpdateCoroutine()
    {
        //worldGenerationCRInProgress = true;
        while (true)
        {
            yield return StartCoroutine(BuildTasks());
            //yield return StartCoroutine();
            //worldGenerationCRInProgress = false;
            yield return null;
        }
    }

    private void RenderAll()
    {
        Plane[] planes = null;
        if(viewCull)
        {
            planes = GeometryUtility.CalculateFrustumPlanes(mainCam);

            // Modify the plane to a "taller" and "flatten" shape, to avoid shadow artifacts
            //planes[2].normal = Vector3.up;
            planes[2].distance += worldHeight * 32.0f; // push them further
            //planes[3].normal = Vector3.down;
            planes[3].distance += worldHeight * 32.0f;
        }

        foreach (var r in renderables)
        {
            //if (r.populated && r.matProp != null && r.indBuffer != null && r.buffer != null && r.indBuffer.IsValid() && r.buffer.IsValid())
            if (r.populated && r.matProp != null && r.indBuffer != null && r.buffer != null)
            {
                if(viewCull)
                {
                    if(!(GeometryUtility.TestPlanesAABB(planes, r.bound) || 
                            (r.bound.center - mainCam.transform.position).sqrMagnitude <= 16384.0f // 4 chunks
                        ))
                    {
                        continue;
                    }
                }

                var mat = Matrix4x4.TRS(transform.position + r.renderPosition, transform.rotation, transform.lossyScale);
                r.matProp.SetMatrix("_LocalToWorld", mat);
                r.matProp.SetMatrix("_WorldToLocal", mat.inverse);

                //Debug.Log($"[{r._ind[0]}, {r._ind[1]}, {r._ind[2]}, {r._ind[3]}]");

                Graphics.DrawProceduralIndirect(
                    r.chunkMat,
                    new Bounds(transform.position + r.position, transform.lossyScale * 32),
                    MeshTopology.Triangles,
                    r.indBuffer, 0, null, r.matProp);

                //Graphics.DrawProcedural(
                //        r.chunkMat,
                //        new Bounds(r.transform.position + r.transform.lossyScale * 16, r.transform.lossyScale * 32),
                //        MeshTopology.Triangles,
                //        128, 1, null, r.matProp);
            }
        }
    }

    // Not carefully designed, might have poor performance <START>
    public Chunk GetChunk(Vector3Int chunkCoord, bool create = false)
    {
        Chunk chk;
        if(chunks.TryGetValue(chunkCoord, out chk))
        {
            return chk;
        }

        if(create == true && InsideWorld(chunkCoord))
        {
            chk = CreateChunk(chunkCoord);

            return chk;
        }

        return null;
    }

    public Vector3Int GetChunkCoord(Vector3Int pos, out Vector3Int posInChunk)
    {
        Vector3Int chkPos = new Vector3Int(pos.x < 0 ? (pos.x + 1) / 32 - 1 : pos.x / 32, pos.y < 0 ? (pos.y + 1) / 32 - 1 : pos.y / 32, pos.z < 0 ? (pos.z + 1) / 32 - 1 : pos.z / 32);
        posInChunk = pos - chkPos * 32;
        return chkPos;
    }

    public uint GetBlock(Vector3Int pos)
    {
        Chunk chk;
        if(chunks.TryGetValue(GetChunkCoord(pos, out pos), out chk))
        {
            return chk.GetBlock(pos.x, pos.y, pos.z);
        }
        return 0;
    }

    public bool SetBlock(Vector3Int pos, uint id)
    {
        Chunk chk;
        if (chunks.TryGetValue(GetChunkCoord(pos, out pos), out chk))
        {
            chk.SetBlock(pos.x, pos.y, pos.z, id);
            return true;
        }
        return false;
    }
    // <END>

    bool InsideWorld(Vector3Int chunkCoord)
    {
        if (Mathf.Abs(chunkCoord.x) > worldSize / 32 || Mathf.Abs(chunkCoord.z) > worldSize / 32)
        {
            return false;
        }
        return true;
    }

    // Heavy
    bool ShouldPrepareData(Vector3Int chunkCoord)
    {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("ShouldPrepareData");
#endif
        if (!InsideWorld(chunkCoord))
        {
            UnityEngine.Profiling.Profiler.EndSample();
            return false;
        }

        Vector3 cp = chunkCoord * 32 + Vector3.one * 16.0f;

        bool res = (new Vector2(follows.position.x, follows.position.z) - new Vector2(cp.x, cp.z)).magnitude <= (showDistance) && cp.y <= (worldHeight * 32.0f);

#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
#endif
        return res;
    }

    // Heavy
    bool ShouldShow(Chunk chunk)
    {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("ShouldShow");
#endif
        Vector3 cp = chunk.centerPos;

        bool res = (new Vector2(follows.position.x, follows.position.z) - new Vector2(cp.x, cp.z)).magnitude <= (showDistance) && cp.y <= (worldHeight * 32.0f);
#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
#endif

        return res;
    }

    bool ShouldDisappear(ChunkRenderer r)
    {
        return (new Vector2(follows.position.x, follows.position.z) - new Vector2(r.position.x, r.position.z)).magnitude > (disappearDistance) || r.position.y > (worldHeight * 32.0f);
    }

    void RefreshRenderables()
    {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("RefreshRenderables()");
        UnityEngine.Profiling.Profiler.BeginSample("Remove phase"); // low cost ( ~15% of RefreshRenderables() )
#endif
        LinkedList<LinkedListNode<ChunkRenderer>> toDel = new LinkedList<LinkedListNode<ChunkRenderer>>();

        for(var p = renderables.First; p != null; p = p.Next)
        {
            var r = p.Value;
            if(r.populated && ShouldDisappear(r))
            {
                if(r != null)
                {
                    // TODO: delayed removal
                    if(removeChunkInMemory)
                    {
                        chunks.Remove(r.chunk.positionOffset / 32); // GO TO GC
                    }
                    r.Clean();
                    toDel.AddLast(p);
                }
            }

            //if((Time.realtimeSinceStartup - CoroutineStartTime) > (maxCoroutineSpendPerFrameMS / 1000.0f))
            //{
            //    yield return null;
            //}
        }

        foreach (var p in toDel)
        {
            renderables.Remove(p);
        }

#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("Generation"); // high cost
#endif

        foreach (var chk in chunks.Values)
        {
            // Profiler: ShouldShow 60.64% of RefreshRenderables()
            if (!chk.hasRenderer() && ShouldShow(chk) && chk.isReadyForPresent())
            {
                chk.renderer = CreateChunkRenderer(chk, chk.positionOffset, Quaternion.identity);
                renderables.AddLast(chk.renderer);
            }

            if(chk.hasRenderer() && chk.dirty)
            {
                chk.renderer.GenerateMesh(chk);
            }

            //if ((Time.realtimeSinceStartup - CoroutineStartTime) > (maxCoroutineSpendPerFrameMS / 1000.0f))
            //{
            //    yield return null;
            //}
        }

#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.EndSample();
#endif
    }

    IEnumerator BuildTasks()
    {
        Vector3Int currentChunk = new Vector3Int((int)(follows.position.x / 32), 0, (int)(follows.position.z / 32));
        int range = Mathf.CeilToInt(showDistance * 1.5f / 32.0f);

        // Heavy; no need to check every chunk each frame. 512 render distance = 9604 iterations
        // Build new chunks
        for (int cX = -range; cX <= range; cX++)
        {
            for (int cY = 0; cY < worldHeight; cY++)
            {
                for (int cZ = -range; cZ <= range; cZ++)
                {
                    Vector3Int dest = currentChunk + new Vector3Int(cX, cY, cZ);

                    // Profiler: ShouldPrepareData 51.33% of BuildTasks()
                    if (ShouldPrepareData(dest))
                    {
                        // Profiler: 21.16% of BuildTasks()
#if PROFILE
                        UnityEngine.Profiling.Profiler.BeginSample("chunks.TryGetValue / Generation");
#endif
                        Chunk chk;
                        if (!chunks.TryGetValue(dest, out chk))
                        {
                            chk = CreateChunk(dest);
                        }
#if PROFILE
                        UnityEngine.Profiling.Profiler.EndSample();
#endif

                        // Let the chunk populate itself if the chunk is not prepared
                        if (!chk.prepared)
                        {
                            chk.Populate(dest * 32, chunkGenerator, this);
                        }
                    }
                }

                if ((Time.realtimeSinceStartup - CoroutineStartTime) > (maxCoroutineSpendPerFrameMS / 1000.0f))
                {
                    yield return null;
                }
            }
        }
    }

    public void CreateStructure(BoundsInt bound, StructureGenerator structureGen)
    {
        // Generate all chunks inside bound
        //Vector3Int min = bound.min / 32;
        //Vector3Int max = bound.max / 32;

        //for (int cX = min.x; cX <= max.x; cX++)
        //{
        //    for (int cY = min.y; cY <= max.y; cY++)
        //    {
        //        for (int cZ = min.z; cZ <= max.z; cZ++)
        //        {
        //            Vector3Int dest = new Vector3Int(cX, cY, cZ);
        //            if (InsideWorld(dest) && !chunks.ContainsKey(dest))
        //            {
        //                CreateChunk(dest);
        //            }
        //        }
        //    }
        //}

        //structureGen.Generate(bound, this);

        CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(this, structureGen, bound));
    }

    Chunk CreateChunk(Vector3Int dest)
    {
        Chunk chunk = new Chunk();
        chunk.positionOffset = dest * 32;
        chunks.Add(dest, chunk);

        // Populate the chunk with selected generator
        // chunk.Populate(dest, chunkGenerator, this);

        return chunk;
    }

    ChunkRenderer CreateChunkRenderer(Chunk chunk, Vector3 position, Quaternion rotation)
    {
        ChunkRenderer r = new ChunkRenderer();
        r.cs_chunkMeshPopulator = cs_chunkMeshPopulator;
        r.chunkMat = chunkMat;

        r.Init(chunk);
        
        return r;
    }

    private void OnDestroy()
    {
        ClearAllImmediate();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadEvents_beforeAssemblyReload;
        UnityEditor.AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_afterAssemblyReload;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= AssemblyReloadEvents_beforeAssemblyReload;
        UnityEditor.AssemblyReloadEvents.afterAssemblyReload -= AssemblyReloadEvents_afterAssemblyReload;
#endif
    }

    void ClearAllImmediate()
    {
        // Clean up
        StopAllCoroutines();

        for (var p = renderables.First; p != null; p = p.Next)
        {
            var r = p.Value;

            if (r != null)
            {
                r.Clean();
            }
        }

        renderables.Clear();

        foreach (var chunk in chunks.Values)
        {
            chunk.Dispose();
        }
        chunks = new Dictionary<Vector3Int, Chunk>(); // Should go to GC right?

        UpdateMgr.Destroy();
    }

    private void AssemblyReloadEvents_beforeAssemblyReload()
    {
        ClearAllImmediate();
    }

    private void AssemblyReloadEvents_afterAssemblyReload()
    {
        SetWorld();
    }
}
