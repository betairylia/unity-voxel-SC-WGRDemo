#define PROFILE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TypeReferences;
using System;
using Unity.Jobs;
using UnityEngine.Rendering;
using WorldGen.WorldSketch;
using Voxelis.Rendering;
using Voxelis.WorldGen;

// TODO: Refactor for editor usage
namespace Voxelis
{
    public class World : BlockGroup
    {
        public int worldHeight = 4;
        public float waterHeight = 10.0f;

        public int worldSize = 1024;

        public TMPro.TextMeshProUGUI debugText;
        bool worldGenerationCRInProgress;

        protected ChunkGenerator chunkGenerator;
        protected IWorldSketcher sketcher;

        public Transform groundPlane, capPlane;

        public ComputeShader cs_generation;
        public int cs_generation_batchsize = 512;

        // World Sketch stuffs
        [HideInInspector]
        public bool isSketchReady { get; private set; }

        [HideInInspector]
        public float[] heightMap;

        [HideInInspector]
        public int mapLen;

        [Header("Erosion Settings")]
        public ComputeShader erosion_cs;

        [Header("World Sketch Preview")]
        public Texture2D sketchMapTex;
        public UnityEngine.UI.RawImage sketchMinimap;
        public int minimapSize = 300;
        public RectTransform playerPointer;

        public bool showSketchMesh = true;
        public Material sketchMeshMat;

        const int worldSketchSize = 1024;

        public Matryoshka.MatryoshkaGraph matryoshkaGraph;

        [EnumNamedArray(typeof(WorldGen.StructureType))]
        public Matryoshka.MatryoshkaGraph[] structureGraphs = new Matryoshka.MatryoshkaGraph[8];

        public bool isMainWorld = false;

        [Inherits(typeof(ChunkGenerator))]
        public TypeReference generatorType;

        [Inherits(typeof(IWorldSketcher))]
        public TypeReference sketcherType;

        // Start is called before the first frame update
        protected override void Start()
        {
            base.Start();

            ChunkRenderer.cs_chunkMeshPopulator = cs_chunkMeshPopulator;
            ChunkRenderer.chunkMat = chunkMat;

            SetWorld();
        }

        protected virtual void SetWorld()
        {
            // Do world sketch
            SketchWorld();

            if(isMainWorld)
            {
                GeometryIndependentPass.SetWorld(this);
            }

            // Setup generators
            chunkGenerator = (ChunkGenerator)System.Activator.CreateInstance(generatorType);

            for (int i = 0; i < 0; i++)
            //for (int i = 0; i < 64; i++)
            {
                Vector2Int pos = new Vector2Int(UnityEngine.Random.Range(-800, 800), UnityEngine.Random.Range(-800, 800));
                CreateStructure(new BoundsInt(pos.x, 5, pos.y, 96, 96, 96), new TestTree());
            }
            //CreateStructure(new BoundsInt(-122, 32, 225, 140, 120, 140), new TestTree());
            //CreateStructure(new BoundsInt(56, 10, 371, 140, 120, 140), new TestTree());
            //CreateStructure(new BoundsInt(143, 0, 177, 140, 120, 140), new TestTree());
        }

        private void SketchWorld(int seed = -1)
        {
            mapLen = worldSketchSize;
            heightMap = new float[worldSketchSize * worldSketchSize];
            float[] erosionMap = new float[worldSketchSize * worldSketchSize];
            float[] waterMap = new float[worldSketchSize * worldSketchSize];

            // Setup computeshaders
            HydraulicErosionGPU.erosion = erosion_cs;

            // Generate height maps
            sketcher = (IWorldSketcher)System.Activator.CreateInstance(sketcherType);
            sketcher.FillHeightmap(ref heightMap, ref erosionMap, ref waterMap, worldSketchSize, worldSketchSize);
            //WorldGen.WorldSketch.SillyRiverPlains.FillHeightmap(ref heightMap, ref erosionMap, ref waterMap, worldSketchSize, worldSketchSize);

            // Create texture
            sketchMapTex = new Texture2D(worldSketchSize, worldSketchSize, TextureFormat.RGBAFloat, false);

            for (int i = 0; i < worldSketchSize; i++)
            {
                for (int j = 0; j < worldSketchSize; j++)
                {
                    sketchMapTex.SetPixel(i, j, new Color(heightMap[i * mapLen + j], erosionMap[i * mapLen + j], waterMap[i * mapLen + j], 0.0f));
                }
            }

            sketchMapTex.Apply();

            if (sketchMinimap)
            {
                sketchMinimap.texture = sketchMapTex;
                sketchMinimap.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, minimapSize);
                sketchMinimap.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minimapSize);
            }

            if (showSketchMesh)
            {
                ShowSketchMesh();
            }
        }

        int sketchMeshSize = 256;
        float sketchScale = 4.0f;

        Vector3 GetHeightmapPoint(float uvx, float uvy)
        {
            float h = sketchMapTex.GetPixelBilinear(uvx, uvy).r;
            return new Vector3(
                (uvx - 0.5f) * worldSketchSize * sketchScale,
                h * 256.0f,
                (uvy - 0.5f) * worldSketchSize * sketchScale
                );
        }

        private void ShowSketchMesh()
        {
            GameObject obj = new GameObject("SketchMesh");
            obj.transform.parent = this.transform;
            obj.transform.position = Vector3.zero;
            obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            obj.GetComponent<MeshFilter>().mesh = mesh;
            obj.GetComponent<MeshRenderer>().material = new Material(sketchMeshMat);
            obj.GetComponent<MeshRenderer>().material.SetTexture("_Control", sketchMapTex);
            obj.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            obj.GetComponent<MeshRenderer>().receiveShadows = true;

            Vector3[] vert = new Vector3[(sketchMeshSize) * (sketchMeshSize)];
            Vector3[] normal = new Vector3[(sketchMeshSize) * (sketchMeshSize)];
            Vector2[] uv = new Vector2[(sketchMeshSize) * (sketchMeshSize)];
            int[] triangles = new int[6 * (sketchMeshSize - 1) * (sketchMeshSize - 1)];

            for (int i = 0; i < sketchMeshSize; i++)
            {
                for (int j = 0; j < sketchMeshSize; j++)
                {
                    vert[i * sketchMeshSize + j] = GetHeightmapPoint((float)i / sketchMeshSize, (float)j / sketchMeshSize);

                    // Normal calculation
                    Vector3 xx = new Vector3(0, 0, 0), zz = new Vector3(0, 0, 0);

                    if (j > 0)
                    {
                        zz += GetHeightmapPoint((float)(i) / sketchMeshSize, (float)(j) / sketchMeshSize) - GetHeightmapPoint((float)(i) / sketchMeshSize, (float)(j - 1) / sketchMeshSize);
                    }
                    if (j < sketchMeshSize - 1)
                    {
                        zz += GetHeightmapPoint((float)(i) / sketchMeshSize, (float)(j + 1) / sketchMeshSize) - GetHeightmapPoint((float)(i) / sketchMeshSize, (float)(j) / sketchMeshSize);
                    }
                    if (i > 0)
                    {
                        xx += GetHeightmapPoint((float)(i) / sketchMeshSize, (float)(j) / sketchMeshSize) - GetHeightmapPoint((float)(i - 1) / sketchMeshSize, (float)(j) / sketchMeshSize);
                    }
                    if (i < sketchMeshSize - 1)
                    {
                        xx += GetHeightmapPoint((float)(i + 1) / sketchMeshSize, (float)(j) / sketchMeshSize) - GetHeightmapPoint((float)(i) / sketchMeshSize, (float)(j) / sketchMeshSize);
                    }

                    Vector3 yy = Vector3.Cross(zz, xx);
                    yy = yy.normalized;

                    normal[i * sketchMeshSize + j] = yy;

                    uv[i * sketchMeshSize + j] = new Vector2((float)i / sketchMeshSize, (float)j / sketchMeshSize);

                    if (i > 0 && j > 0)
                    {
                        int s = (i - 1) * (sketchMeshSize - 1) + j - 1;
                        s *= 6;

                        triangles[s + 0] = (i - 1) * sketchMeshSize + (j - 1);
                        triangles[s + 1] = (i - 1) * sketchMeshSize + (j - 0);
                        triangles[s + 2] = (i - 0) * sketchMeshSize + (j - 1);
                        triangles[s + 3] = (i - 1) * sketchMeshSize + (j - 0);
                        triangles[s + 4] = (i - 0) * sketchMeshSize + (j - 0);
                        triangles[s + 5] = (i - 0) * sketchMeshSize + (j - 1);
                    }
                }
            }

            mesh.vertices = vert;
            mesh.normals = normal;
            mesh.uv = uv;
            mesh.triangles = triangles;
        }

        //Vector3 _pos;
        //int f = 0;
        //float mspd = 0.2f;
        // Update is called once per frame
        protected override void Update()
        {
            base.Update();

            //if (!worldGenerationCRInProgress)
            //{
            //    worldGeneratingCoroutine = StartCoroutine(WorldUpdateCoroutine());
            //}

            // Randomly set some blocks
            //for(int ti = 0; ti < 1000; ti ++)
            //{
            //    SetBlock(new Vector3Int(
            //        UnityEngine.Random.Range(-worldSize, worldSize),
            //        UnityEngine.Random.Range(0, 32 * worldHeight),
            //        UnityEngine.Random.Range(-worldSize, worldSize)
            //    ), 0xffffffff);
            //}

            //if(f % 1 == 0)
            //{
            //    int size = 50;
            //    if (_pos == null) { _pos = new Vector3(mspd * Time.time - 10, 80, -10); }
            //    (new UglySphere(0x00000000)).Generate(new BoundsInt(Mathf.FloorToInt(_pos.x), Mathf.FloorToInt(_pos.y), Mathf.FloorToInt(_pos.z), size, size, size), this);
            //    _pos = new Vector3(Mathf.Sin(mspd * Time.time - 10) * 128.0f, 80, -Mathf.Cos(mspd * Time.time - 10) * 128.0f);
            //    (new UglySphere(0x00ff00ff)).Generate(new BoundsInt(Mathf.FloorToInt(_pos.x), Mathf.FloorToInt(_pos.y), Mathf.FloorToInt(_pos.z), size, size, size), this);
            //}
            //f++;

            // SUPER HEAVY - FIXME: Optimize it orz
            //RefreshRenderables();
            if (debugText != null)
            {
                // Get renderable size
                uint vCount = 0;
                foreach (var r in renderables)
                {
                    vCount += r.GetVertCount();
                }

                debugText.text = $"" +
                    $"CHUNKS:\n" +
                    $"Loaded:   {chunks.Count} ({chunks.Count * sizeof(uint) * 32 / 1024} MB)\n" +
                    $"Rendered: {renderables.Count} ({(vCount / 1024.0f) * System.Runtime.InteropServices.Marshal.SizeOf(typeof(ChunkRenderer.Vertex)) / 1024.0f} MB)\n" +
                    $"          {vCount} verts\n" +
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
                    $"Scheduled     {CustomJobs.CustomJob.scheduledJobs.Count}\n" +
                    $"\n" +
                    $"Loop stage:\n" +
                    $"{_worldUpdateStageStr[(int)currentWorldUpdateStage]}\n" +
                    $"{Matryoshka.Utils.NoisePools.OS2S_FBm_3oct_f1.instances.Count}\n" +
                    $"\n" +
                    $"[C] - Toggle freeview";
            }

            // Forced fence placement
            GPUDispatchManager.Singleton.CheckTasks();
            GPUDispatchManager.Singleton.SyncTasks();

            groundPlane.position = new Vector3(follows.position.x, waterHeight, follows.position.z);
            capPlane.position = new Vector3(follows.position.x, worldHeight * 32, follows.position.z);

            // Update player pointer on minimap
            if (playerPointer)
            {
                playerPointer.anchoredPosition = new Vector2(
                    Mathf.Clamp((mainCam.transform.position.x / (float)worldSize) * (minimapSize / 2.0f), -(minimapSize / 2.0f), (minimapSize / 2.0f)),
                    Mathf.Clamp((mainCam.transform.position.z / (float)worldSize) * (minimapSize / 2.0f), -(minimapSize / 2.0f), (minimapSize / 2.0f))
                );
            }

        }

        float avgFPS = 0;
        float EMAFPS(float val)
        {
            float eps = 0.13f;
            avgFPS = eps * val + (1 - eps) * avgFPS;
            return avgFPS;
        }

        public override Chunk GetChunk(Vector3Int chunkCoord, bool create = false)
        {
            Chunk chk;
            if (chunks.TryGetValue(chunkCoord, out chk))
            {
                return chk;
            }

            if (create == true && InsideWorld(chunkCoord)) // <- diff with base; TODO: refactor
            {
                chk = CreateChunk(chunkCoord);

                return chk;
            }

            return null;
        }

        // Difference with blockgroup version: viewCull implementation
        protected override void Render()
        {
            Plane[] planes = null;
            if (viewCull)
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
                if (r.IsReadyForPresent())
                {
                    if (viewCull)
                    {
                        if (!(GeometryUtility.TestPlanesAABB(planes, r.bound) ||
                                (r.bound.center - mainCam.transform.position).sqrMagnitude <= 16384.0f // 4 chunks
                            ))
                        {
                            continue;
                        }
                    }

                    r.Render(this);
                }
            }
        }

        bool InsideWorld(Vector3Int chunkCoord)
        {
            if (Mathf.Abs(chunkCoord.x) > worldSize / 32 || Mathf.Abs(chunkCoord.z) > worldSize / 32)
            {
                return false;
            }
            return true;
        }

        // Heavy
        // Difference with blockgroup version: Y
        protected override bool ShouldPrepareData(Vector3Int chunkCoord)
        {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("ShouldPrepareData");
#endif
            if (!InsideWorld(chunkCoord))
            {
#if PROFILE
                UnityEngine.Profiling.Profiler.EndSample();
#endif
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
        protected override bool ShouldShow(Chunk chunk)
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

        protected override bool ShouldDisappear(ChunkRenderableBase r)
        {
            return (new Vector2(follows.position.x, follows.position.z) - new Vector2(r.position.x, r.position.z)).magnitude > (disappearDistance) || r.position.y > (worldHeight * 32.0f);
        }

        // Y
        protected override IEnumerator BuildTasks()
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
                            if (!chk.prepared && !chk.populating)
                            {
                                PopulateChunk(chk, dest * 32);
                            }
                        }
                    }

                    if ((Time.realtimeSinceStartup - startTime) > (budgetMS / 1000.0f))
                    {
                        yield return null;
                    }
                }
            }
        }

        protected override void PopulateChunk(Chunk chunk, Vector3Int chunkPos)
        {
            base.PopulateChunk(chunk, chunkPos);

            chunk._PopulateStart(chunkPos);
            if(chunkGenerator.Generate(chunk, this))
            {
                chunk._PopulateFinish();
            }
        }

        public void CreateStructure(BoundsInt bound, IStructureGenerator structureGen)
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

        protected override void AssemblyReloadEvents_afterAssemblyReload()
        {
            SetWorld();
        }
    }
}

