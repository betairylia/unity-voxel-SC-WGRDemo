using System.Collections;
using UnityEngine;

namespace Voxelis.Rendering
{
    // TODO: LOD ?
    public abstract class ChunkRenderableBase
    {
        public Vector3 position { get; protected set; }
        public Vector3 renderPosition { get; protected set; }

        public Bounds bound
        {
            get;
            protected set;
        }

        public abstract void Init(BlockGroup group, Chunk chunk);
        public abstract void GenerateGeometry(BlockGroup group, Chunk chunk);
        public abstract void Render(BlockGroup group);
        public abstract void Clean();
        public abstract bool IsReadyForPresent();

        public abstract uint GetVertCount();
        public abstract Chunk GetChunk();
    }
}