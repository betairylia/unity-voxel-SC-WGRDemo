using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;
using XNode;

namespace Matryoshka.Generators
{
    // TODO: Rubbish code, pls optimize

    // Generators with planting abailities.
    public class SingleBlockGrowthInstance : PlanterNodeInstance
    {
        // HERE: Declear variables for actual calculation
        // e.g. public float ratio;
        public Vector3 generalDirection;
        public float dircStrength = 0.7f;
        public int stepMin, stepMax;

        // Favor "connected" neighbors
        public float connection = 0.5f;

        public uint block;
        System.Random random;

        List<KeyValuePair<Vector3Int, Block>> whereToGrowth = new List<KeyValuePair<Vector3Int, Block>>();

        public override void Voxelize(World world)
        {
            // HERE: Fill the world with your data
            // Use world.SetBlock(), etc.
            // Beaware of the Bounds of the root node, which can be set in the NodeGraph Editor.
            // NO modifications outside the bound.

            foreach (var item in whereToGrowth)
            {
                world.SetBlock(item.Key, random.NextDouble() < 0.02 ? Block.From32bitColor(0x34bfb890) : item.Value);
            }
        }

        public override void Build(World world)
        {
            // HERE: Modify to add meaningful seed to seeds
            //seeds.Add(NewSeed(Vector3.zero, Quaternion.identity, origin.power, 0));

            Vector3Int pi = Vector3Int.FloorToInt(origin.position);
            random = new System.Random(pi.x ^ pi.y ^ pi.z);

            float stepCostMin = 1.0f / (float)(stepMax);
            float stepCostMax = 1.0f / (float)(stepMin);
            float currentCost = 0.0f;
            
            Vector3Int nextDirc = Vector3Int.zero;
            float maxScore = 0.0f;

            Debug.Log(origin.normal);
            Vector3 growthDirc = origin.world.rotation * generalDirection * dircStrength;

            BoundsInt neighbor2 = new BoundsInt(-2, -2, -2, 5, 5, 5);
            BoundsInt neighbor1 = new BoundsInt(-1, -1, -1, 3, 3, 3);

            while (currentCost < 1.0f)
            {
                float stepCost_thisStep = Utils.MathCore.RandomRange(random, stepCostMin, stepCostMax);
                float stepCost_real = 1.0f;
                maxScore = -1e5f;

                // Get world data
                Block[,,] blocks_cache = new Block[5,5,5];
                foreach(var o in neighbor2.allPositionsWithin)
                {
                    blocks_cache[o.x + 2, o.y + 2, o.z + 2] = world.GetBlock(pi + o);
                }

                foreach (var offset in neighbor1.allPositionsWithin)
                {
                    // Copy stepcost
                    float stepCost = stepCost_thisStep;

                    // Destination must be empty
                    if (blocks_cache[offset.x + 2, offset.y + 2, offset.z + 2].IsSolid()) 
                    { 
                        continue; 
                    }

                    // Decide which direction to go

                    // Random noise
                    float score = (float)random.NextDouble();

                    // Growth direction
                    score += Vector3.Dot(offset, growthDirc);

                    // Connectedness
                    score *= (offset.sqrMagnitude <= 1) ? (1.0f) : (1.0f - connection);

                    // Surrounding / Supports
                    stepCost = stepCost / 0.95f;
                    foreach (Vector3Int support_check in neighbor1.allPositionsWithin)
                    {
                        Vector3Int nn = support_check + offset;
                        if(blocks_cache[nn.x + 2, nn.y + 2, nn.z + 2].IsSolid())
                        {
                            score += 2.5f / 27.0f;
                            stepCost = stepCost * 0.7f;
                        }
                    }

                    stepCost = Mathf.Max(stepCost, stepCostMin * 0.1f);

                    if (score > maxScore)
                    {
                        maxScore = score;
                        nextDirc = offset;
                        stepCost_real = stepCost;
                    }
                }

                if(maxScore < -1e4f)
                {
                    // Cannot find suitable grid; terminating
                    break;
                }

                currentCost += stepCost_real;

                // Grow along the selected direction
                //world.SetBlock(pi, block);
                whereToGrowth.Add(new KeyValuePair<Vector3Int, Block>(pi, Block.From32bitColor(block)));
                pi += nextDirc;
            }

            // Rubbish, dead since no support ()
            if(whereToGrowth.Count < 15)
            {
                whereToGrowth.Clear();
            }

            // Final block
            //world.SetBlock(pi, 0x8fc9ff24);
        }
    }

    public class SingleBlockGrowthUnderNode : GeneratorUnderNode
    {
        // HERE: Declear variables for batched store
        // e.g. public Func<float> ratio;
        public Vector3 generalDirection;
        public int stepMin, stepMax;
        public float dircStrength, connection;
        public uint block;

        // Init the UnderNode if needed
        public override void Init()
        {
            base.Init();
		}

        // This creates the actual Instance, i.e. calculation agent.
        public override NodeInstance NewInstance(Seed seed, World world)
        {
            var inst = new SingleBlockGrowthInstance();

            // HERE: Assign attributes to your instance
            // e.g. inst.ratio = ratio();
            inst.generalDirection = generalDirection;
            inst.stepMax = stepMax;
            inst.stepMin = stepMin;
            inst.block = block;
            inst.dircStrength = dircStrength;
            inst.connection = connection;

            return inst;
        }
    }

    public class SingleBlockGrowth : BatchedPlanterNode
    {
        // HERE: Declear variables for node graph
        // e.g. [Input] public RoadMap<float> ratio = new C<float>(5.0f);
        public Vector3 generalDirection;
        public int stepMin, stepMax;
        public float dircStrength, connection;
        public uint block;

        // Use this for initialization
        protected override void Init() 
        {
            base.Init();
        }

        // Return the correct value of an output port when requested
        public override object GetValue(NodePort port)
        {
            object v = base.GetValue(port);

            // HERE: Modify if you have additional output ports

            return v;
        }

        // Build() is called when the graph need to populate a "Underlying graph" for actual generation.
        // This should return a UnderNode which can do (batched) calculation.
        public override AbsUnderNode Build()
        {
            SingleBlockGrowthUnderNode n = base.Build() as SingleBlockGrowthUnderNode;

            // HERE: Retrieve values from the node graph\
            // e.g. n.ratio = GetInputValue("ratio", ratio).realized;
            n.generalDirection = generalDirection;
            n.stepMin = stepMin;
            n.stepMax = stepMax;
            n.block = block;

            n.dircStrength = dircStrength;
            n.connection = connection;

            return n;
        }

        protected override AbsUnderNode NewUndernode()
        {
            return new SingleBlockGrowthUnderNode();
        }
    }
}