using System;
using UnityEngine;

namespace PCGLand
{
    /// <summary>整数三维分块网格坐标，与世界坐标互转。</summary>
    [Serializable]
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int x;
        public int y;
        public int z;

        public ChunkCoord(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>分块原点（最小角）的世界坐标。</summary>
        public Vector3 ToWorldOrigin(float chunkSize)
        {
            return new Vector3(x * chunkSize, y * chunkSize, z * chunkSize);
        }

        /// <summary>给定世界坐标落入的分块坐标。</summary>
        public static ChunkCoord FromWorld(Vector3 worldPos, float chunkSize)
        {
            return new ChunkCoord(
                Mathf.FloorToInt(worldPos.x / chunkSize),
                Mathf.FloorToInt(worldPos.y / chunkSize),
                Mathf.FloorToInt(worldPos.z / chunkSize));
        }

        public bool Equals(ChunkCoord other) => x == other.x && y == other.y && z == other.z;

        public override bool Equals(object obj) => obj is ChunkCoord o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + x;
                h = h * 31 + y;
                h = h * 31 + z;
                return h;
            }
        }

        public override string ToString() => $"({x},{y},{z})";
    }
}
