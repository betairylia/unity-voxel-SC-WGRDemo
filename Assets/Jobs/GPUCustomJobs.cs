using UnityEngine;
using System.Collections;
using Unity.Jobs;

namespace Voxelis.CustomJobs
{
    public abstract class BatchedGPUComputeJob : CustomJob
    {
        protected bool GPUFinished;

        public BatchedGPUComputeJob() : base()
        {
            GPUFinished = false;
        }

        public virtual void OnGPUFinish()
        {
            GPUFinished = true;
        }

        public override bool CheckFinished()
        {
            if (scheduled && GPUFinished)
            {
                return true;
            }
            return false;
        }
    }
}
