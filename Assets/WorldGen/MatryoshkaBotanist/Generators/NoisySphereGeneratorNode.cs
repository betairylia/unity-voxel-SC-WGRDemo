using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Voxelis;
using XNode;

namespace Matryoshka.Generators
{
    public class NoisySphereInstance : NodeInstance
	{
		public Utils.PerThreadPool<FastNoiseLite> noise;
		public uint block, blockVariant;
		public float variantRate, noiseScale;
		public float rawRadius, noiseFreq;

		//[NativeSetThreadIndex]
		//int threadIndex;

		public void FillNoisySphere(World world, uint id, Vector3 o, float r, FastNoiseLite noise, float noiseScale, uint idVariant, float variantRate)
		{
			var rand = new System.Random();

			Vector3Int min = Vector3Int.FloorToInt(o) - Vector3Int.one * Mathf.CeilToInt(r + noiseScale);
			Vector3Int max = Vector3Int.CeilToInt(o) + Vector3Int.one * Mathf.CeilToInt(r + noiseScale);

			for (int x = min.x; x <= max.x; x++)
				for (int y = min.y; y <= max.y; y++)
					for (int z = min.z; z <= max.z; z++)
					{
						if ((new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) - o).magnitude <= (r + 0.35f + noise.GetNoise(x * noiseFreq + 3.75f, y * noiseFreq - 18.63f, z * noiseFreq + 1.55f) * noiseScale))
						{
							if (rand.NextDouble() < variantRate)
							{
								world.SetBlock(new Vector3Int(x, y, z), Block.From32bitColor(idVariant));
							}
							else
							{
								world.SetBlock(new Vector3Int(x, y, z), Block.From32bitColor(id));
							}
						}
					}
		}

		public override void Voxelize(World world)
        {
			FillNoisySphere(world, block, origin.position, rawRadius * origin.power, noise.Get(Thread.CurrentThread.ManagedThreadId), noiseScale, blockVariant, variantRate);
        }

        public override void Build(World world)
        {
            // Nothing to build ...
        }
    }

    public class NoisySphereGeneratorUnderNode : UnseedGeneratorUnderNode
	{
		public Func<float> noiseFreq;
		public Func<uint> block, blockVariant;
		public Func<float> variantRate, noiseScale, rawRadius;

        public override void Init()
        {
            base.Init();
		}

		public override NodeInstance NewInstance(Seed seed, World world)
		{
			var inst = new NoisySphereInstance();

			inst.noise = Utils.NoisePools.OS2S_FBm_3oct_f1;
			inst.block = block();
			inst.blockVariant = blockVariant();
			inst.variantRate = variantRate();
			inst.noiseScale = noiseScale();
			inst.rawRadius = rawRadius();
			inst.noiseFreq = noiseFreq();

			return inst;
		}
	}

	[System.Serializable]
	public class NoisySphereGeneratorNode : BatchedNode
    {
		[Input] public RoadMap<float> noiseFreq = new C<float>(0.1f);
		[Input] public RoadMap<uint> block = new C<uint>(0x317d18ff), blockVariant = new C<uint>(0x51991aff);
		[Input] public RoadMap<float> variantRate = new C<float>(0.3f), noiseScale = new C<float>(5.0f), rawRadius = new C<float>(5.0f);

		// Return the correct value of an output port when requested
		public override object GetValue(NodePort port)
		{
			return null; // Replace this
		}

        public override AbsUnderNode Build()
        {
			NoisySphereGeneratorUnderNode n = base.Build() as NoisySphereGeneratorUnderNode;

			n.noiseFreq = GetInputValue("noiseFreq", noiseFreq).realized;
			n.block = GetInputValue("block", block).realized;
			n.blockVariant = GetInputValue("blockVariant", blockVariant).realized;
			n.variantRate = GetInputValue("variantRate", variantRate).realized;
			n.noiseScale = GetInputValue("noiseScale", noiseScale).realized;
			n.rawRadius = GetInputValue("rawRadius", rawRadius).realized;

			return n;
		}

        protected override AbsUnderNode NewUndernode()
        {
			return new NoisySphereGeneratorUnderNode();
        }
    }
}
