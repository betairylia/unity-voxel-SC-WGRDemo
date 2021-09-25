using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;
using C5;

namespace GDMC.MCMC
{
    // Plain Metropolis-Hastings with uniform proposals
    public class ConnectiveSimpleMCMC
    {
        System.Random random;

        PatternWeights weights;

        public enum Direction
        {
            Up = 0,
            Down = 1,
            Left = 2,
            Right = 3
        }

        public readonly Vector2Int[] dirc_dxy =
        {
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0)
        };
        public readonly int[] dirc_dx = { 0, 0, -1, 1 };
        public readonly int[] dirc_dy = { 1, -1, 0, 0 };
        public readonly Direction[] possibleDirections =
        {
            Direction.Up,
            Direction.Down,
            Direction.Left,
            Direction.Right
        };

        public readonly Direction[] reverseDirections =
        {
            Direction.Down,
            Direction.Up,
            Direction.Right,
            Direction.Left
        };

        public struct ConnectivityInfo
        {
            public int distance;
            public Direction next;

            public ConnectivityInfo(int dist, Direction next)
            {
                this.distance = dist;
                this.next = next;
            }
        }

        public Dictionary<char, int> blockConnectivities = new Dictionary<char, int>();

        public struct BFSKeys : IComparable
        {
            public Vector2Int pos;
            public int distance;

            public int CompareTo(object obj)
            {
                return distance - ((BFSKeys)obj).distance;
            }

            public BFSKeys(int x, int y, int d)
            {
                this.pos = new Vector2Int(x, y);
                this.distance = d;
            }
        }

        public char[,] map { get; protected set; }
        public ConnectivityInfo[] connectivityMap { get; protected set; }
        ConnectivityInfo[] connectivityMap_backup;
        C5.IntervalHeap<BFSKeys> pQueue = new IntervalHeap<BFSKeys>();
        Queue<Vector2Int> refreshBFSQueue = new Queue<Vector2Int>();
        System.Collections.Generic.HashSet<Vector2Int> BFSModifiedPoints = new System.Collections.Generic.HashSet<Vector2Int>();

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

        public ConnectiveSimpleMCMC(PatternWeights weights, char[,] input, char[] charset, int maplenX, int maplenY, int neighbor = 3)
        {
            this.weights = weights;
            this.map = input;
            this.connectivityMap = new ConnectivityInfo[maplenX * maplenY];
            this.connectivityMap_backup = new ConnectivityInfo[maplenX * maplenY];

            this.charset = charset;

            this.maplenX = maplenX;
            this.maplenY = maplenY;

            N = neighbor;

            random = new System.Random();

            InitializeBlockConnectivities();
            InitializeConnectivity();
            RefreshEnergy();
        }

        private int flatten(int x, int y)
        {
            return x * maplenY + y;
        }

        private bool CheckLoop(Vector2Int coord)
        {
            Vector2Int origin = coord;

            System.Collections.Generic.HashSet<Vector2Int> visited = new System.Collections.Generic.HashSet<Vector2Int>();
            visited.Add(coord);

            while(map[coord.x, coord.y] != 'o')
            {
                coord += dirc_dxy[(int)(connectivityMap[flatten(coord.x, coord.y)].next)];

                if(visited.Contains(coord))
                {
                    return true;
                }

                visited.Add(coord);
            }

            return false;
        }

        private float GetConnectivityEnergy(float distance)
        {
            // Final likelihood (energy exp'd): multiplier (inside log) * (1 / (distance + dOffset))

            //float multiplier = (float)System.Math.Log(5.0f);
            float multiplier = 2.0f;
            int dOffset = 3;

            //return (float)System.Math.Log(distance + dOffset) - multiplier;
            return (float)System.Math.Log(distance + dOffset) * multiplier;
        }

        private float GetConnectivityEnergy(int x, int y)
            => GetConnectivityEnergy(connectivityMap[flatten(x, y)].distance);

        private void RefreshEnergy()
        {
            for (int x = 0; x < maplenX; x++)
            {
                for (int y = 0; y < maplenY; y++)
                {
                    prevEnergy += -(float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, x, y, N, N)));

                    prevEnergy += GetConnectivityEnergy(x, y);
                }
            }
        }

        private void InitializeConnectivity()
        {
            // Initialize distance

            if (!pQueue.IsEmpty) { pQueue = new IntervalHeap<BFSKeys>(); Debug.LogError("pQueue non empty !"); }

            for (int x = 0; x < maplenX; x++)
            {
                for (int y = 0; y < maplenY; y++)
                {
                    // Set initial value
                    connectivityMap[flatten(x, y)] = new ConnectivityInfo(9999999, Direction.Up);

                    // Preserved grid as major roads
                    if (map[x, y] == 'o')
                    {
                        connectivityMap[flatten(x, y)].distance = 0;
                        pQueue.Add(new BFSKeys(x, y, 0));
                    }
                }
            }

            while (!pQueue.IsEmpty)
            {
                var curr = pQueue.DeleteMin();

                // Iterate all possible directions
                foreach (var d in possibleDirections)
                {
                    Vector2Int newpos = curr.pos + dirc_dxy[(int)d];
                    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY) { continue; }

                    // Check if neighbor can achieve shorter path by pointing to self
                    // ( Relax )
                    int newdist = curr.distance + blockConnectivities[map[curr.pos.x, curr.pos.y]];
                    if (connectivityMap[flatten(newpos.x, newpos.y)].distance > newdist)
                    {
                        // Set distance and next pointer
                        connectivityMap[flatten(newpos.x, newpos.y)].distance = newdist;
                        connectivityMap[flatten(newpos.x, newpos.y)].next = reverseDirections[(int)d];

                        pQueue.Add(new BFSKeys(newpos.x, newpos.y, newdist));
                    }
                }
            }
        }

        private void InitializeBlockConnectivities()
        {
            // Initialize block connectivities

            blockConnectivities.Add(' ', 7);
            blockConnectivities.Add('*', 1);
            blockConnectivities.Add('o', 1);
            blockConnectivities.Add('r', 2);
            blockConnectivities.Add('g', 5);
            blockConnectivities.Add('b', 5);
        }

        float prevEnergy = 0;

        // True: something has been modified
        // False: rejected, nothing changed
        public bool Step(ref Modification modification)
        {
            int x = random.Next(0, maplenX);
            int y = random.Next(0, maplenY);

            // This grid is preserved, cannot be modified
            if (map[x, y] == 'o') { return false; }

            int to = random.Next(0, charset.Length);

            float Eorg = 0, Enew = 0;
            char Corg = map[x, y];

            // Previous energy
            for (int sx = x - N + 1; sx <= x + N - 1; sx++)
            {
                for (int sy = y - N + 1; sy <= y + N - 1; sy++)
                {
                    Eorg += -(float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, sx, sy, N, N)));
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

            // Connectivity energy
            #region Connectivity

            connectivityMap.CopyTo(connectivityMap_backup, 0);

            // Initialize distance

            if (!pQueue.IsEmpty) { pQueue = new IntervalHeap<BFSKeys>(); Debug.LogError("pQueue non empty !"); }
            BFSModifiedPoints.Clear();
            refreshBFSQueue.Clear();

            refreshBFSQueue.Enqueue(new Vector2Int(x, y));

            //pQueue.Add(new BFSKeys(x, y, connectivityMap[flatten(x, y)].distance));

            //foreach (var d in possibleDirections)
            //{
            //    Vector2Int newpos = new Vector2Int(x, y) + dirc_dxy[(int)d];
            //    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY) { continue; }

            //    pQueue.Add(new BFSKeys(newpos.x, newpos.y, connectivityMap[flatten(newpos.x, newpos.y)].distance));
            //}

            // Recalculate distances w.r.t. existing paths and new block
            while (refreshBFSQueue.Count > 0)
            {
                Vector2Int pt = refreshBFSQueue.Dequeue();
                int dist = connectivityMap[flatten(pt.x, pt.y)].distance;

                pQueue.Add(new BFSKeys(pt.x, pt.y, dist));

                // No this issue now
                //if (BFSModifiedPoints.Contains(new Vector2Int(pt.x, pt.y)))
                //{
                //    Debug.LogError("Loops exist in connectivityMap !!!!");
                //    break;
                //}

                BFSModifiedPoints.Add(new Vector2Int(pt.x, pt.y));

                // Iterate all possible directions
                foreach (var d in possibleDirections)
                {
                    Vector2Int newpos = pt + dirc_dxy[(int)d];
                    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY) { continue; }

                    if (map[newpos.x, newpos.y] == 'o') { continue; }

                    // Check if neighbor's next is self
                    if (connectivityMap[flatten(newpos.x, newpos.y)].next == reverseDirections[(int)d])
                    {
                        connectivityMap[flatten(newpos.x, newpos.y)].distance =
                            dist + blockConnectivities[map[pt.x, pt.y]];

                        refreshBFSQueue.Enqueue(newpos);
                    }
                }
            }

            // Relax
            while (!pQueue.IsEmpty)
            {
                var curr = pQueue.DeleteMin();
                bool updated = false;

                // Iterate all possible directions
                foreach (var d in possibleDirections)
                {
                    Vector2Int newpos = curr.pos + dirc_dxy[(int)d];
                    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY) { continue; }

                    //if(d == connectivityMap[flatten(curr.pos.x, curr.pos.y)].next) { continue; }

                    // Check if self can get shorter path by pointing to new neighbor
                    int newdist =
                        connectivityMap[flatten(newpos.x, newpos.y)].distance +
                        blockConnectivities[map[newpos.x, newpos.y]];

                    if (newdist < connectivityMap[flatten(curr.pos.x, curr.pos.y)].distance)
                    {
                        connectivityMap[flatten(curr.pos.x, curr.pos.y)].distance = newdist;
                        connectivityMap[flatten(curr.pos.x, curr.pos.y)].next = d;

                        updated = true;
                    }

                    //// Check if neighbor can achieve shorter path by pointing to self
                    //// ( Relax )
                    //int newdist = curr.distance + blockConnectivities[map[curr.pos.x, curr.pos.y]];
                    //if (connectivityMap[flatten(newpos.x, newpos.y)].distance > newdist)
                    //{
                    //    // Set distance and next pointer
                    //    connectivityMap[flatten(newpos.x, newpos.y)].distance = newdist;
                    //    connectivityMap[flatten(newpos.x, newpos.y)].next = reverseDirections[(int)d];

                    //    if (CheckLoop(newpos))
                    //    {
                    //        Debug.LogError("I am creating loops, why ?");
                    //    }

                    //    pQueue.Add(new BFSKeys(newpos.x, newpos.y, newdist));
                    //    BFSModifiedPoints.Add(new Vector2Int(newpos.x, newpos.y));
                    //}
                }

                if (updated)
                {
                    foreach (var d in possibleDirections)
                    {
                        Vector2Int newpos = curr.pos + dirc_dxy[(int)d];
                        if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY) { continue; }

                        pQueue.Add(new BFSKeys(newpos.x, newpos.y, connectivityMap[flatten(newpos.x, newpos.y)].distance));
                        BFSModifiedPoints.Add(new Vector2Int(newpos.x, newpos.y));
                    }
                }
            }

            foreach (var pt in BFSModifiedPoints)
            {
                Eorg += GetConnectivityEnergy(connectivityMap_backup[flatten(pt.x, pt.y)].distance);
                Enew += GetConnectivityEnergy(connectivityMap[flatten(pt.x, pt.y)].distance);
            }

            #endregion

            // Convert difference in energy to true energy using prevEnergy = Eorg
            Enew = prevEnergy - Eorg + Enew;
            Eorg = prevEnergy;

            // Reject ?
            if (Enew < Eorg)
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
                if (u < Math.Exp(-(Enew - Eorg) / temperature))
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

            // Swap back buffers
            var tmp = connectivityMap;
            connectivityMap = connectivityMap_backup;
            connectivityMap_backup = tmp;

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
            for (int i = 0; i < iter; i++)
            {
                if (Step(ref m))
                {
                    modifications.Add(m);
                }
            }

            return modifications;
        }

        internal void Finish()
        {
            // Loop check
            for (int x = 0; x < maplenX; x++)
            {
                for (int y = 0; y < maplenY; y++)
                {
                    if(CheckLoop(new Vector2Int(x, y)))
                    {
                        Debug.LogError("Loops found");
                    }
                }
            }

            Debug.Log("Loop check finished");

            // Replace all black grids on road to 'red'

            bool[] visited = new bool[maplenX * maplenY];

            for (int x = 0; x < maplenX; x++)
            {
                for (int y = 0; y < maplenY; y++)
                {
                    // Set initial value
                    visited[flatten(x, y)] = false;

                    // ignore major roads
                    if (map[x, y] == 'o')
                    {
                        visited[flatten(x, y)] = true;
                    }
                }
            }

            for (int x = 0; x < maplenX; x++)
            {
                for (int y = 0; y < maplenY; y++)
                {
                    if (map[x, y] == '*' && visited[flatten(x, y)] == false)
                    {
                        Vector2Int pt = new Vector2Int(x, y);
                        while (map[pt.x, pt.y] != 'o' && visited[flatten(pt.x, pt.y)] == false)
                        {
                            if (map[pt.x, pt.y] == ' ')
                            {
                                map[pt.x, pt.y] = 'r';
                            }

                            visited[flatten(pt.x, pt.y)] = true;
                            pt += dirc_dxy[(int)connectivityMap[flatten(pt.x, pt.y)].next];
                        }
                    }
                }
            }

            InitializeConnectivity();
        }
    }

    public class ConnectiveSimpleMCMCVisualizer : IStructureGenerator
    {
        ConnectiveSimpleMCMC instance_n;

        protected Dictionary<char, Block> blockSet = new Dictionary<char, Block>();

        public int epochs = 100, iters = 10;

        BoundsInt bound;
        World world;

        public enum RenderMode
        {
            Connectivity,
            Color
        }

        public RenderMode renderMode;

        public void SetupBlocks()
        {
            blockSet.Add(' ', new Block { id = 0xffff, meta = 0x000f });
            blockSet.Add('*', new Block { id = 0xffff, meta = 0xffff });
            blockSet.Add('o', new Block { id = 0xffff, meta = 0x07ff });
            blockSet.Add('r', new Block { id = 0xffff, meta = 0xf00f });
            blockSet.Add('g', new Block { id = 0xffff, meta = 0x0f0f });
            blockSet.Add('b', new Block { id = 0xffff, meta = 0x00ff });
        }

        public void Init(object instance)
        {
            this.instance_n = instance as ConnectiveSimpleMCMC;

            SetupBlocks();

            Debug.Log("Conn-MCMC Visualizer inited");
        }

        public void ShowColors()
        {
            for (int x = 0; x < bound.size.x; x++)
                for (int y = 0; y < bound.size.z; y++)
                {
                    world.SetBlock(new Vector3Int(x + bound.min.x, bound.max.y - 1, y + bound.min.z), blockSet[instance_n.map[x, y]]);
                }

            renderMode = RenderMode.Color;
        }

        public void ShowConnectivity()
        {
            for (int x = 0; x < bound.size.x; x++)
                for (int y = 0; y < bound.size.z; y++)
                {
                    uint val = (uint)(instance_n.connectivityMap[x * bound.size.z + y].distance);
                    val = val & 0xffff;

                    world.SetBlock(
                        new Vector3Int(x + bound.min.x, bound.max.y - 1, y + bound.min.z),
                        new Block { id = 0xFFFF, meta = (ushort)(((ushort)val << 4) | 0x000f) }
                    );
                }

            renderMode = RenderMode.Connectivity;
        }

        public void Generate(BoundsInt bound, World world)
        {
            this.bound = bound;
            this.world = world;

            // init
            for (int x = 0; x < bound.size.x; x++)
                for (int y = 0; y < bound.size.z; y++)
                {
                    uint val = (uint)(instance_n.connectivityMap[x * bound.size.z + y].distance);
                    //uint val = (uint)instance_n.blockConnectivities[instance_n.map[x, y]];
                    val = val & 0xffff;

                    world.SetBlock(
                        new Vector3Int(x + bound.min.x, bound.max.y - 1, y + bound.min.z),
                        new Block { id = 0xFFFF, meta = (ushort)(((ushort)val << 4) | 0x000f) }
                    );

                    //world.SetBlock(new Vector3Int(x + bound.min.x, bound.max.y - 1, y + bound.min.z), blockSet[instance_n.map[x, y]]);
                }

            Debug.Log("BitMap Initialized");

            instance_n.ResetCounters();

            for (int i = 0; i < epochs; i++)
            {
                var ms = instance_n.BatchedStep(iters);
                foreach (var m in ms)
                {
                    uint val = (uint)(instance_n.connectivityMap[m.x * bound.size.z + m.y].distance);
                    val = val & 0xffff;
                    Block b = new Block { id = 0xFFFF, meta = (ushort)(((ushort)val << 4) | 0x000f) };

                    if(renderMode == RenderMode.Connectivity)
                    {
                        world.SetBlock(new Vector3Int(m.x + bound.min.x, bound.max.y - 1, m.y + bound.min.z), b);
                    }
                    else if (renderMode == RenderMode.Color)
                    {
                        world.SetBlock(new Vector3Int(m.x + bound.min.x, bound.max.y - 1, m.y + bound.min.z), blockSet[m.to]);
                    }
                }

                // Require full update on connectivities
                if (renderMode == RenderMode.Connectivity) { ShowConnectivity(); }

                if ((i % ((int)epochs / 10)) == 0)
                {
                    Debug.Log($"Ep {i} Finished, Success {instance_n.success + instance_n.successRNG} (RNG {instance_n.successRNG}); Reject {instance_n.reject}");
                }
            }

            instance_n.Finish();
            if (renderMode == RenderMode.Color) { ShowColors(); }
            if (renderMode == RenderMode.Connectivity) { ShowConnectivity(); }

            Debug.Log("MCMC Finished");
        }
    }
}