#define PROFILE

using UnityEngine;
using System.Collections;

using Assets.FractalSystemCore;
using Assets.FractalSystemCore.NodeInherits;
using Voxelis;

public class SplineCyclinder
{
    

    public SplineCyclinder()
    {

    }
}

public class TestTree : IStructureGenerator
{
    public int iterationMax = 3;
    public float iterGrowRate = 1.0f;
    public float startGrowRate = 64.0f;
    public float scale = 1.0f;

    uint trunkBlock, branchBlock;
    LeafRenderingSetup leafSetup;

    public float trunkLen = 1.0f;
    public float crownWidth = 1.0f, rootScale = 1.35f, endRadiusScale = 0.15f;

    FractalSystemNode startNode;

    public struct LeafRenderingSetup
    {
        public bool isNoisy;
        public FastNoiseLite noise;
        public uint leaf, leafVariant;
        public float variantRate, noiseScale;
    }

    public static LeafRenderingSetup GetDefaultLeafRenderingSetup()
    {
        return new LeafRenderingSetup() {
            isNoisy = false,
            noise = null,
            leaf = (uint)Blocks.leaf,
            leafVariant = 0x000000ff,
            variantRate = 0.0f,
            noiseScale = 1.0f
        };
    }

    public TestTree(int iterMax = 3, float scale = 1.0f, float growRate = 64.0f)
    {
        this.iterationMax = iterMax;
        this.scale = scale;
        this.startGrowRate = growRate;

        trunkBlock = (uint)Blocks.wood;
        branchBlock = (uint)Blocks.wood;
        leafSetup = GetDefaultLeafRenderingSetup();
    }

    public TestTree SetGrowth(float growth)
    {
        this.startGrowRate = growth;
        return this;
    }

    public TestTree SetShape(float trunkLen = 1.0f, float crownWidth = 1.0f, float rootScale = 1.35f, float endRadiusScale = 0.15f)
    {
        this.trunkLen = trunkLen;
        this.crownWidth = crownWidth;
        this.rootScale = rootScale;
        this.endRadiusScale = endRadiusScale;
        return this;
    }

    public TestTree SetColors(uint trunk, uint branch, uint leaf)
    {
        trunkBlock = trunk;
        branchBlock = branch;
        leafSetup.leaf = leaf;

        return this;
    }

    public TestTree SetLeaf(LeafRenderingSetup setup)
    {
        leafSetup = setup;

        return this;
    }

    public static void FillCyclinder(World world, uint id, Vector3 start, Vector3 end, float startRadius, float endRadius, Vector3 startNormal, Vector3 endNormal)
    {
        Vector3 mainDirc = (end - start).normalized;
        startNormal = startNormal.normalized;
        endNormal = endNormal.normalized;

        float scanLen = 0.0f;
        string scanAxis = "";
        int scanAxisDirc = 0;

        // Determine main scan direction
        float max = (Mathf.Abs(mainDirc.x) > Mathf.Abs(mainDirc.y)) ? (Mathf.Abs(mainDirc.z) > Mathf.Abs(mainDirc.x) ? mainDirc.z : mainDirc.x) : ((Mathf.Abs(mainDirc.z) > Mathf.Abs(mainDirc.y) ? mainDirc.z : mainDirc.y));

        Vector3 scanLineDirc = Vector3.zero;
        if(max == mainDirc.x)
        {
            if(max < 0) { scanLineDirc = Vector3.left; scanAxisDirc = -1; }
            else { scanLineDirc = Vector3.right; scanAxisDirc = 1; }

            scanLen = Mathf.Abs(end.x - start.x);
            scanAxis = "x";
        }
        else if(max == mainDirc.y)
        {
            if (max < 0) { scanLineDirc = Vector3.down; scanAxisDirc = -1; }
            else { scanLineDirc = Vector3.up; scanAxisDirc = 1; }

            scanLen = Mathf.Abs(end.y - start.y);
            scanAxis = "y";
        }
        else
        {
            if (max < 0) { scanLineDirc = Vector3.back; scanAxisDirc = -1; }
            else { scanLineDirc = Vector3.forward; scanAxisDirc = 1; }
         
            scanLen = Mathf.Abs(end.z - start.z);
            scanAxis = "z";
        }

        // Begin (center) of the scan
        float startScanOffset = startRadius;
        Vector3 scanBegin = start - mainDirc * startScanOffset - scanLineDirc;

        // End of the scan
        float endScanOffset = endRadius;
        Vector3 scanEnd = end + mainDirc * endScanOffset + scanLineDirc;

        // Start scan
        int scanCount = 0, scanBeginAxisCoord = 0, scanEndAxisCoord = 0;
        if (scanAxis == "x")
        {
            scanCount = Mathf.Abs((int)(scanEnd.x - scanBegin.x));
            scanBeginAxisCoord = (int)scanBegin.x;
            scanEndAxisCoord = (int)scanEnd.x;
        }
        else if (scanAxis == "y") 
        { 
            scanCount = Mathf.Abs((int)(scanEnd.y - scanBegin.y));
            scanBeginAxisCoord = (int)scanBegin.y;
            scanEndAxisCoord = (int)scanEnd.y;
        }
        else 
        { 
            scanCount = Mathf.Abs((int)(scanEnd.z - scanBegin.z));
            scanBeginAxisCoord = (int)scanBegin.z;
            scanEndAxisCoord = (int)scanEnd.z;
        }

        // Scanner loop
        Vector3 sCenter, blockCenter, toCenter, toS, toE;
        Vector3Int sCenterI, blockPos;

        for (int i = scanBeginAxisCoord; i != scanEndAxisCoord; i += scanAxisDirc)
        {
            // Calculate where are we
            float sA, eA;
            if (scanAxis == "x") { sA = start.x; eA = end.x; }
            else if(scanAxis == "y") { sA = start.y; eA = end.y; }
            else { sA = start.z; eA = end.z; }

            float t = (i - sA) / (eA - sA);
            float ct = Mathf.Clamp(t, 0, 1);

            // Center
            sCenter = (1 - t) * start + t * end;
            sCenterI = Vector3Int.RoundToInt(sCenter);

            // Radius
            float sRadius = (1 - ct) * startRadius + ct * endRadius;

            // Radius (projected)
            float sPRadius = sRadius / (Vector3.Dot(mainDirc, scanLineDirc));
            int sPRadiusI = Mathf.CeilToInt(sPRadius);

            // Fill the circle
            for(int j = -sPRadiusI; j <= sPRadiusI; j++)
            {
                for(int k = -sPRadiusI; k <= sPRadiusI; k++)
                {
                    // Compute our current position
                    blockPos = new Vector3Int(sCenterI.x, sCenterI.y, sCenterI.z);

                    if (scanAxis == "x")
                    {
                        blockPos.x = sCenterI.x;
                        blockPos.y = sCenterI.y + j;
                        blockPos.z = sCenterI.z + k;
                    }
                    else if (scanAxis == "y")
                    {
                        blockPos.x = sCenterI.x + j;
                        blockPos.y = sCenterI.y;
                        blockPos.z = sCenterI.z + k;
                    }
                    else
                    {
                        blockPos.x = sCenterI.x + j;
                        blockPos.y = sCenterI.y + k;
                        blockPos.z = sCenterI.z;
                    }

                    blockCenter = blockPos + Vector3.one * 0.5f;

                    // Check inside the circle or not
                    toCenter = blockCenter - sCenter;
                    toS = blockCenter - start;
                    toE = blockCenter - end;

                    float radShift = Vector3.Dot(toCenter, mainDirc);
                    float r = radShift * radShift + sRadius * sRadius;

                    if(toCenter.sqrMagnitude <= r + 0.6)
                    {
                        // Check normals
                        if(Vector3.Dot(toS, startNormal) > 0 && Vector3.Dot(toE, endNormal) < 0)
                            world.SetBlock(blockPos, Block.From32bitColor(id));
                    }
                }
            }
        }
    }

    public static void FillLeaf(World world, LeafRenderingSetup setup, Vector3 o, float r)
    {
        if(setup.isNoisy)
        {
            FillNoisySphere(world, setup.leaf, o, r, setup.noise, setup.noiseScale, setup.leafVariant, setup.variantRate);
        }
        else
        {
            FillSphere(world, setup.leaf, o, r);
        }
    }

    public static void FillSphere(World world, uint id, Vector3 o, float r)
    {
        Vector3Int min = Vector3Int.FloorToInt(o) - Vector3Int.one * Mathf.CeilToInt(r);
        Vector3Int max = Vector3Int.CeilToInt(o) + Vector3Int.one * Mathf.CeilToInt(r);

        for(int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    if((new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - o).magnitude <= (r + 0.35f))
                    {
                        world.SetBlock(new Vector3Int(x, y, z), Block.From32bitColor(id));
                    }
                }

    }

    public static void FillNoisySphere(World world, uint id, Vector3 o, float r, FastNoiseLite noise, float noiseScale, uint idVariant, float variantRate)
    {
#if PROFILE
        UnityEngine.Profiling.Profiler.BeginSample("TestTree.FillNoisySphere");
#endif
        var rand = new System.Random();

        Vector3Int min = Vector3Int.FloorToInt(o) - Vector3Int.one * Mathf.CeilToInt(r + noiseScale);
        Vector3Int max = Vector3Int.CeilToInt(o) + Vector3Int.one * Mathf.CeilToInt(r + noiseScale);

        for (int x = min.x; x <= max.x; x++)
            for (int y = min.y; y <= max.y; y++)
                for (int z = min.z; z <= max.z; z++)
                {
                    if ((new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - o).magnitude <= (r + 0.35f + noise.GetNoise(x, y, z) * noiseScale))
                    {
                        if(rand.NextDouble() < variantRate)
                        {
                            world.SetBlock(new Vector3Int(x, y, z), Block.From32bitColor(idVariant));
                        }
                        else
                        {
                            world.SetBlock(new Vector3Int(x, y, z), Block.From32bitColor(id));
                        }
                    }
                }

#if PROFILE
        UnityEngine.Profiling.Profiler.EndSample();
#endif
    }

    public void Generate(BoundsInt bound, World world)
    {
        Vector3 dirc = (bound.max - bound.min);
        //TestTree.FillCyclinder(world, (uint)Blocks.wood, bound.min, bound.max, 4, 2, dirc.normalized, dirc.normalized);

        System.Random random = new System.Random((bound.x << 16) ^ (bound.y << 8) ^ (bound.z));

        startNode = new TreeVer4_Ultra(random, this.trunkLen, crownWidth, rootScale, endRadiusScale);
        startNode.growRate = startGrowRate;

        startNode.init();

        Descript(startNode, 0);

        FractalRenderState state = new FractalRenderState();
        state.position = new Vector3(bound.center.x, bound.min.y, bound.center.z);
        state.rotation = Quaternion.identity;
        state.scale = this.scale;

        Render(world, state, startNode);
    }

    public void Descript(FractalSystemNode node, int depth)
    {
        // Check terminating conditions
        if (node.growRate < iterGrowRate)
        {
            return;
        }
        if (depth >= iterationMax)
        {
            node.ClearNode();
            return;
        }

        // Expand the node if it have not been expanded.
        if (node.child.Count == 0)
        {
            node.generateChildren();
        }

        foreach (FractalSystemNode child in node.child)
        {
            Descript(child, depth + 1);
        }
    }

    void Render(World world, FractalRenderState state, FractalSystemNode node)
    {
        ((TreeVer4_Ultra)node)?.SetColors(trunkBlock, branchBlock, leafSetup.leaf);
        ((TreeVer4_Ultra)node)?.SetLeaf(leafSetup);
        node.Express(world, ref state);

        foreach (FractalSystemNode child in node.child)
        {
            Render(world, state, child);
        }
    }
}