#define DO_VISUALIZATION
//#define DO_VISUALIZATION_CONNECTIVITY

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;
using Voxelis.WorldGen;

using GDMC.MCMC;

namespace GDMC
{
    public enum ElfType
    {
        Common,
        Scholar,
        Guardian,
    }

    public enum GridType
    {
        Empty = 0,
        Support = 1,
        PreservedSpace = 2,

        SolidNature = 2,
        Road = 3,
        Building = 4,

        Roof = 5
    }

    public abstract class Elf
    {
        public Elf(ElfCity city) { this.city = city; }

        public ElfType type;
        public int age;

        private ElfCity city;

        public abstract bool Birth(Vector3Int gridPos);

        public abstract void Loop();
    }

    public struct SuperGrid
    {
        public GridType type;
        public bool walkable;

        public bool[] connection;

        public Vector3 groundNormal;

        public Elf occpiedBy;

        public bool isPassable()
        {
            return (type == GridType.Empty || type == GridType.Support);
        }

        public bool isSolid()
        {
            return (type == GridType.SolidNature || type == GridType.Road || type == GridType.Building);
        }
    }

    public abstract class ElfCity 
    {
        protected int[,] heightMap;

        protected int[,] gridHeightMap;
        protected SuperGrid[,,] grids;

        // Grid helper vars
        protected Vector3Int gridSize;
        protected BoundsInt gridBound, insideGridBound;

        public const int c_gridSize_blocks = 4;
        public const float c_sgrid_solid_threshold = 0.3f;

        #region SuperGrid Helpers

        protected void SetGridType(int x, int y, int z, GridType type)
        {
            grids[x, y, z].type = type;

            if (y < (gridSize.y - 1))
            {
                grids[x, y + 1, z].walkable = grids[x, y, z].isSolid() && grids[x, y + 1, z].isPassable();
            }
        }

        #endregion
    }

    public class Alfheim_Prototype : ElfCity, IStructureGenerator
    {
        BoundsInt bound;
        World world;
        System.Random random;

        public char[,] streetMap;

        public void SetBlock(Vector3Int pos, Block blk)
        {
            pos = pos + bound.min;
            if (bound.Contains(pos))
            {
                world.SetBlock(pos, blk);
            }
        }

        public Block GetBlock(Vector3Int pos)
        {
            pos = pos + bound.min;
            if(bound.Contains(pos))
            {
                return world.GetBlock(pos);
            }

            return Block.Empty;
        }

        #region Notes (Helper)

        bool note_hasPrev;
        int note_qLength = 16;

        struct Note_struct
        {
            public Vector3Int pos;
            public Block blk;

            public int lifespan;
        }

        Queue<Note_struct> notes;

        // TODO: do this (why tho)
        //void Note(Vector3Int pos, uint note = 0xff0000ff)
        //{
        //    if(notes.Count >= note_qLength)
        //    {
        //        foreach (var n in notes)
        //        {
        //            SetBlock(n.pos, note_prevNote);
        //        }
        //    }
        //    note_hasPrev = true;
        //    note_prevNote = GetBlock(pos);
        //    note_prevPos = pos;
        //    SetBlock(pos, Block.From32bitColor(note));
        //}

        //void ClearNote()
        //{
        //    SetBlock(note_prevPos, note_prevNote);
        //    note_hasPrev = false;
        //}

        #endregion

        #region SuperGrid visualization helpers

        public static readonly uint[] gridColor = new uint[6]
        {
            // Empty
            0x00000000,

            // Support
            0x0000ffff,

            // SolidNature
            0x00ff00f,

            // Road
            0x999999ff,

            // Building
            0xff0000ff,

            // Roof
            0xff00ffff
        };

        protected void DEBUG_ShowSuperGrid()
        {
            foreach (var p in gridBound.allPositionsWithin)
            {
                SuperGrid sg = grids[p.x, p.y, p.z];
                ushort bid = ((sg.type == GridType.Empty) ? (ushort)0x0000 : (ushort)0xffff);

                if (sg.type == GridType.SolidNature)
                {
                    SetBlock(bound.size - gridSize + p, new Block
                    {
                        id = bid * (uint)((Mathf.Clamp((int)((Vector3.Dot(sg.groundNormal, Vector3.up) + 0.0f) * 256.0f), 0, 255) << 16) + 0xff)
                    });
                }
                else
                {
                    SetBlock(bound.size - gridSize + p, new Block { id = bid * gridColor[(int)sg.type] });
                }
            }
        }

        #endregion

        public void Generate(BoundsInt bound, World world)
        {
            // Initialization
            this.bound = bound;
            this.world = world;
            random = new System.Random();

            // Read-in
            //SetupHeightMap();
            SetupSuperGrid();
            DEBUG_ShowSuperGrid();

            // Alfheim tree & Royal Avenue
            SetupRoyalArea();

            // MCMC Pass
            DoMCMCPass();

            // Fill Pass
            DoFillPass();

            // WFC Pass
            DoWFCPass();

            // Resident placement & update loop
            //PlaceResident(new Elves.CommonElf(this));

            // Finishing
            //ClearNote();
        }

        protected Dictionary<char, Block> blockSet = new Dictionary<char, Block>();
        protected Dictionary<char, int> blockModifications = new Dictionary<char, int>();
        public double temperature;

        public int mcmc_iters, mcmc_epochs;

        string GetStr<K,V>(Dictionary<K,V> d)
        {
            string s = "";
            foreach (var item in d)
            {
                s += $"{item.Key} : {item.Value} | ";
            }

            return s;
        }

        private void DoMCMCPass()
        {
            PatternWeights weights = new PatternWeights(0.2f);

            char[,,] mcmc_input = new char[gridSize.x, gridSize.y, gridSize.z];
            gridBound = new BoundsInt(0, 0, 0, gridSize.x, gridSize.y, gridSize.z);

            char[] charset = new char[2] { ' ', '*' };

            // Read Map Data
            foreach (var p in gridBound.allPositionsWithin)
            {
                SuperGrid sg = grids[p.x, p.y, p.z];

                if (sg.type == GridType.SolidNature)// && Vector3.Dot(sg.groundNormal, Vector3.up) > 0.707f)
                {
                    mcmc_input[p.x, p.y, p.z] = 'g';
                }
                else
                {
                    mcmc_input[p.x, p.y, p.z] = '*';

                    // Above main road and cannot build
                    if (streetMap != null && streetMap[p.x * 4, p.z * 4] == '*' && p.y < 48)
                    {
                        mcmc_input[p.x, p.y, p.z] = 'b';
                    }
                }
            }

            // Read Map Data
            foreach (var p in gridBound.allPositionsWithin)
            {
                // Place a layer of buildings on top of nature
                if (p.y > 0 &&
                    grids[p.x, p.y, p.z].type == GridType.Empty && 
                    grids[p.x, p.y - 1, p.z].type == GridType.SolidNature)
                {
                    // Main road (on ground)
                    if(streetMap != null && streetMap[p.x * 4, p.z * 4] == '*')
                    {
                        mcmc_input[p.x, p.y - 1, p.z] = 'o';
                    }
                    else
                    {
                        // Randomly place some buildings
                        if(random.NextDouble() < 0.5)
                        {
                            mcmc_input[p.x, p.y, p.z] = ' ';
                        }
                    }
                }
            }

            Alfheim_MCMCPass pass = new Alfheim_MCMCPass(
                weights,
                mcmc_input,
                charset,
                gridSize.x,
                gridSize.y,
                gridSize.z,
                3
            );

            pass.temperature = temperature;

            // Initial Visualization
#if DO_VISUALIZATION
            #region Visualize

            blockSet.Add(' ', new Block { id = 0x99dd66ff });
            blockSet.Add('*', new Block { id = 0x00000000 }); // Air
            blockSet.Add('o', new Block { id = 0x0077ffff });
            blockSet.Add('r', new Block { id = 0xff0000ff });
            blockSet.Add('g', new Block { id = 0x00ff00ff });
            blockSet.Add('b', new Block { id = 0x00000000 }); // Air (Preserved)

            foreach (var p in gridBound.allPositionsWithin)
            {
                SetBlock(bound.size - gridSize + p, blockSet[pass.map[p.x, p.y, p.z]]);
            }

            #endregion
#endif

            pass.ResetCounters();

            int epochs = mcmc_epochs;
            int iters = mcmc_iters;

            for(int i = 0; i < epochs; i++)
            {
                var ms = pass.BatchedStep(iters);

                foreach (var m in ms)
                {
                    if(!blockModifications.ContainsKey(m.to))
                    {
                        blockModifications[m.to] = 0;
                    }
                    blockModifications[m.to] += 1;

#if DO_VISUALIZATION
                    SetBlock(bound.size - gridSize + new Vector3Int(m.x, m.y, m.z), blockSet[m.to]);
#endif
                }

                if ((i % ((int)epochs / 10)) == 0)
                {
                    Debug.Log($"Ep {i} Finished, Success {pass.success + pass.successRNG} (RNG {pass.successRNG}); Reject {pass.reject}; Reroll {pass.reroll}; E = {pass.prevEnergy}\n" +
                        $"Time: Proposal {pass.proposalTicks}, Pattern {pass.patternTicks}, ConnectionCopy {pass.connCopyTicks}, ConnectionUpdate {pass.connUpdateTicks}\n" +
                        $"{GetStr<char, int>(blockModifications)}");
                }

#if DO_VISUALIZATION_CONNECTIVITY
                for (int xx = gridBound.min.x; xx < gridBound.max.x; xx++)
                {
                    for (int yy = gridBound.min.y; yy < gridBound.max.y; yy++)
                    {
                        int zz = 32;
                        Vector3Int p = new Vector3Int(xx, yy, zz);

                        uint val = (uint)(pass.connectivityMap[pass.flatten(xx, yy, zz)].distance);
                        val = val & 0xffff;

                        SetBlock(
                            bound.size - gridSize + p,
                            new Block { id = 0xFFFF, meta = (ushort)(((ushort)val << 4) | 0x000f) }
                        );
                    }
                }
#endif
            }

            pass.Finish();

#if DO_VISUALIZATION
            foreach (var p in gridBound.allPositionsWithin)
            {
                SetBlock(bound.size - gridSize + p, blockSet[pass.map[p.x, p.y, p.z]]);
            }

#if DO_VISUALIZATION_CONNECTIVITY
            for (int xx = gridBound.min.x; xx < gridBound.max.x; xx++)
            {
                for (int yy = gridBound.min.y; yy < gridBound.max.y; yy++)
                {
                    int zz = 32;
                    Vector3Int p = new Vector3Int(xx, yy, zz);

                    uint val = (uint)(pass.connectivityMap[pass.flatten(xx, yy, zz)].distance);
                    val = val & 0xffff;

                    SetBlock(
                        bound.size - gridSize + p,
                        new Block { id = 0xFFFF, meta = (ushort)(((ushort)val << 4) | 0x000f) }
                    );
                }
            }
#endif
#endif

            Debug.Log("MCMC Finished");
        }

        private void DoFillPass()
        {
        }

        private void DoWFCPass()
        {
        }

        private void SetupHeightMap()
        {
            heightMap = new int[bound.size.x, bound.size.z];

            for (int x = 0; x <= bound.size.x; x++)
            {
                for (int z = 0; z <= bound.size.z; z++)
                {
                    for (int y = bound.size.y; y >= 0; y--)
                    {
                        Vector3Int pos = new Vector3Int(x, y, z);
                        if (GetBlock(pos).IsSolid())
                        {
                            //SetBlock(pos, Block.From32bitColor(0xff0000ff));
                            heightMap[x, z] = y;
                            //Note(pos);
                            break;
                        }
                    }
                }
            }
        }

        private void SetupSuperGrid()
        {
            gridSize = new Vector3Int(bound.size.x / c_gridSize_blocks, bound.size.y / c_gridSize_blocks, bound.size.z / c_gridSize_blocks);
            grids = new SuperGrid[gridSize.x, gridSize.y, gridSize.z];
            gridHeightMap = new int[gridSize.x, gridSize.z];

            gridBound = new BoundsInt(0, 0, 0, gridSize.x, gridSize.y, gridSize.z);
            insideGridBound = new BoundsInt(0, 0, 0, c_gridSize_blocks, c_gridSize_blocks, c_gridSize_blocks);

            // Read Map Data
            foreach (var p in gridBound.allPositionsWithin)
            {
                int solidCount = 0;
                Vector3 normal = Vector3.zero;

                foreach (var blockPos in insideGridBound.allPositionsWithin)
                {
                    if (GetBlock(p * c_gridSize_blocks + blockPos).IsSolid())
                    {
                        solidCount++;
                        normal += ((Vector3.one * (c_gridSize_blocks - 1.0f) / 2.0f) - (Vector3)blockPos).normalized;
                    }
                }

                float solidRate = solidCount / (float)(Mathf.Pow(c_gridSize_blocks, 3.0f));
                if (solidRate > c_sgrid_solid_threshold)
                {
                    SetGridType(p.x, p.y, p.z, GridType.SolidNature);
                    grids[p.x, p.y, p.z].groundNormal = solidRate == 1.0f ? Vector3.up : normal.normalized;

                    if(gridHeightMap[p.x, p.z] <= p.y)
                    {
                        gridHeightMap[p.x, p.z] = p.y;
                    }
                }
            }
        }

        private void SetupRoyalArea()
        {
            // Sample candidates from blue noise
            // Assign score to points
            // Check if point good to place Alfheim tree
            // Below threshold - early terminate
            // Otherwise pick best point

            // Generate Royal Avenue
            // Generate major streets maybe ? connect the graph
        }

        private bool PlaceResident(Elf elf)
        {
            Vector2Int xzPos = new Vector2Int(random.Next(0, bound.size.x), random.Next(0, bound.size.z));

            return elf.Birth(new Vector3Int(xzPos.x, gridHeightMap[xzPos.x, xzPos.y] + 1, xzPos.y));
        }

        public void RandomRects(int rects = 100, int rsmin = 10, int rsmax = 64)
        {
            for (int r = 0; r < rects; r++)
            {
                Vector2Int rSize = new Vector2Int(random.Next(rsmin, rsmax), random.Next(rsmin, rsmax));
                Vector2Int rPos = new Vector2Int(random.Next(0, bound.size.x - rSize.x), random.Next(0, bound.size.z - rSize.y));

                uint color = (((uint)(random.Next(0x000000, 0xffffff))) << 8) + 0xff;

                for (int x = 0; x < rSize.x; x++)
                {
                    for (int z = 0; z < rSize.y; z++)
                    {
                        if (z < 2 || x < 2 || z >= (rSize.y - 2) || x >= (rSize.x - 2))
                        {
                            SetBlock(new Vector3Int(rPos.x + x, heightMap[rPos.x + x, rPos.y + z], rPos.y + z), Block.From32bitColor(0x000000ff));
                        }
                        else
                        {
                            SetBlock(new Vector3Int(rPos.x + x, heightMap[rPos.x + x, rPos.y + z], rPos.y + z), Block.From32bitColor(0x00ff00ff));
                        }
                    }
                }
            }
        }
    }
}