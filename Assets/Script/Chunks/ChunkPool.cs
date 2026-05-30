using System.Collections.Generic;
using UnityEngine;

namespace PCGLand
{
    /// <summary>复用 Chunk 的 GameObject/Mesh，避免频繁分配与 GC。</summary>
    public sealed class ChunkPool
    {
        private readonly Transform _parent;
        private readonly Material _material;
        private readonly Stack<Chunk> _idle = new Stack<Chunk>();

        public ChunkPool(Transform parent, Material material)
        {
            _parent = parent;
            _material = material;
        }

        public Chunk Rent(ChunkCoord coord, float chunkSize)
        {
            Chunk chunk = _idle.Count > 0 ? _idle.Pop() : new Chunk(_parent, _material);
            chunk.Activate(coord, chunkSize);
            return chunk;
        }

        public void Return(Chunk chunk)
        {
            chunk.Deactivate();
            _idle.Push(chunk);
        }
    }
}
