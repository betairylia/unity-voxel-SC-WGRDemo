using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis.Rendering;

public class GPUDispatchManager
{
    private static GPUDispatchManager _Singleton;
    public static GPUDispatchManager Singleton
    {
        get
        {
            if(_Singleton == null)
            {
                _Singleton = new GPUDispatchManager();
            }
            return _Singleton;
        }
    }

    private LinkedList<KeyValuePair<UnityEngine.Rendering.GraphicsFence, LinkedList<ChunkRenderer>>> fenceCBs;
    private int maxTasksInFence;

    private LinkedList<ChunkRenderer> tmp;
    private int currentTasksCount;

    private bool rubbish = false;

    private GPUDispatchManager(int maxTasksInFence = 256)
    {
        if(!SystemInfo.supportsGraphicsFence)
        {
            Debug.LogWarning("System does not support Graphics Fence !! Using available fallback.");
            rubbish = true;
        }

        if(!SystemInfo.supportsAsyncCompute)
        {
            Debug.LogWarning("System does not support Async compute !! Using available fallback.");
            rubbish = true;
        }

        fenceCBs = new LinkedList<KeyValuePair<UnityEngine.Rendering.GraphicsFence, LinkedList<ChunkRenderer>>>();
        this.maxTasksInFence = maxTasksInFence;
        
        ResetQueue();
    }

    public void AppendTask(ChunkRenderer r)
    {
        tmp.AddLast(r);
        currentTasksCount += 1;

        if(currentTasksCount > maxTasksInFence)
        {
            SyncTasks();
        }
    }

    public void SyncTasks()
    {
        //Debug.Log($"Syncing {currentTasksCount} tasks.");

        // Create GPU Fence
        UnityEngine.Rendering.GraphicsFence fence = Graphics.CreateAsyncGraphicsFence();
        fenceCBs.AddLast(new KeyValuePair<UnityEngine.Rendering.GraphicsFence, LinkedList<ChunkRenderer>>(fence, tmp));

        ResetQueue();
    }

    public void CheckTasks()
    {
        // Check from the earilest one
        var kv = fenceCBs.First;
        while (kv != null)
        {
            if(rubbish || kv.Value.Key.passed) // If rubbish, just go ahead since 1 frame has been passed. Shouldn't our compute already finished since rendering is done?
            {
                // We good
                //Debug.Log($"Fence passed.");

                foreach(var r in kv.Value.Value)
                {
                    r.OnBufferReady();
                }
                
                // Remove this node and move to the next
                var n_kv = kv.Next;
                fenceCBs.Remove(kv);
                kv = n_kv;
            }
            else
            {
                // Everything after me is not going to be finished.
                break;
            }
        }
    }

    private void ResetQueue()
    {
        tmp = new LinkedList<ChunkRenderer>();
        currentTasksCount = 0;
    }
}