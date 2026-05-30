using UnityEngine;
using UnityEngine.Rendering;

namespace PCGLand
{
    /// <summary>
    /// 纯数据网格容器（POCO）。在工作线程上填充，仅含托管数组，
    /// 不触碰任何 Unity 对象；主线程再将其上传到 UnityEngine.Mesh。
    /// </summary>
    public sealed class MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public Color[] Colors;     // 每顶点 Biome 颜色
        public int[] Triangles;

        public bool IsEmpty => Triangles == null || Triangles.Length == 0;

        /// <summary>主线程调用：把数据写入给定 Mesh（先清空）。</summary>
        public void UploadTo(Mesh mesh)
        {
            mesh.Clear();
            if (IsEmpty)
            {
                return;
            }

            // 体素分块顶点数可能超过 65535，使用 32 位索引。
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Triangles, 0, true);
            mesh.RecalculateBounds();
        }
    }
}
