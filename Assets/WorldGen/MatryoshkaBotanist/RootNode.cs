using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Matryoshka
{
    public class RootUnderNode : AbsUnderNode
    {
		public Seed worldSeed;

		public Vector3 rootPos = new Vector3(0, 0, 0);
		public Vector3 rootNormal = new Vector3(0, 1, 0);
		public float rootPower = 1.0f;

		public List<Seed> root;

		public override void Build(World world)
        {
			root = new List<Seed>();
			root.Add(new Seed(worldSeed, rootPos, Quaternion.FromToRotation(Vector3.up
				, rootNormal), rootPower, 0));
		}
    }

    [System.Serializable]
	public class RootNode : AbsNode
	{
		[Header("Bounds")]
		public Vector3Int boundSize = new Vector3Int(0, 0, 0);
		public Vector3Int seedCenter = new Vector3Int(0, 0, 0);

		[Space(15.0f)]

		public Vector3 rootPos = new Vector3(0, 0, 0);
		public Vector3 rootNormal = new Vector3(0, 1, 0);
		public float rootPower = 1.0f;

		[Space]

		[Output] public RoadMap<List<Seed>> root;

		// Return the correct value of an output port when requested
		public override object GetValue(NodePort port)
		{
			if(port.fieldName == "root")
            {
				return root;
            }
			return null; // Replace this
		}

        public override AbsUnderNode Build()
        {
			RootUnderNode n = new RootUnderNode();

			// Set parameters
			n.rootPos = rootPos;
			n.rootNormal = rootNormal;
			n.rootPower = rootPower;

			// Fill output ports
			root.realized = () => n.root;

			return n;
        }

        protected override AbsUnderNode NewUndernode()
        {
			return new RootUnderNode();
        }
    }
}
