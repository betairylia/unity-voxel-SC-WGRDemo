using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;
using C5;

namespace GDMC.MCMC
{
    // Plain Metropolis-Hastings with uniform proposals
    public class Alfheim_MCMCPass
    {
        System.Random random;

        PatternWeights weights;

        public enum Direction
        {
            // Z+, Z-
            Forward = 0,
            Backward = 1,

            // X+, X-
            Right = 2,
            Left = 3,

            // Y+, Y-
            Up = 4,
            Down = 5
        }

        public readonly Vector3Int[] dirc_dxyz =
        {
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0),
        };

        public readonly int[] dirc_dx = { 0,  0, 1, -1, 0,  0 };
        public readonly int[] dirc_dy = { 0,  0, 0,  0, 1, -1 };
        public readonly int[] dirc_dz = { 1, -1, 0,  0, 0,  0 };

        public readonly Direction[] possibleDirections =
        {
            Direction.Forward,
            Direction.Backward,
            Direction.Right,
            Direction.Left,
            Direction.Up,
            Direction.Down,
        };

        public readonly Direction[] reverseDirections =
        {
            Direction.Backward,
            Direction.Forward,
            Direction.Left,
            Direction.Right,
            Direction.Down,
            Direction.Up,
        };

        public struct ConnectivityInfo
        {
            public int distanceToNearestAir;
            public int distance;
            public Direction next;

            public ConnectivityInfo(int dist, int distAir, Direction next)
            {
                this.distanceToNearestAir = distAir;
                this.distance = dist;
                this.next = next;
            }

            public ConnectivityInfo AppendBeforeHead(ConnectivityInfo src, ConnectivityInfo d)
            {
                return new ConnectivityInfo(src.distance + d.distance, src.distanceToNearestAir + d.distanceToNearestAir, d.next);
            }
        }

        public Dictionary<string, int> _rawblockConnectivities = new Dictionary<string, int>();

        public char GetBlockSolidness(char blk)
        {
            if(blk == '*' || blk == 'b') { return '*'; }
            return ' ';
        }
        
        /// <summary>
        /// Get Connectivity based on current state
        /// </summary>
        /// <param name="airBit">Is the agent in mid-air right now?</param>
        /// <param name="x">Agent X</param>
        /// <param name="y">Agent Y</param>
        /// <param name="z">Agent Z</param>
        /// <returns></returns>
        //public ConnectivityInfo GetConnectivity(Vector3Int from, Vector3Int to)
        public int GetConnectivity(Vector3Int from, Vector3Int to)
        {
            bool isVertical = !(from.y == to.y);

            bool airBit = (from.y == 0) || (GetBlockSolidness(map[from.x, from.y - 1, from.z]) == '*');

            char block = GetBlockSolidness(map[to.x, to.y, to.z]);
            char fromSolid = GetBlockSolidness(map[from.x, from.y, from.z]);

            char footBlock = '*';
            if(to.y > 0) { footBlock = GetBlockSolidness(map[to.x, to.y - 1, to.z]); }

            string desc = $"{(airBit ? "a" : "g")}{fromSolid}{block}{footBlock}{(isVertical ? "v" : "")}";

            return _rawblockConnectivities[desc];
        }

        public struct BFSKeys : IComparable
        {
            public Vector3Int pos;
            public int distance;

            public int CompareTo(object obj)
            {
                return distance - ((BFSKeys)obj).distance;
            }

            public BFSKeys(int x, int y, int z, int d)
            {
                this.pos = new Vector3Int(x, y, z);
                this.distance = d;
            }
        }

        public char[,,] map { get; protected set; }
        public ConnectivityInfo[] connectivityMap { get; protected set; }
        ConnectivityInfo[] connectivityMap_backup;
        C5.IntervalHeap<BFSKeys> pQueue = new IntervalHeap<BFSKeys>();
        Queue<Vector3Int> refreshBFSQueue = new Queue<Vector3Int>();
        System.Collections.Generic.HashSet<Vector3Int> BFSModifiedPoints = new System.Collections.Generic.HashSet<Vector3Int>();

        char[] charset;
        float[] blockwiseEnergy = new float[256];

        int maplenX, maplenY, maplenZ;
        int N;

        public int success = 0, reject = 0, successRNG = 0;

        public double temperature = 1.0;

        public struct Modification
        {
            public int x, y, z;
            public char to;
        }

        public Alfheim_MCMCPass(PatternWeights weights, char[,,] input, char[] charset, int maplenX, int maplenY, int maplenZ, int neighbor = 3)
        {
            this.weights = weights;
            this.map = input;
            this.connectivityMap = new ConnectivityInfo[maplenX * maplenY * maplenZ];
            this.connectivityMap_backup = new ConnectivityInfo[maplenX * maplenY * maplenZ];

            this.charset = charset;

            this.maplenX = maplenX;
            this.maplenY = maplenY;
            this.maplenZ = maplenZ;

            N = neighbor;

            random = new System.Random();

            InitializeBlockwiseEnergy();
            InitializeBlockConnectivities();
            InitializeConnectivity();
            RefreshEnergy();
        }

        private void InitializeBlockwiseEnergy()
        {
            float eps = 0.1f;

            blockwiseEnergy[' '] = (float)System.Math.Log(eps); // Building
            blockwiseEnergy['*'] = (float)System.Math.Log(1.0); // Air
            blockwiseEnergy['r'] = (float)System.Math.Log(eps);
            blockwiseEnergy['g'] = (float)System.Math.Log(eps);
            blockwiseEnergy['b'] = (float)System.Math.Log(eps);
            blockwiseEnergy['o'] = (float)System.Math.Log(eps);
        }

        private int flatten(Vector3Int v) => flatten(v.x, v.y, v.z);

        public int flatten(int x, int y, int z)
        {
            return x * maplenY * maplenZ + y * maplenZ + z;
        }

        private bool CheckLoop(Vector3Int coord)
        {
            Vector3Int origin = coord;

            System.Collections.Generic.HashSet<Vector3Int> visited = new System.Collections.Generic.HashSet<Vector3Int>();
            visited.Add(coord);

            while (map[coord.x, coord.y, coord.z] != 'o')
            {
                coord += dirc_dxyz[(int)(connectivityMap[flatten(coord)].next)];

                if (visited.Contains(coord))
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
            float multiplier = 0.92f;
            int dOffset = 2;

            //return (float)System.Math.Log(distance + dOffset) - multiplier;
            return (float)System.Math.Log((distance + dOffset) / 10.0f) * multiplier;
            //return (float)(distance + dOffset) * multiplier;
        }

        private float GetConnectivityEnergy(int x, int y, int z)
            => GetConnectivityEnergy(connectivityMap[flatten(x, y, z)].distance);

        private void RefreshEnergy()
        {
            for (int x = 0; x < maplenX; x++)
            {
                for (int y = 0; y < maplenY; y++)
                {
                    for (int z = 0; z < maplenZ; z++)
                    {
                        // Pattern energy
                        prevEnergy += -(float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, x, y, N, N)));

                        // Block energy
                        prevEnergy += blockwiseEnergy[map[x, y, z]];

                        // Connectivity energy
                        if(map[x, y, z] == ' ')
                        {
                            prevEnergy += GetConnectivityEnergy(x, y, z);
                        }
                    }
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
                    for (int z = 0; z < maplenZ; z++)
                    {
                        // Set initial value
                        connectivityMap[flatten(x, y, z)] = new ConnectivityInfo(9999999, 100, Direction.Forward);

                        // Set initial values for air
                        if(GetBlockSolidness(map[x, y, z]) == '*')
                        {
                            connectivityMap[flatten(x, y, z)].distanceToNearestAir = 0;
                        }

                        // Preserved grid as major roads
                        if (map[x, y, z] == 'o')
                        {
                            connectivityMap[flatten(x, y, z)].distanceToNearestAir = 0;
                            connectivityMap[flatten(x, y, z)].distance = 0;
                            pQueue.Add(new BFSKeys(x, y, z, 0));
                        }
                    }
                }
            }

            while (!pQueue.IsEmpty)
            {
                var curr = pQueue.DeleteMin();

                // Iterate all possible directions
                foreach (var d in possibleDirections)
                {
                    Vector3Int newpos = curr.pos + dirc_dxyz[(int)d];
                    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY || newpos.z < 0 || newpos.z >= maplenZ) { continue; }

                    // Check if neighbor can achieve shorter path by pointing to self
                    // ( Relax )
                    //int newdist = curr.distance + blockConnectivities[map[curr.pos.x, curr.pos.y, curr.pos.z]];
                    int newdist = curr.distance + GetConnectivity(newpos, curr.pos);
                    if (connectivityMap[flatten(newpos)].distance > newdist)
                    {
                        // Set distance and next pointer
                        connectivityMap[flatten(newpos)].distance = newdist;
                        connectivityMap[flatten(newpos)].next = reverseDirections[(int)d];

                        pQueue.Add(new BFSKeys(newpos.x, newpos.y, newpos.z, newdist));
                    }
                }
            }
        }

        private void InitializeBlockConnectivities()
        {
            // Initialize block connectivities

            // string desc = $"{(airBit ? "a" : "g")}{fromSolid}{block}{footBlock}{(isVertical ? "v" : "")}";

            // '<a/g><block><block under foot>"

            // ' ' = Buildings
            // '*' = Air

            int invalid = 8;
            int valid = 1;
            int stairs = 2; // vertical movement
            int fall_raise = 16384; // invalid vertical movement
            int solid_to_air_vert = 16384; // invalid vertical movement

            _rawblockConnectivities.Add("a   ", invalid);
            _rawblockConnectivities.Add("a  *", fall_raise + invalid);
            _rawblockConnectivities.Add("a * ", valid);
            _rawblockConnectivities.Add("a **", fall_raise);

            _rawblockConnectivities.Add("g   ", invalid);
            _rawblockConnectivities.Add("g  *", fall_raise + invalid);
            _rawblockConnectivities.Add("g * ", valid);
            _rawblockConnectivities.Add("g **", fall_raise + stairs);

            _rawblockConnectivities.Add("a*  ", invalid);
            _rawblockConnectivities.Add("a* *", fall_raise + invalid);
            _rawblockConnectivities.Add("a** ", valid);
            _rawblockConnectivities.Add("a***", fall_raise);

            _rawblockConnectivities.Add("g*  ", invalid);
            _rawblockConnectivities.Add("g* *", fall_raise + invalid);
            _rawblockConnectivities.Add("g** ", valid);
            _rawblockConnectivities.Add("g***", stairs);

            ////////////////////////////////////////////////////////////////////

            //_rawblockConnectivities.Add("a   v", invalid);
            //_rawblockConnectivities.Add("a  *v", fall_raise + invalid);
            //_rawblockConnectivities.Add("a * v", solid_to_air_vert);
            //_rawblockConnectivities.Add("a **v", solid_to_air_vert + fall_raise);

            //_rawblockConnectivities.Add("g   v", invalid);
            //_rawblockConnectivities.Add("g  *v", fall_raise + invalid);
            //_rawblockConnectivities.Add("g * v", solid_to_air_vert);
            //_rawblockConnectivities.Add("g **v", solid_to_air_vert + stairs);

            //_rawblockConnectivities.Add("a*  v", invalid);
            //_rawblockConnectivities.Add("a* *v", fall_raise + invalid);
            //_rawblockConnectivities.Add("a** v", valid);
            //_rawblockConnectivities.Add("a***v", fall_raise);

            //_rawblockConnectivities.Add("g*  v", invalid);
            //_rawblockConnectivities.Add("g* *v", fall_raise + invalid);
            //_rawblockConnectivities.Add("g** v", valid);
            //_rawblockConnectivities.Add("g***v", stairs);

            _rawblockConnectivities.Add("a   v", solid_to_air_vert);
            _rawblockConnectivities.Add("a  *v", solid_to_air_vert + invalid);
            _rawblockConnectivities.Add("a * v", solid_to_air_vert);
            _rawblockConnectivities.Add("a **v", solid_to_air_vert + fall_raise);

            _rawblockConnectivities.Add("g   v", solid_to_air_vert);
            _rawblockConnectivities.Add("g  *v", solid_to_air_vert + invalid);
            _rawblockConnectivities.Add("g * v", solid_to_air_vert);
            _rawblockConnectivities.Add("g **v", solid_to_air_vert + stairs);

            _rawblockConnectivities.Add("a*  v", solid_to_air_vert);
            _rawblockConnectivities.Add("a* *v", solid_to_air_vert + invalid);
            _rawblockConnectivities.Add("a** v", solid_to_air_vert);
            _rawblockConnectivities.Add("a***v", solid_to_air_vert);

            _rawblockConnectivities.Add("g*  v", solid_to_air_vert);
            _rawblockConnectivities.Add("g* *v", solid_to_air_vert + invalid);
            _rawblockConnectivities.Add("g** v", solid_to_air_vert);
            _rawblockConnectivities.Add("g***v", solid_to_air_vert);
        }

        public float prevEnergy = 0;
        public int reroll;

        public long proposalTicks = 0, patternTicks = 0, connCopyTicks = 0, connUpdateTicks = 0;

        // True: something has been modified
        // False: rejected, nothing changed
        public bool Step(ref Modification modification)
        {
            long ticks_begin = DateTime.Now.Ticks;

            // TODO: Asymmetric proposal from already occpied blocks

            int x = random.Next(0, maplenX);
            int y = random.Next(0, maplenY);
            int z = random.Next(0, maplenZ);

            long ticks_proposal = DateTime.Now.Ticks;
            proposalTicks += ticks_proposal - ticks_begin;

            // This grid is preserved, cannot be modified.
            // o - major road; g - preserved nature; b - preserved air
            if (map[x, y, z] == 'o' || map[x, y, z] == 'g' || map[x, y, z] == 'b') { reroll++; return false; }

            int to = random.Next(0, charset.Length);

            // Don't waste time ...
            if( map[x,y,z] == charset[to] ) { reroll++; return false; }

            float Eorg = 0, Enew = 0;
            char Corg = map[x, y, z];

            ticks_begin = DateTime.Now.Ticks;

            // TODO: 3D Patterns

            // Previous energy
            //for (int sx = x - N + 1; sx <= x + N - 1; sx++)
            //{
            //    for (int sy = y - N + 1; sy <= y + N - 1; sy++)
            //    {
            //        Eorg += -(float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, sx, sy, N, N)));
            //    }
            //}

            // Previous block-wise energy
            Eorg += blockwiseEnergy[Corg];

            // Replace and calculate new energy
            map[x, y, z] = charset[to];

            // New block-wise energy
            Enew += blockwiseEnergy[charset[to]];

            //for (int sx = x - N + 1; sx <= x + N - 1; sx++)
            //{
            //    for (int sy = y - N + 1; sy <= y + N - 1; sy++)
            //    {
            //        Enew += -(float)System.Math.Log(weights.GetWeight(PatternWeights.GetPatternStr(map, maplenX, maplenY, sx, sy, N, N)));
            //    }
            //}

            long ticks_pattern = DateTime.Now.Ticks;
            patternTicks += ticks_pattern - ticks_begin;

            // Connectivity energy
            #region Connectivity

            ticks_begin = DateTime.Now.Ticks;

            connectivityMap.CopyTo(connectivityMap_backup, 0);

            long ticks_connCpy = DateTime.Now.Ticks;
            connCopyTicks += ticks_connCpy - ticks_begin;

            ticks_begin = DateTime.Now.Ticks;

            // Initialize distance

            if (!pQueue.IsEmpty) { pQueue = new IntervalHeap<BFSKeys>(); Debug.LogError("pQueue non empty !"); }
            BFSModifiedPoints.Clear();
            refreshBFSQueue.Clear();

            refreshBFSQueue.Enqueue(new Vector3Int(x, y, z));

            //pQueue.Add(new BFSKeys(x, y, connectivityMap[flatten(x, y)].distance));

            //foreach (var d in possibleDirections)
            //{
            //    Vector3Int newpos = new Vector3Int(x, y) + dirc_dxy[(int)d];
            //    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY) { continue; }

            //    pQueue.Add(new BFSKeys(newpos.x, newpos.y, connectivityMap[flatten(newpos.x, newpos.y)].distance));
            //}

            // Recalculate distances w.r.t. existing paths and new block
            while (refreshBFSQueue.Count > 0)
            {
                Vector3Int pt = refreshBFSQueue.Dequeue();
                int dist = connectivityMap[flatten(pt)].distance;

                pQueue.Add(new BFSKeys(pt.x, pt.y, pt.z, dist));

                // No this issue now
                //if (BFSModifiedPoints.Contains(new Vector3Int(pt.x, pt.y)))
                //{
                //    Debug.LogError("Loops exist in connectivityMap !!!!");
                //    break;
                //}

                BFSModifiedPoints.Add(pt);

                // Iterate all possible directions
                foreach (var d in possibleDirections)
                {
                    Vector3Int newpos = pt + dirc_dxyz[(int)d];
                    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY || newpos.z < 0 || newpos.z >= maplenZ) { continue; }

                    if (map[newpos.x, newpos.y, newpos.z] == 'o') { continue; }

                    // Check if neighbor's next is self
                    if (connectivityMap[flatten(newpos)].next == reverseDirections[(int)d])
                    {
                        connectivityMap[flatten(newpos)].distance =
                            //dist + blockConnectivities[map[pt.x, pt.y, pt.z]];
                            dist + GetConnectivity(newpos, pt);

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
                    Vector3Int newpos = curr.pos + dirc_dxyz[(int)d];
                    if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY || newpos.z < 0 || newpos.z >= maplenZ) { continue; }

                    //if(d == connectivityMap[flatten(curr.pos.x, curr.pos.y)].next) { continue; }

                    // Check if self can get shorter path by pointing to new neighbor
                    //int newdist =
                    //     +
                    //    blockConnectivities[map[newpos.x, newpos.y, newpos.z]];
                    int newdist = connectivityMap[flatten(newpos)].distance + GetConnectivity(curr.pos, newpos);

                    if (newdist < connectivityMap[flatten(curr.pos)].distance)
                    {
                        connectivityMap[flatten(curr.pos)].distance = newdist;
                        connectivityMap[flatten(curr.pos)].next = d;

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
                    //    BFSModifiedPoints.Add(new Vector3Int(newpos.x, newpos.y));
                    //}
                }

                if (updated)
                {
                    foreach (var d in possibleDirections)
                    {
                        Vector3Int newpos = curr.pos + dirc_dxyz[(int)d];
                        if (newpos.x < 0 || newpos.x >= maplenX || newpos.y < 0 || newpos.y >= maplenY || newpos.z < 0 || newpos.z >= maplenZ) { continue; }

                        pQueue.Add(new BFSKeys(newpos.x, newpos.y, newpos.z, connectivityMap[flatten(newpos)].distance));
                        BFSModifiedPoints.Add(newpos);
                    }
                }
            }

            foreach (var pt in BFSModifiedPoints)
            {
                // Original modification point, need to use different version of blockdatta
                if(pt == new Vector3Int(x, y, z))
                {
                    // Original
                    if(Corg == ' ')
                    {
                        Eorg += GetConnectivityEnergy(connectivityMap_backup[flatten(pt)].distance);
                    }

                    // New
                    if(charset[to] == ' ')
                    {
                        Enew += GetConnectivityEnergy(connectivityMap[flatten(pt)].distance);
                    }
                }
                else
                {
                    if (map[pt.x, pt.y, pt.z] == ' ')
                    {
                        Eorg += GetConnectivityEnergy(connectivityMap_backup[flatten(pt)].distance);
                        Enew += GetConnectivityEnergy(connectivityMap[flatten(pt)].distance);
                    }
                }
            }

            long ticks_conn = DateTime.Now.Ticks;
            connUpdateTicks += ticks_conn - ticks_begin;

            #endregion

            // Convert difference in energy to true energy using prevEnergy = Eorg
            Enew = prevEnergy - Eorg + Enew;
            Eorg = prevEnergy;

            // Reject ?
            if (Enew < Eorg)
            {
                modification.x = x;
                modification.y = y;
                modification.z = z;
                modification.to = charset[to];

                prevEnergy = Enew;

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
                    modification.z = z;
                    modification.to = charset[to];

                    prevEnergy = Enew;

                    successRNG++;

                    return true;
                }
            }

            // Rejected, reverse modification
            reject++;

            map[x, y, z] = Corg;

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
                    for (int z = 0; z < maplenZ; z++)
                    {
                        if (CheckLoop(new Vector3Int(x, y, z)))
                        {
                            Debug.LogError("Loops found");
                        }
                    }
                }
            }

            Debug.Log("Loop check finished");

            // Replace all black grids on road to 'red'

            //bool[] visited = new bool[maplenX * maplenY * maplenZ];

            //for (int x = 0; x < maplenX; x++)
            //{
            //    for (int y = 0; y < maplenY; y++)
            //    {
            //        for (int z = 0; z < maplenZ; z++)
            //        {
            //            // Set initial value
            //            visited[flatten(x, y, z)] = false;

            //            // ignore major roads
            //            if (map[x, y, z] == 'o')
            //            {
            //                visited[flatten(x, y, z)] = true;
            //            }
            //        }
            //    }
            //}

            //for (int x = 0; x < maplenX; x++)
            //{
            //    for (int y = 0; y < maplenY; y++)
            //    {
            //        for (int z = 0; z < maplenZ; z++)
            //        {
            //            if (map[x, y, z] == '*' && visited[flatten(x, y, z)] == false)
            //            {
            //                Vector3Int pt = new Vector3Int(x, y, z);
            //                while (map[pt.x, pt.y, pt.z] != 'o' && visited[flatten(pt)] == false)
            //                {
            //                    if (map[pt.x, pt.y, pt.z] == ' ')
            //                    {
            //                        map[pt.x, pt.y, pt.z] = 'r';
            //                    }

            //                    visited[flatten(pt)] = true;
            //                    pt += dirc_dxyz[(int)connectivityMap[flatten(pt)].next];
            //                }
            //            }
            //        }
            //    }
            //}

            InitializeConnectivity();
        }
    }
}