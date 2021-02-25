#define PROFILE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis.Rendering;

namespace Voxelis
{
    public class BlockGroup : MonoBehaviour
    {
        //// Data Holders
        // Concurrent ?
        public Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();
        public LinkedList<ChunkRenderableBase> renderables = new LinkedList<ChunkRenderableBase>();

        //// Overall
        public bool isFinite = true;
        public BoundsInt blockBound;

        //// Rendering control
        public int showDistance = 256, disappearDistance = 360;
        public bool viewCull = true;

        [HideInInspector]
        public bool removeChunkInMemory = false;

        //// Assigned by VoxelisMain
        [HideInInspector]
        public Voxelis.VoxelisGlobalSettings globalSettings;

        [HideInInspector]
        public Material chunkMat;

        [HideInInspector]
        public ComputeShader cs_chunkMeshPopulator;

        [HideInInspector]
        public Transform follows;

        [HideInInspector]
        public Camera mainCam;

        // Use this for initialization
        protected virtual void Start()
        {
            if(!Globals.voxelisMain.Instance.Contains(this))
            {
                Globals.voxelisMain.Instance.Add(this);
            }
        }

        // Update is called once per frame
        protected virtual void Update()
        {
            startTime = Time.realtimeSinceStartup;
        }

        protected virtual void LateUpdate()
        {
            // I DON'T KNOW WHY BY PUTTING THIS INTO LATE UPDATE WILL NOT CAUSE ANY CRASH. BUT IT WORKS JUST FINE. OKAY BLAME UNITY (*@#^$&
            Render();
        }

        protected virtual void Render()
        {
            Plane[] planes = null;
            if (viewCull)
            {
                planes = GeometryUtility.CalculateFrustumPlanes(mainCam);
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

        // Not carefully designed, might have poor performance <START>
        public virtual Chunk GetChunk(Vector3Int chunkCoord, bool create = false)
        {
            Chunk chk;
            if (chunks.TryGetValue(chunkCoord, out chk))
            {
                return chk;
            }

            if (create == true)
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

        public Block GetBlock(Vector3Int pos)
        {
            Chunk chk;
            if (chunks.TryGetValue(GetChunkCoord(pos, out pos), out chk))
            {
                return chk.GetBlock(pos.x, pos.y, pos.z);
            }
            return Block.Empty;
        }

        public bool SetBlock(Vector3Int pos, ushort id)
        {
            return SetBlock(pos, Block.FromID(id));
        }

        public bool SetBlock(Vector3Int pos, Block block)
        {
            Chunk chk;
            if (chunks.TryGetValue(GetChunkCoord(pos, out pos), out chk))
            {
                chk.SetBlock(pos.x, pos.y, pos.z, block);
                return true;
            }
            return false;
        }
        // <END>

        #region Update related

        [HideInInspector]
        public enum WorldUpdateStage
        {
            BUILD_TASKS = 0,
            REFRESH_RENDERABLES = 1,
        }
        [HideInInspector]
        public WorldUpdateStage currentWorldUpdateStage { get; protected set; }
        [HideInInspector]
        public string[] _worldUpdateStageStr = new string[]
        {
            "Build tasks",
            "Refresh renderables",
        };

        [HideInInspector]
        public float startTime, budgetMS = 15.0f;

        public bool UpdateLoopFinished { get; protected set; }

        public virtual void StartWorldUpdateSingleLoop()
        {
            UpdateLoopFinished = false;

            StartCoroutine(WorldUpdateCoroutine());
        }

        public virtual IEnumerator WorldUpdateCoroutine()
        {
            currentWorldUpdateStage = WorldUpdateStage.BUILD_TASKS;
            yield return StartCoroutine(BuildTasks());
            currentWorldUpdateStage = WorldUpdateStage.REFRESH_RENDERABLES;
            yield return StartCoroutine(RefreshRenderables());

            UpdateLoopFinished = true;
            //yield return null;
        }

        protected virtual IEnumerator RefreshRenderables()
        {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("RefreshRenderables()");
        UnityEngine.Profiling.Profiler.BeginSample("Remove phase"); // low cost ( ~15% of RefreshRenderables() )
#endif
            LinkedList<LinkedListNode<ChunkRenderableBase>> toDel = new LinkedList<LinkedListNode<ChunkRenderableBase>>();

            for (var p = renderables.First; p != null; p = p.Next)
            {
                var r = p.Value;
                if (r.IsReadyForPresent() && ShouldDisappear(r))
                {
                    if (r != null)
                    {
                        // TODO: delayed removal
                        if (removeChunkInMemory)
                        {
                            chunks.Remove(r.GetChunk().positionOffset / 32); // GO TO GC
                        }
                        r.Clean();
                        toDel.AddLast(p);
                    }
                }

                if ((Time.realtimeSinceStartup - startTime) > (budgetMS / 1000.0f))
                {
#if PROFILE
                    UnityEngine.Profiling.Profiler.EndSample();
                    UnityEngine.Profiling.Profiler.EndSample();
#endif
                    yield return null;
#if PROFILE
                    UnityEngine.Profiling.Profiler.BeginSample("RefreshRenderables()");
                    UnityEngine.Profiling.Profiler.BeginSample("Remove phase");
#endif
                }
            }

            foreach (var p in toDel)
            {
                renderables.Remove(p);
            }

#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("Generation"); // high cost
#endif

            Vector3Int[] keys_copy = new Vector3Int[chunks.Keys.Count];
            chunks.Keys.CopyTo(keys_copy, 0);
            foreach (var chkpos in keys_copy)
            {
                if (chunks.ContainsKey(chkpos))
                {
                    var chk = chunks[chkpos];

                    // Profiler: ShouldShow 60.64% of RefreshRenderables()
                    if (!chk.hasRenderer() && ShouldShow(chk) && chk.isReadyForPresent())
                    {
                        chk.renderer = CreateChunkRenderer(chk, chk.positionOffset, Quaternion.identity);
                        renderables.AddLast(chk.renderer);
                    }

                    if (chk.hasRenderer() && chk.dirty && chk.prepared)
                    {
                        chk.renderer.GenerateGeometry(this, chk);
                    }

                    if ((Time.realtimeSinceStartup - startTime) > (budgetMS / 1000.0f))
                    {
#if PROFILE
                        UnityEngine.Profiling.Profiler.EndSample();
                        UnityEngine.Profiling.Profiler.EndSample();
#endif
                        yield return null;
#if PROFILE
                        UnityEngine.Profiling.Profiler.BeginSample("RefreshRenderables()");
                        UnityEngine.Profiling.Profiler.BeginSample("Generation");
#endif
                    }
                }
            }

#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.EndSample();
#endif
        }

        protected virtual IEnumerator BuildTasks()
        {
            Vector3Int currentChunk = new Vector3Int((int)(follows.position.x / 32), 0, (int)(follows.position.z / 32));
            int range = Mathf.CeilToInt(showDistance * 1.5f / 32.0f);

            // Heavy; no need to check every chunk each frame. 512 render distance = 9604 iterations
            // Build new chunks
            for (int cX = -range; cX <= range; cX++)
            {
                for (int cY = -range; cY <= range; cY++)
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

        #endregion

        protected virtual void PopulateChunk(Chunk chunk, Vector3Int chunkPos)
        {
            // Do nothing
        }

        // Heavy
        protected virtual bool ShouldPrepareData(Vector3Int chunkCoord)
        {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("ShouldPrepareData");
#endif
            Vector3 cp = chunkCoord * 32 + Vector3.one * 16.0f;

            bool res = (new Vector3(follows.position.x, follows.position.y, follows.position.z) - new Vector3(cp.x, cp.y, cp.z)).magnitude <= (showDistance);

#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
#endif
            return res;
        }

        // Heavy
        protected virtual bool ShouldShow(Chunk chunk)
        {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("ShouldShow");
#endif
            Vector3 cp = chunk.centerPos;

            bool res = (new Vector3(follows.position.x, follows.position.y, follows.position.z) - new Vector3(cp.x, cp.y, cp.z)).magnitude <= (showDistance);
#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
#endif

            return res;
        }

        protected virtual bool ShouldDisappear(ChunkRenderableBase r)
        {
            return (new Vector3(follows.position.x, follows.position.y, follows.position.z) - r.position).magnitude > (showDistance);
        }

        protected Chunk CreateChunk(Vector3Int dest)
        {
            Chunk chunk = new Chunk();
            chunk.positionOffset = dest * 32;
            chunks.Add(dest, chunk);

            return chunk;
        }

        protected ChunkRenderableBase CreateChunkRenderer(Chunk chunk, Vector3 position, Quaternion rotation)
        {
            ChunkRenderableBase r = (ChunkRenderableBase)System.Activator.CreateInstance(globalSettings.rendererType);

            r.Init(this, chunk);
            r.GenerateGeometry(this, chunk);

            return r;
        }

        protected void OnDestroy()
        {
            ClearAllImmediate();
        }

        protected void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += AssemblyReloadEvents_beforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_afterAssemblyReload;
#endif
        }

        protected void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= AssemblyReloadEvents_beforeAssemblyReload;
            UnityEditor.AssemblyReloadEvents.afterAssemblyReload -= AssemblyReloadEvents_afterAssemblyReload;
#endif
        }

        protected void ClearAllImmediate()
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
        }

        protected virtual void AssemblyReloadEvents_beforeAssemblyReload()
        {
            ClearAllImmediate();
        }

        protected virtual void AssemblyReloadEvents_afterAssemblyReload()
        {
        }

        // Helper
        public static uint GetID(int r, int g, int b, int a)
        {
            return (((uint)r) << 24) + (((uint)g) << 16) + (((uint)b) << 8) + ((uint)a);
        }
    }
}