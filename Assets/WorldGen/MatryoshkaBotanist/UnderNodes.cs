using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;

namespace Matryoshka
{
	public interface IVoxelizableUnderNode
	{
		void Voxelize(World world);
	}

	public abstract class AbsUnderNode
	{
		public bool built { get; protected set; } = false;
		public LinkedList<AbsUnderNode> children;

		public virtual void Init() { }

		public abstract void Build(World world);
		public void _Build(World world) { Build(world); built = true; }
	}
	
	public abstract class BatchedUnderNode : AbsUnderNode
    {
		public Func<List<Seed>> origins;
		protected List<NodeInstance> instances;

		public override void Build(World world)
		{
			instances = new List<NodeInstance>();
			var currentOrigins = origins();

			foreach (Seed origin in currentOrigins)
			{
				var ins = NewInstance(origin, world);
				ins._Build(origin, world);

				// TODO: performance issue?
				ins.Voxelize(world);
				instances.Add(ins);
			}
		}

		public abstract NodeInstance NewInstance(Seed seed, World world);
	}
	public abstract class BatchedPlanterUnderNode : BatchedUnderNode
    {
		public Func<List<Seed>> seeds;
		public List<Seed> cachedSeeds { get; protected set; }

		public bool generateSeed = true;

		public override void Build(World world)
		{
			base.Build(world);
			cachedSeeds = new List<Seed>();

			foreach (var instance in instances)
			{
				cachedSeeds.AddRange(((PlanterNodeInstance)instance)?.GetSeed());
			}
		}
	}
    public abstract class UnseedGeneratorUnderNode : BatchedUnderNode, IVoxelizableUnderNode
    {
		public virtual void Voxelize(World world)
		{
			foreach (var instance in instances)
			{
				instance.Voxelize(world);
			}
		}
	}

	// I can do nothing but copy it ... really ? CSharp BAD
	public abstract class GeneratorUnderNode : BatchedPlanterUnderNode, IVoxelizableUnderNode
	{
		public virtual void Voxelize(World world)
		{
			foreach (var instance in instances)
			{
				instance.Voxelize(world);
			}
		}
	}
}