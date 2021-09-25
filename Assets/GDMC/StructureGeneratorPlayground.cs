using GDMC.MCMC;
using System.Collections;
using System.Collections.Generic;
using TypeReferences;
using UnityEngine;
using Voxelis;
using Voxelis.WorldGen;

public class StructureGeneratorPlayground : MonoBehaviour
{
    public World world;

    [Inherits(typeof(IStructureGenerator))]
    public TypeReference structureGeneratorType;

    IStructureGenerator generator;
    public BoundsInt generationBound;

    public Texture2D patternTex;
    public Texture2D inputTex;
    public int N = 3;
    public float eps = 0.2f;
    public char[] charset = new char[5] { ' ', '*', 'r', 'g', 'b' };

    public int epochs = 100, itersPerEp = 10;
    public double temperature = 1.0;

    // Start is called before the first frame update
    void Start()
    {
        generator = (IStructureGenerator)System.Activator.CreateInstance(structureGeneratorType);

        if (generator is ConnectiveSimpleMCMCVisualizer)
        {
            // Read pattern
            PatternWeights weights = new PatternWeights(eps);
            int plX, plY;
            var patternMap = Tex2DToCharArray(patternTex, out plX, out plY);
            for (int x = 0; x < plX; x++)
            {
                for (int y = 0; y < plY; y++)
                {
                    weights.AddPatternOnce(PatternWeights.GetPatternStr(patternMap, plX, plY, x, y, N, N));
                    weights.AddPatternOnce(PatternWeights.GetPatternStr(patternMap, plX, plY, x, y, N, N, true));
                    weights.AddPatternOnce(PatternWeights.GetPatternStr(patternMap, plX, plY, x, y, N, N, false, true));
                    weights.AddPatternOnce(PatternWeights.GetPatternStr(patternMap, plX, plY, x, y, N, N, true, true));

                    // TODO: rotations ?
                }
            }

            Debug.Log("Pattern Generated");

            int clX, clY;
            var inputMap = Tex2DToCharArray(inputTex, out clX, out clY);

            System.Random random = new System.Random();

            // Convert inputmap to masked noise
            for (int x = 0; x < clX; x++)
            {
                for (int y = 0; y < clY; y++)
                {
                    if (inputMap[x, y] == ' ')
                    {
                        inputMap[x, y] = charset[random.Next(0, charset.Length)];
                    }
                    else if (inputMap[x, y] == '*')
                    {
                        inputMap[x, y] = 'o';
                    }
                }
            }

            ConnectiveSimpleMCMC mcmc = new ConnectiveSimpleMCMC(weights, inputMap, charset, clX, clY, N);
            mcmc.temperature = temperature;

            (generator as ConnectiveSimpleMCMCVisualizer).epochs = epochs;
            (generator as ConnectiveSimpleMCMCVisualizer).iters = itersPerEp;
            (generator as ConnectiveSimpleMCMCVisualizer).Init(mcmc);
        }

        if (generator is GDMC.Alfheim_Prototype)
        {
            int clX, clY;
            (generator as GDMC.Alfheim_Prototype).streetMap = Tex2DToCharArray(inputTex, out clX, out clY);
            (generator as GDMC.Alfheim_Prototype).temperature = temperature;
            (generator as GDMC.Alfheim_Prototype).mcmc_epochs = epochs;
            (generator as GDMC.Alfheim_Prototype).mcmc_iters = itersPerEp;
        }
    }

    public char[,] Tex2DToCharArray(Texture2D tex, out int maplenX, out int maplenY)
    {
        maplenX = tex.width;
        maplenY = tex.height;
        char[,] res = new char[tex.width, tex.height];

        for (int x = 0; x < tex.width; x++)
        {
            for (int y = 0; y < tex.height; y++)
            {
                Color color = tex.GetPixel(x, y);

                if(color == Color.black) { res[x, y] = ' '; }
                if(color == Color.white) { res[x, y] = '*'; }
                if(color ==   Color.red) { res[x, y] = 'r'; }
                if(color == Color.green) { res[x, y] = 'g'; }
                if(color ==  Color.blue) { res[x, y] = 'b'; }
            }
        }

        return res;
    }

    // Update is called once per frame
    //void Update()
    //{
    //}

    [ContextMenu("Refresh")]
    public void Refresh()
    {
        AlignToGrid();

        var job = new Voxelis.WorldGen.GenericStructureGeneration(world, generator, generationBound);
        Voxelis.CustomJobs.CustomJob.TryAddJob(job);
    }

    [ContextMenu("ShowColors")]
    public void ShowColors()
    {
        (generator as ConnectiveSimpleMCMCVisualizer)?.ShowColors();
    }

    [ContextMenu("ShowConnectivity")]
    public void ShowConnectivity()
    {
        (generator as ConnectiveSimpleMCMCVisualizer)?.ShowConnectivity();
    }

    void AlignToGrid()
    {
        transform.position = Vector3Int.FloorToInt(transform.position);
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.Max(Vector3Int.RoundToInt(transform.localScale), Vector3.one);

        generationBound.min = Vector3Int.FloorToInt(transform.position - (transform.localScale / 2.0f));
        generationBound.max = Vector3Int.CeilToInt(transform.position + (transform.localScale / 2.0f));
    }

    private void OnDrawGizmosSelected()
    {
        AlignToGrid();

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(generationBound.center, generationBound.size);
    }
}
