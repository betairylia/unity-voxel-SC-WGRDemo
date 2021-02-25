// Jobs that needs to write data into chunks.

using UnityEngine;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;

namespace Voxelis.CustomJobs
{
    /// <summary>
    /// This will ensure that there will be no other jobs writing to the chunk when Execute() was called.
    /// </summary>
    public abstract class SingleChunkJob : CustomJob
    {
        protected Chunk chunk;

        public SingleChunkJob(Chunk chunk)
        {
            this.chunk = chunk;
        }

        public override void InitJob()
        {
            if (chunk.lastQueuedWriter != null)
            {
                Depends(chunk.lastQueuedWriter);
            }
            chunk.lastQueuedWriter = this;
        }

        protected override void OnFinish()
        {
            base.OnFinish();
            if (chunk.lastQueuedWriter == this)
            {
                chunk.lastQueuedWriter = null;
            }
        }
    }

    public class SingleChunkHostModify : SingleChunkJob
    {
        public delegate void Modification(Chunk chunk);
        Modification modification;

        public SingleChunkHostModify(Chunk chunk, Modification modification) : base(chunk)
        {
            isUnique = false;
            this.modification = modification;
        }

        protected override void OnExecute()
        {
            modification?.Invoke(chunk);
        }
    }

    public abstract class SingleChunkGPUJob : BatchedGPUComputeJob
    {
        protected Chunk chunk;

        public SingleChunkGPUJob(Chunk chunk) : base()
        {
            this.chunk = chunk;
        }

        public override void InitJob()
        {
            if (chunk.lastQueuedWriter != null)
            {
                Depends(chunk.lastQueuedWriter);
            }
            chunk.lastQueuedWriter = this;
        }

        protected override void OnFinish()
        {
            base.OnFinish();
            if (chunk.lastQueuedWriter == this)
            {
                chunk.lastQueuedWriter = null;
            }
        }
    }

    /// <summary>
    /// This will ensure that there will be no other jobs writing to the chunk when Execute() was called.
    /// </summary>
    public abstract class MultipleChunkJob : CustomJob
    {
        protected List<Chunk> chunks;

        public MultipleChunkJob(List<Chunk> chunks)
        {
            this.chunks = chunks;
        }

        public override void InitJob()
        {
            foreach (var chunk in chunks)
            {
                if (chunk.lastQueuedWriter != null)
                {
                    Depends(chunk.lastQueuedWriter);
                }
                chunk.lastQueuedWriter = this;
            }
        }

        protected override void OnFinish()
        {
            base.OnFinish();
            foreach (var chunk in chunks)
            {
                if(chunk == null) { continue; }
                if(chunk.lastQueuedWriter == this)
                {
                    chunk.lastQueuedWriter = null;
                }
            }
        }
    }
}
