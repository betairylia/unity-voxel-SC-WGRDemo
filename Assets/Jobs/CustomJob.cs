using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Jobs;

// TODO: use memory pools, maybe ?
// TODO: now not using Unity's native job system's dependencies as we need to take care of GPU & mainthread. what should we do?
namespace CustomJobs
{
    public abstract class CustomJob
    {
        public delegate void OnFinishCallback(CustomJob job);
        public static LinkedList<CustomJob> scheduledJobs = new LinkedList<CustomJob>(); // Jobs that are currently active, running in CPU
        public static Dictionary<CustomJob, CustomJob> queuedUniqueJobs = new Dictionary<CustomJob, CustomJob>(); // Unique Jobs that has registered, which may be run in the future

        public static int Count { get; private set; }

        public static void UpdateAllJobs()
        {
            //Profiler.BeginSample($"CustomJobUpdate ({scheduledJobs.Count})");
            LinkedListNode<CustomJob> p = scheduledJobs.First;
            LinkedListNode<CustomJob> _p;
            while (p != null)
            {
                _p = p.Next;

                if (p.Value.CheckFinished())
                {
                    // TODO: do something, update dependencies
                    p.Value.OnFinish();
                    p.Value.finished = true;

                    scheduledJobs.Remove(p);
                    queuedUniqueJobs.Remove(p.Value);
                    Count -= 1;
                }

                p = _p;
            }
            //Profiler.EndSample();
        }

        // Try to add a job to current queue. If the job is already presented, the existing job will be returned; If the job is already finished, null will be returned; otherwise the job itself will be returned.
        public static CustomJob TryAddJob(CustomJob job, OnFinishCallback finishCallback = null)
        {
            if (job.isUnique)
            {
                if (queuedUniqueJobs.ContainsKey(job))
                {
                    //Debug.Log($"{job.ToString()} has already been queued.");
                    queuedUniqueJobs[job].finishCallback += finishCallback;
                    return queuedUniqueJobs[job];
                }

                // Check if job is already finished
                job.finishCallback = finishCallback;
                if (job.AlreadyFinished())
                {
                    job.OnFinish();
                    return null;
                }

                job.InitJob();
                queuedUniqueJobs.Add(job, job);
                Count += 1;
                job.Schedule();
            }
            else
            {
                job.InitJob();
                Count += 1;
                job.Schedule();
            }

            return job;
        }

        // --- Non-static members --- //

        protected OnFinishCallback finishCallback;

        protected LinkedList<CustomJob> followingTasks = new LinkedList<CustomJob>();
        protected int dependenciesCount = 0;

        public CustomJob() { }

        protected bool scheduled = false, finished = false;
        protected JobHandle jobHandle;

        #region Uniqueness
        protected bool isUnique = true;

        public override string ToString()
        {
            if(isUnique)
            {
                Debug.LogError("You need to implement ToString(), GetHashCode(), Equals(object) and AlreadyFinished() with out calling base for isUnique CustomJobs; or set isUnique = false.");
                throw new System.NotImplementedException();
            }
            else
            {
                return "NonUniqueCastomJob";
            }
        }

        public override int GetHashCode()
        {
            if (isUnique)
            {
                Debug.LogError("You need to implement ToString(), GetHashCode(), Equals(object) and AlreadyFinished() with out calling base for isUnique CustomJobs; or set isUnique = false.");
                throw new System.NotImplementedException();
            }
            else
            {
                return base.GetHashCode();
            }
        }

        public override bool Equals(object obj)
        {
            if (isUnique)
            {
                Debug.LogError("You need to implement ToString(), GetHashCode(), Equals(object) and AlreadyFinished() with out calling base for isUnique CustomJobs; or set isUnique = false.");
                throw new System.NotImplementedException();
            }
            else
            {
                return (object)this == obj;
            }
        }

        protected virtual bool AlreadyFinished()
        {
            if (isUnique)
            {
                Debug.LogError("You need to implement ToString(), GetHashCode(), Equals(object) and AlreadyFinished() with out calling base for isUnique CustomJobs; or set isUnique = false.");
                throw new System.NotImplementedException();
            }
            else
            {
                return false;
            }
        }

        #endregion

        protected virtual void Depends(CustomJob dependency)
        {
            if(dependency == null) { return; } // Probably we depends on an "AlreadyFinished" job.

            // TODO: dependency.followingTasks is a LinkedList; dependencies may overlap due to chunk locking. currently this was not cared and dependencies are let to be duplicated.
            dependency.followingTasks.AddLast(this);
            dependenciesCount += 1;
        }

        protected virtual void Depends(IEnumerable<CustomJob> dependencies)
        {
            foreach (var job in dependencies)
            {
                Depends(job);
            }
        }

        public abstract void InitJob();


        // Main entry for starting the job; will be called on startup.
        protected virtual void Schedule()
        {
            // If no dependencies, start the job immedietely.
            if (dependenciesCount == 0)
            {
                Execute();
            }

            // Otherwise, wait for all dependencies to be finished
        }

        protected virtual void Execute()
        {
            scheduledJobs.AddLast(this);

            // TODO: Use Unity Jobs to schedule the actual job.
            scheduled = true;
            OnExecute();
        }

        protected abstract void OnExecute();

        public virtual bool CheckFinished()
        {
            if (scheduled && jobHandle.IsCompleted)
            {
                return true;
            }
            return false;
        }

        protected virtual void OnFinish()
        {
            jobHandle.Complete();
            finishCallback?.Invoke(this);

            foreach (var job in followingTasks)
            {
                // 1 dependency have been solved.
                job.dependenciesCount -= 1;
                if (job.dependenciesCount <= 0)
                {
                    job.Execute();
                }
            }
        }
    }
}
