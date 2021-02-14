using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Matryoshka
{
    [System.Serializable]
	public class RoadMap<T>
	{
		[SerializeField]
		protected T val;

		public Func<T> realized = () => { Debug.LogError("Roadmap unrealized when built !"); return default(T); };
	}

    [System.Serializable]
	public class C<T> : RoadMap<T>
	{
		public C(T v)
		{
			val = v;
			realized = () => val;
		}

		public static Func<T> f(T v)
        {
			return () => v;
        }
    }

	public abstract class AbsNode : Node
	{
		public bool built { get; protected set; } = false;

		public abstract AbsUnderNode Build();
		public AbsUnderNode _Build() { AbsUnderNode n = Build(); n.Init(); built = true; return n; }

		protected abstract AbsUnderNode NewUndernode();
	}

	public abstract class BatchedNode : AbsNode
	{
		[Input] public RoadMap<List<Seed>> origins;

		public override AbsUnderNode Build()
		{
			BatchedUnderNode n = NewUndernode() as BatchedUnderNode;
			n.origins = GetInputValue< RoadMap<List<Seed>> >("origins").realized;

			return n;
		}
	}

	public abstract class BatchedPlanterNode : BatchedNode
	{
        [Output] public RoadMap<List<Seed>> seeds;

		public bool generateSeed = true;

		// Return the correct value of an output port when requested
		public override object GetValue(NodePort port)
		{
			if(port.fieldName == "seeds") { return seeds; }
			return null; // Replace this
		}

        public override AbsUnderNode Build()
        {
			BatchedPlanterUnderNode n = base.Build() as BatchedPlanterUnderNode;
			n.generateSeed = generateSeed;

			// Fill output ports
			seeds.realized = () => n.cachedSeeds;

			return n;
        }
    }
}
