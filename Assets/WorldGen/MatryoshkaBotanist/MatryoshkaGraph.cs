#define PROFILE

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Voxelis;
using XNode;

namespace Matryoshka
{
    [System.Serializable]
    [CreateAssetMenu(menuName = "Matryoshka (Progression Graph)")]
    public class MatryoshkaGraph : NodeGraph
    {
        public RootNode root;

        void OnEnable()
        {
            root = null;
            foreach (var n in this.nodes)
            {
                if(n is RootNode)
                {
                    root = n as RootNode;
                }    
            }

            if(root == null)
            {
                Debug.LogError($"MatryoshkaGraph {this.name} has no root node.");
            }
        }

        public BoundsInt GetBounds(Vector3Int seedPos)
        {
            BoundsInt b = new BoundsInt(seedPos - root.seedCenter, root.boundSize);
            return b;
        }

        public MatryoshkaGenerator NewGenerator()
        {
            if(root)
            {
#if PROFILE
                UnityEngine.Profiling.Profiler.BeginSample("Matryoshka.NewGenerator");
#endif
                RootUnderNode n = buildNodeRec(root) as RootUnderNode;
#if PROFILE
                UnityEngine.Profiling.Profiler.EndSample();
#endif
                return new MatryoshkaGenerator(n, root.seedCenter);
            }

            return null;
        }

        // TODO: Build and cache this, DO NOT build this everytime OMG ...
        AbsUnderNode buildNodeRec(AbsNode node)
        {
            AbsUnderNode result = node._Build();

            LinkedList<AbsUnderNode> children = new LinkedList<AbsUnderNode>();
            foreach (var o in node.Outputs)
            {
                foreach (var c in o.GetConnections())
                {
                    if (c.node is AbsNode)
                    {
                        children.AddLast(buildNodeRec(c.node as AbsNode));
                    }
                }
            }

            result.children = children;
            return result;
        }
    }

    public class MatryoshkaGenerator : IStructureGenerator
    {
        RootUnderNode root;
        Vector3Int offset;

        public MatryoshkaGenerator(RootUnderNode root, Vector3Int offset)
        {
            this.root = root;
            this.offset = offset;
        }

        public void GenerateRec(AbsUnderNode node, World world)
        {
            node.Build(world);

            foreach(var n in node.children)
            {
                GenerateRec(n, world);
            }
        }

        public void Generate(BoundsInt bound, World world)
        {
            if(root != null)
            {
#if PROFILE
                UnityEngine.Profiling.Profiler.BeginSample("MatryoshkaGenerator.Generate");
#endif
                root.worldSeed = new Seed(Seed.identity, bound.min + offset + root.rootPos, Quaternion.identity, 1.0f, 0);
                GenerateRec(root, world);
#if PROFILE
                UnityEngine.Profiling.Profiler.EndSample();
#endif
            }
        }
    }
}
