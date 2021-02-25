using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;

namespace Matryoshka
{
    public interface IVoxelizableInstance
    {
        void Voxelize(World world);
    }

    [System.Serializable]
    public abstract class NodeInstance
    {
        [SerializeField]
        protected Seed origin;

        public virtual void _Build(Seed origin, World world)
        {
            this.origin = origin;
            Build(world);
        }
        public abstract void Build(World world);
        public virtual void Voxelize(World world)
        {
            Debug.LogWarning("NodeInstance.Voxelize() being called without override");
        }
    }

    [System.Serializable]
    public abstract class PlanterNodeInstance : NodeInstance
    {
        protected List<Seed> seeds;

        public override void _Build(Seed origin, World world)
        {
            seeds = new List<Seed>();
            base._Build(origin, world);
        }

        protected Seed NewSeed(Vector3 pos, Quaternion rot, float power, int id)
        {
            return new Seed(origin, pos, rot, power, id);
        }

        public virtual IEnumerable<Seed> GetSeed()
        {
            return seeds;
        }
    }

    [System.Serializable]
    public struct Seed
    {
        // Consts
        public static Seed identity = new Seed() { id = 0, self = Matrix4x4.identity, parent = Matrix4x4.identity, power = 1.0f };

        // Constructors
        public Seed(Seed origin, Vector3 pos, Quaternion rot, float power, int id)
        {
            self = Matrix4x4.TRS(pos, rot, Vector3.one);
            parent = origin.world;
            this.power = power;
            this.id = id;
        }

        public Seed(Vector3 worldPos, Quaternion worldRot, float power, int id)
        {
            if(float.IsNaN(worldRot.x + worldRot.y + worldRot.z + worldRot.w))
            {
                Debug.Log("NOOOO");
            }
            self = Matrix4x4.TRS(worldPos, worldRot, Vector3.one);
            parent = Matrix4x4.identity;
            this.power = power;
            this.id = id;
        }

        // Stores in memory
        public Matrix4x4 self, parent;
        public float power;
        public int id;

        // Getters
        public Matrix4x4 world
        {
            get
            {
                return parent * self;
            }
        }

        public Vector3 localPosition
        {
            get
            {
                return self.GetColumn(3);
            }
        }

        public Vector3 position
        {
            get
            {
                return world.GetColumn(3);
            }
        }

        public Vector3 xAxis { get { return world.GetColumn(0); } }
        public Vector3 yAxis { get { return world.GetColumn(1); } }
        public Vector3 zAxis { get { return world.GetColumn(2); } }

        public Vector3 localNormal { get { return self.MultiplyVector(Vector3.forward).normalized; } }
        public Vector3 normal { get { return world.MultiplyVector(Vector3.forward).normalized; } }
    }
}