using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;

namespace GDMC.MCMC
{
    public class PatternWeights
    {
        public float eps;
        Dictionary<string, float> weightDict;

        public PatternWeights(float eps = 0.2f)
        {
            this.eps = eps;
            weightDict = new Dictionary<string, float>();
        }

        public void AddPatternOnce(string pattern, float weight = 1.0f)
        {
            if(weightDict.ContainsKey(pattern))
            {
                weightDict[pattern] += weight;
            }
            else
            {
                weightDict.Add(pattern, weight);
            }
        }

        public float GetWeight(string pattern)
        {
            float weight = eps;
            if(weightDict.TryGetValue(pattern, out weight))
            {
                return weight;
            }

            return eps;
        }

        // TODO: 3D patterns ... ?
        public static string GetPatternStr(char[,] map, int maplenX, int maplenY, int xorg, int yorg, int dx, int dy, bool flipX = false, bool flipY = false, int rotation = 0)
        {
            if(dx <= 0 || dy <= 0) { return "ERROR"; }

            char[] result = new char[dx * dy];

            for(int x = 0; x < dx; x++)
            {
                for(int y = 0; y < dy; y++)
                {
                    int _x = x, _y = y;

                    // rotation
                    switch(rotation)
                    {
                        case 1:
                            _x = y;
                            _y = dx - x - 1;
                            break;

                        case 2:
                            _x = dx - x - 1;
                            _y = dy - y - 1;
                            break;

                        case 3:
                            _x = dy - y - 1;
                            _y = x;
                            break;
                    }

                    // flip
                    if (flipX) { _x = dx - _x - 1; }
                    if (flipY) { _y = dy - _y - 1; }

                    // transform
                    _x += xorg;
                    _y += yorg;

                    // periodic
                    if(_x < 0) { _x += maplenX; }
                    else if(_x >= maplenX) { _x -= maplenX; }

                    if (_y < 0) { _y += maplenY; }
                    else if (_y >= maplenX) { _y -= maplenY; }

                    // embed; Treat 'o' as '*'
                    if(map[_x, _y] == 'o') { result[x * dy + y] = '*'; }
                    else { result[x * dy + y] = map[_x, _y]; }
                }
            }

            return new string(result);
        }

        public static string GetPatternStr(char[,,] map, int maplenX, int maplenY, int xorg, int yorg, int dx, int dy, bool flipX = false, bool flipY = false, int rotation = 0)
        {
            // TODO: Implement 3D Patterns
            return "BAD_PATTERN";
        }
    }

    // Plain Metropolis-Hastings with uniform proposals
    public class SimpleMCMC
    {
        System.Random random;

        PatternWeights weights;
        public char[,] map { get; protected set; }
        char[] charset;

        int maplenX, maplenY;
        int N;

        public int success = 0, reject = 0, successRNG = 0;

        public double temperature = 1.0;

        public struct Modification
        {
            public int x, y;
            public char to;
        }

        public SimpleMCMC(PatternWeights weights, char[,] input, char[] charset, int maplenX, int maplenY, int neighbor = 3)
        {
            this.weights = weights;
            this.map = input;

            this.charset = charset;

            this.maplenX = maplenX;
            this.maplenY = maplenY;

            N = neighbor;

            random = new System.Random();

            RefreshEnergy();
        }

        private void RefreshEnergy()
        {
            for(int x = 0; x < maplenX; x++)
            {
                for(int y = 0; y < maplenY; y++)
                {
                    prevEnergy += - (float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, x, y, N, N)));
                }
            }
        }

        float prevEnergy = 0;

        // True: something has been modified
        // False: rejected, nothing changed
        public bool Step(ref Modification modification)
        {
            int x = random.Next(0, maplenX);
            int y = random.Next(0, maplenY);

            // This grid is preserved, cannot be modified
            if(map[x, y] == 'o') { return false; }

            int to = random.Next(0, charset.Length);

            float Eorg = 0, Enew = 0;
            char Corg = map[x, y];

            // Previous energy
            for (int sx = x - N + 1; sx <= x + N - 1; sx++)
            {
                for (int sy = y - N + 1; sy <= y + N - 1; sy++)
                {
                    Eorg += - (float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, sx, sy, N, N)));
                }
            }

            // Replace and calculate new energy
            map[x, y] = charset[to];

            for (int sx = x - N + 1; sx <= x + N - 1; sx++)
            {
                for (int sy = y - N + 1; sy <= y + N - 1; sy++)
                {
                    Enew += -(float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, sx, sy, N, N)));
                }
            }

            // Convert difference in energy to true energy using prevEnergy = Eorg
            Enew = prevEnergy - Eorg + Enew;
            Eorg = prevEnergy;

            // Reject ?
            if(Enew < Eorg)
            {
                modification.x = x;
                modification.y = y;
                modification.to = charset[to];

                success++;

                return true;
            }
            else
            {
                // Sample from uniform RNG
                double u = random.NextDouble();
                if(u < Math.Exp(-(Enew - Eorg) / temperature))
                {
                    modification.x = x;
                    modification.y = y;
                    modification.to = charset[to];

                    successRNG++;

                    return true;
                }
            }

            // Rejected, reverse modification
            reject++;
            map[x, y] = Corg;
            return false;
        }

        public void ResetCounters()
        {
            reject = 0;
            success = 0;
            successRNG = 0;
        }

        public List<Modification> BatchedStep(int iter = 10)
        {
            List<Modification> modifications = new List<Modification>();

            Modification m = new Modification();
            for(int i = 0; i < iter; i++)
            {
                if(Step(ref m))
                {
                    modifications.Add(m);
                }
            }

            return modifications;
        }
    }

    public class PatternSimpleMCMCVisualizer : Voxelis.IStructureGenerator
    {
        //PatternWeights weights;
        //int[,] input;
        SimpleMCMC instance;

        protected Dictionary<char, Block> blockSet = new Dictionary<char, Block>();

        public int epochs = 100, iters = 10;

        public void SetupBlocks()
        {
            blockSet.Add(' ', new Block { id = 0xffff, meta = 0x000f });
            blockSet.Add('*', new Block { id = 0xffff, meta = 0xffff });
            blockSet.Add('o', new Block { id = 0xffff, meta = 0x07ff });
            blockSet.Add('r', new Block { id = 0xffff, meta = 0xf00f });
            blockSet.Add('g', new Block { id = 0xffff, meta = 0x0f0f });
            blockSet.Add('b', new Block { id = 0xffff, meta = 0x00ff });
        }

        public virtual void Init(object instance)
        {
            this.instance = instance as SimpleMCMC;

            SetupBlocks();

            Debug.Log("MCMC Visualizer inited");
        }

        public virtual void Generate(BoundsInt bound, World world)
        {
            // init
            for (int x = 0; x < bound.size.x; x++) 
                for (int y = 0; y < bound.size.z; y++)
                {
                    world.SetBlock(new Vector3Int(x + bound.min.x, bound.max.y - 1, y + bound.min.z), blockSet[instance.map[x, y]]);
                }

            Debug.Log("BitMap Initialized");

            instance.ResetCounters();

            for (int i = 0; i < epochs; i++)
            {
                var ms = instance.BatchedStep(iters);
                foreach(var m in ms)
                {
                    world.SetBlock(new Vector3Int(m.x + bound.min.x, bound.max.y - 1, m.y + bound.min.z), blockSet[m.to]);
                }

                if((i % ((int)epochs / 10)) == 0)
                {
                    Debug.Log($"Ep {i} Finished, Success {instance.success + instance.successRNG} (RNG {instance.successRNG}); Reject {instance.reject}");
                }
            }

            Debug.Log("MCMC Finished");
        }
    }
}
