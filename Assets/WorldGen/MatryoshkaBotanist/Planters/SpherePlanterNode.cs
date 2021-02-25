#define PROFILE

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;

namespace Matryoshka.Planters
{
    public class SpherePlanterInstance : PlanterNodeInstance
    {
        public float radius;
        public int pointCount;

        public override void Build(World world)
        {
#if PROFILE
            UnityEngine.Profiling.Profiler.BeginSample("SpherePlanterInstance.Build");
#endif
            Vector3Int pi = Vector3Int.FloorToInt(origin.position);
            System.Random random = new System.Random(pi.x ^ pi.y ^ pi.z);
            // Random
            for(int i = 0; i < pointCount; i++)
            {
                Vector3 pos = new Vector3(
                    Utils.MathCore.RandomRange(random, -1, 1),
                    Utils.MathCore.RandomRange(random, -1, 1),
                    Utils.MathCore.RandomRange(random, -1, 1)).normalized * radius * origin.power;

                seeds.Add(NewSeed(pos, Quaternion.identity, origin.power, i));
            }
#if PROFILE
            UnityEngine.Profiling.Profiler.EndSample();
#endif
        }
    }

    public class SpherePlanterUnderNode : BatchedPlanterUnderNode
    {
        public Func<float> radius;
        public Func<int> pointCount;

        public override NodeInstance NewInstance(Seed seed, World world)
        {
            var inst = new SpherePlanterInstance();

            inst.radius = radius();
            inst.pointCount = pointCount();

            return inst;
        }
    }

    public class SpherePlanterNode : BatchedPlanterNode
    {
        [Input] public RoadMap<float> radius = new C<float>(5.0f);
        [Input] public RoadMap<int> pointCount = new C<int>(8);

        public override AbsUnderNode Build()
        {
            SpherePlanterUnderNode n = base.Build() as SpherePlanterUnderNode;

            n.radius = GetInputValue("radius", radius).realized;
            n.pointCount = GetInputValue("pointCount", pointCount).realized;

            return n;
        }

        protected override AbsUnderNode NewUndernode()
        {
            return new SpherePlanterUnderNode();
        }
    }
}