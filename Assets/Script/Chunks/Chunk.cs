using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 单个分块的运行时表示：持有一个 GameObject + MeshFilter/Renderer/Mesh。
    /// 由 ChunkPool 创建与复用。
    /// </summary>
    public sealed class Chunk
    {
        public ChunkCoord Coord;
        public readonly GameObject GameObject;
        public readonly MeshFilter Filter;
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;
        public MeshCollider Collider; // 可选，扩展点

        public Chunk(Transform parent, Material material)
        {
            GameObject = new GameObject("Chunk");
            GameObject.transform.SetParent(parent, false);

            Mesh = new Mesh { name = "ChunkMesh" };
            Mesh.MarkDynamic();

            Filter = GameObject.AddComponent<MeshFilter>();
            Filter.sharedMesh = Mesh;

            Renderer = GameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = material;
        }

        public void Activate(ChunkCoord coord, float chunkSize)
        {
            Coord = coord;
            // 网格顶点为世界坐标，因此分块 Transform 保持原点。
            GameObject.transform.position = Vector3.zero;
            GameObject.name = $"Chunk {coord}";
            GameObject.SetActive(true);
        }

        public void Deactivate()
        {
            GameObject.SetActive(false);
            Mesh.Clear();
            if (Collider != null) Collider.sharedMesh = null;
        }
    }
}
