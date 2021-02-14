using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PreviewWorld : World
{
    public Matryoshka.MatryoshkaGraph graphForPreview;

    protected override void SetWorld()
    {
        // Setup generators
        chunkGenerator = (ChunkGenerator)System.Activator.CreateInstance(generatorType);
        UpdateMgr.Init(this, cs_generation, cs_generation_batchsize);

        GenerateTestStructure();
        worldGeneratingCoroutine = StartCoroutine(WorldUpdateCoroutine());
    }

    public void GenerateTestStructure()
    {
        Vector3Int origin = Vector3Int.zero + Vector3Int.up * 64;
        BoundsInt b = this.graphForPreview.GetBounds(origin);
        CustomJobs.CustomJob.TryAddJob(new WorldGen.GenericStructureGeneration(this, this.graphForPreview.NewGenerator(), b));
    }

    [ContextMenu("Refresh preview")]
    public void Refresh()
    {
        if(CustomJobs.CustomJob.Count == 0)
        {
            // TODO: Do this more elegantly
            ClearAllImmediate();
            SetWorld();
        }
        else
        {
            Debug.LogError("Preview refresh failed - Unfinished job exists");
        }
    }
}
