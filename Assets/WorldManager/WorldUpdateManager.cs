using UnityEngine;
using System.Collections;
using CustomJobs;
using WorldGen;

public class WorldUpdateManager
{
    public void Init(
        ComputeShader cs_generation,
        int cs_generation_batchsize)
    {
        GeometryIndependentPass.cs_generation = cs_generation;
        GeometryIndependentPass.cs_generation_batchsize = cs_generation_batchsize;
        GeometryIndependentPass.Init();
    }

    public void Update()
    {
        GeometryIndependentPass.Update();

        CustomJobs.CustomJob.UpdateAllJobs();
    }

    public void Destroy()
    {
        GeometryIndependentPass.Destroy();
    }
}
