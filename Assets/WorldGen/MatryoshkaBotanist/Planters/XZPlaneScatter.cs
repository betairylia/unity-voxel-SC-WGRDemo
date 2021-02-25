using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;
using XNode;

namespace Matryoshka.Planters
{
    // Planters which only plant seeds without voxelize anything.
    public class XZPlaneScatterInstance : PlanterNodeInstance
    {
        // HERE: Declear variables for actual calculation
        // e.g. public float ratio;
        public float radius = 32;
        public int pointCount = 10;

        public override void Build(World world)
        {
            Vector3Int p = Vector3Int.FloorToInt(origin.position);
            System.Random random = new System.Random(p.x ^ p.y ^ p.z);

            // HERE: Modify to add meaningful seed to seeds
            for(int i = 0; i < pointCount; i++)
            {
                seeds.Add(NewSeed(new Vector3(Utils.MathCore.RandomRange(random, -1, 1), 0, Utils.MathCore.RandomRange(random, -1, 1)).normalized * Utils.MathCore.RandomRange(random, 0, radius), Quaternion.identity, origin.power, 0));
            }
        }
    }

    public class XZPlaneScatterUnderNode : BatchedPlanterUnderNode
    {
        // HERE: Declear variables for batched store
        // e.g. public Func<float> ratio;
        public float radius = 32;
        public int pointCount = 10;

        // Init the UnderNode if needed
        public override void Init()
        {
            base.Init();
		}

        // This creates the actual Instance, i.e. calculation agent.
        public override NodeInstance NewInstance(Seed seed, World world)
        {
            var inst = new XZPlaneScatterInstance();

            // HERE: Assign attributes to your instance
            // e.g. inst.ratio = ratio();
            inst.radius = radius;
            inst.pointCount = pointCount;

            return inst;
        }
    }

    public class XZPlaneScatter : BatchedPlanterNode
    {
        // HERE: Declear variables for node graph
        // e.g. [Input] public RoadMap<float> ratio = new C<float>(5.0f);
        public float radius = 32;
        public int pointCount = 10;

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
            XZPlaneScatterUnderNode n = base.Build() as XZPlaneScatterUnderNode;

            // HERE: Retrieve values from the node graph\
            // e.g. n.ratio = GetInputValue("ratio", ratio).realized;
            n.radius = radius;
            n.pointCount = pointCount;

            return n;
        }

        protected override AbsUnderNode NewUndernode()
        {
            return new XZPlaneScatterUnderNode();
        }
    }
}