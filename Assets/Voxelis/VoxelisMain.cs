using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis.WorldGen;

namespace Voxelis
{
    public class VoxelisMain : MonoBehaviour
    {
        List<BlockGroup> allBlockGroups = new List<BlockGroup>();

        public ComputeShader cs_generation;
        public int cs_generation_batchsize;

        public Voxelis.VoxelisGlobalSettings globalSettings;
        public Material chunkMat;
        public ComputeShader cs_chunkMeshPopulator;

        public Camera mainCam;

        private void Awake()
        {
            GeometryIndependentPass.cs_generation = cs_generation;
            GeometryIndependentPass.cs_generation_batchsize = cs_generation_batchsize;
            GeometryIndependentPass.Init();
        }

        // Use this for initialization
        void Start()
        {
            worldGeneratingCoroutine = StartCoroutine(WorldUpdateCoroutine());
        }

        // Update is called once per frame
        void Update()
        {
            CustomJobs.CustomJob.UpdateAllJobs();
            GeometryIndependentPass.Update();
        }

        #region WorldUpdate

        protected Coroutine worldGeneratingCoroutine;
        protected BlockGroup currentUpdatingBlockGroup;

        public float UpdateCoroutineBudgetMS = 15.0f;

        protected IEnumerator WorldUpdateCoroutine()
        {
            while(true)
            {
                BlockGroup[] allBG_curr = new BlockGroup[allBlockGroups.Count];
                allBlockGroups.CopyTo(allBG_curr);

                foreach (var bg in allBG_curr)
                {
                    bg.budgetMS = UpdateCoroutineBudgetMS;
                    bg.StartWorldUpdateSingleLoop();

                    while (!bg.UpdateLoopFinished)
                    {
                        yield return null;
                    }
                }

                yield return null;
            }
        }

        #endregion

        #region BlockGroupContainer Ops

        public void Destroy()
        {
            GeometryIndependentPass.Destroy();
        }

        public bool Contains(BlockGroup bg)
        {
            return allBlockGroups.Contains(bg);
        }

        public void Add(BlockGroup bg)
        {
            allBlockGroups.Add(bg);

            bg.globalSettings = globalSettings;
            bg.chunkMat = chunkMat;
            bg.cs_chunkMeshPopulator = cs_chunkMeshPopulator;

            bg.follows = mainCam.transform;
            bg.mainCam = mainCam;
        }

        #endregion

        #region CleanUp

        private void ClearAllImmediate()
        {
            GeometryIndependentPass.Destroy();
        }

        public void Refresh()
        {
            ClearAllImmediate();
        }

        protected void OnDestroy()
        {
            ClearAllImmediate();
        }

        protected virtual void AssemblyReloadEvents_beforeAssemblyReload()
        {
            ClearAllImmediate();
        }

        #endregion
    }
}