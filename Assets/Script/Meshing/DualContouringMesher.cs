using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 统一分辨率（无 LOD）的 Dual Contouring 网格器。
    ///
    /// 无缝策略：每个分块的 cell 索引覆盖 [0, N]（共 N+1 个 cell），
    /// 即向 +X/+Y/+Z 邻块重叠采样 1 个 cell，使 + 朝向的边界四边形闭合，
    /// 与邻块 cell 0 精确重合。成立前提是密度为单一全局连续确定性函数。
    ///
    /// 法线取自密度梯度（指向空气=外侧），与四边形绕序无关；
    /// 着色器使用 Cull Off 作为绕序兜底，保证不出现孔洞。
    /// </summary>
    public sealed class DualContouringMesher : IMesher
    {
        // 8 个角的局部偏移：x=i&1, y=(i>>1)&1, z=(i>>2)&1
        private static readonly int[] CornerX = { 0, 1, 0, 1, 0, 1, 0, 1 };
        private static readonly int[] CornerY = { 0, 0, 1, 1, 0, 0, 1, 1 };
        private static readonly int[] CornerZ = { 0, 0, 0, 0, 1, 1, 1, 1 };

        // 12 条边（角索引对）
        private static readonly int[] EdgeA = { 0, 2, 4, 6, 0, 1, 4, 5, 0, 1, 2, 3 };
        private static readonly int[] EdgeB = { 1, 3, 5, 7, 2, 3, 6, 7, 4, 5, 6, 7 };

        public MeshData Build(IDensityField field, ChunkCoord coord, WorldSettings settings, CancellationToken token)
        {
            if (settings.debugVoxelBlocks)
            {
                return BuildVoxelBlocks(field, coord, settings, token);
            }

            int n = settings.voxelResolution;          // 每轴 cell 数
            int cells = n + 1;                          // cell 索引 0..N（含 +1 重叠）
            int nodes = n + 2;                          // 角节点 0..N+1
            float cellSize = settings.chunkSize / n;
            float iso = settings.isoLevel;
            Vector3 origin = coord.ToWorldOrigin(settings.chunkSize);

            // 1) 采样所有角节点密度
            var density = new float[nodes * nodes * nodes];
            for (int i = 0; i < nodes; i++)
            {
                if (token.IsCancellationRequested) return null;
                for (int j = 0; j < nodes; j++)
                {
                    for (int k = 0; k < nodes; k++)
                    {
                        Vector3 p = origin + new Vector3(i, j, k) * cellSize;
                        density[NodeIdx(i, j, k, nodes)] = field.Sample(p);
                    }
                }
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();

            // 2) 每个含变号边的 cell 生成一个顶点
            var cellVertex = new int[cells * cells * cells];
            for (int i = 0; i < cellVertex.Length; i++) cellVertex[i] = -1;

            for (int ci = 0; ci < cells; ci++)
            {
                if (token.IsCancellationRequested) return null;
                for (int cj = 0; cj < cells; cj++)
                {
                    for (int ck = 0; ck < cells; ck++)
                    {
                        var qef = new Qef();
                        // 该 cell 的 8 个角密度
                        // 检查 12 条边的变号
                        for (int e = 0; e < 12; e++)
                        {
                            int a = EdgeA[e];
                            int b = EdgeB[e];
                            float dA = density[NodeIdx(ci + CornerX[a], cj + CornerY[a], ck + CornerZ[a], nodes)];
                            float dB = density[NodeIdx(ci + CornerX[b], cj + CornerY[b], ck + CornerZ[b], nodes)];
                            bool solidA = dA < iso;
                            bool solidB = dB < iso;
                            if (solidA == solidB) continue;

                            float t = Mathf.Approximately(dB, dA) ? 0.5f : (iso - dA) / (dB - dA);
                            t = Mathf.Clamp01(t);

                            Vector3 pa = origin + new Vector3(ci + CornerX[a], cj + CornerY[a], ck + CornerZ[a]) * cellSize;
                            Vector3 pb = origin + new Vector3(ci + CornerX[b], cj + CornerY[b], ck + CornerZ[b]) * cellSize;
                            Vector3 hit = Vector3.Lerp(pa, pb, t);
                            Vector3 nrm = field.Gradient(hit);
                            qef.Add(hit, nrm);
                        }

                        if (qef.Count == 0) continue;

                        Vector3 cellMin = origin + new Vector3(ci, cj, ck) * cellSize;
                        Vector3 cellMax = cellMin + new Vector3(cellSize, cellSize, cellSize);
                        Vector3 vpos = qef.Solve(cellMin, cellMax);

                        cellVertex[CellIdx(ci, cj, ck, cells)] = verts.Count;
                        verts.Add(vpos);
                        normals.Add(field.Gradient(vpos));
                        colors.Add(field.SampleColor(vpos));
                    }
                }
            }

            // 3) 每条变号的节点边生成一个四边形（其周围 4 个 cell 的顶点）
            for (int i = 0; i < nodes; i++)
            {
                if (token.IsCancellationRequested) return null;
                for (int j = 0; j < nodes; j++)
                {
                    for (int k = 0; k < nodes; k++)
                    {
                        float d0 = density[NodeIdx(i, j, k, nodes)];

                        // X 边: (i,j,k)-(i+1,j,k)，周围 cell 的 y∈{j-1,j}, z∈{k-1,k}, x=i
                        if (i < nodes - 1 && j >= 1 && j <= cells - 1 && k >= 1 && k <= cells - 1)
                        {
                            float d1 = density[NodeIdx(i + 1, j, k, nodes)];
                            TryQuad(d0, d1, iso, cellVertex, cells, tris,
                                i, j - 1, k - 1, i, j, k - 1, i, j, k, i, j - 1, k);
                        }

                        // Y 边: (i,j,k)-(i,j+1,k)，周围 cell 的 x∈{i-1,i}, z∈{k-1,k}, y=j
                        if (j < nodes - 1 && i >= 1 && i <= cells - 1 && k >= 1 && k <= cells - 1)
                        {
                            float d1 = density[NodeIdx(i, j + 1, k, nodes)];
                            TryQuad(d0, d1, iso, cellVertex, cells, tris,
                                i - 1, j, k - 1, i, j, k - 1, i, j, k, i - 1, j, k);
                        }

                        // Z 边: (i,j,k)-(i,j,k+1)，周围 cell 的 x∈{i-1,i}, y∈{j-1,j}, z=k
                        if (k < nodes - 1 && i >= 1 && i <= cells - 1 && j >= 1 && j <= cells - 1)
                        {
                            float d1 = density[NodeIdx(i, j, k + 1, nodes)];
                            TryQuad(d0, d1, iso, cellVertex, cells, tris,
                                i - 1, j - 1, k, i, j - 1, k, i, j, k, i - 1, j, k);
                        }
                    }
                }
            }

            return new MeshData
            {
                Vertices = verts.ToArray(),
                Normals = normals.ToArray(),
                Colors = colors.ToArray(),
                Triangles = tris.ToArray(),
            };
        }

        // 6 个面的外法线方向（与下方 FaceCorners 一一对应）。
        private static readonly Vector3[] FaceNormals =
        {
            new Vector3( 1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3( 0, 1, 0), new Vector3( 0,-1, 0),
            new Vector3( 0, 0, 1), new Vector3( 0, 0,-1),
        };

        // 每个面的 4 个角（单位 cell 局部坐标，逆时针朝外）。
        private static readonly Vector3[][] FaceCorners =
        {
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) }, // +X
            new[] { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0) }, // -X
            new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) }, // +Y
            new[] { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1), new Vector3(0,0,1) }, // -Y
            new[] { new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1) }, // +Z
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) }, // -Z
        };

        // 6 个面对应的邻居 cell 偏移（与 FaceNormals 对应）。
        private static readonly int[] FaceDX = { 1, -1, 0, 0, 0, 0 };
        private static readonly int[] FaceDY = { 0, 0, 1, -1, 0, 0 };
        private static readonly int[] FaceDZ = { 0, 0, 0, 0, 1, -1 };

        /// <summary>
        /// 调试方块网格：每个 cell 取中心密度判定实心，实心 cell 渲为立方体，
        /// 仅输出朝向空气邻居的外表面。邻居 cell 中心密度跨分块连续采样，
        /// 故分块交界处的共享面会被剔除，不产生重叠双层壁。
        /// </summary>
        private MeshData BuildVoxelBlocks(IDensityField field, ChunkCoord coord, WorldSettings settings, CancellationToken token)
        {
            int n = settings.voxelResolution;
            float cellSize = settings.chunkSize / n;
            float iso = settings.isoLevel;
            Vector3 origin = coord.ToWorldOrigin(settings.chunkSize);

            // 覆盖 cell 索引 -1..n（共 n+2），多出的一圈用于邻居实心判定。
            int s = n + 2;
            var solid = new bool[s * s * s];
            for (int ci = -1; ci <= n; ci++)
            {
                if (token.IsCancellationRequested) return null;
                for (int cj = -1; cj <= n; cj++)
                {
                    for (int ck = -1; ck <= n; ck++)
                    {
                        Vector3 center = origin + new Vector3(ci + 0.5f, cj + 0.5f, ck + 0.5f) * cellSize;
                        solid[SolidIdx(ci, cj, ck, s)] = field.Sample(center) < iso;
                    }
                }
            }

            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();

            for (int ci = 0; ci < n; ci++)
            {
                if (token.IsCancellationRequested) return null;
                for (int cj = 0; cj < n; cj++)
                {
                    for (int ck = 0; ck < n; ck++)
                    {
                        if (!solid[SolidIdx(ci, cj, ck, s)]) continue;

                        Vector3 cellMin = origin + new Vector3(ci, cj, ck) * cellSize;
                        Vector3 cellCenter = cellMin + new Vector3(0.5f, 0.5f, 0.5f) * cellSize;
                        Color color = field.SampleColor(cellCenter);

                        for (int f = 0; f < 6; f++)
                        {
                            // 邻居为实心则该面在内部，跳过。
                            if (solid[SolidIdx(ci + FaceDX[f], cj + FaceDY[f], ck + FaceDZ[f], s)]) continue;

                            Vector3[] fc = FaceCorners[f];
                            Vector3 nrm = FaceNormals[f];
                            int baseIdx = verts.Count;
                            for (int v = 0; v < 4; v++)
                            {
                                verts.Add(cellMin + Vector3.Scale(fc[v], new Vector3(cellSize, cellSize, cellSize)));
                                normals.Add(nrm);
                                colors.Add(color);
                            }
                            tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2);
                            tris.Add(baseIdx); tris.Add(baseIdx + 2); tris.Add(baseIdx + 3);
                        }
                    }
                }
            }

            return new MeshData
            {
                Vertices = verts.ToArray(),
                Normals = normals.ToArray(),
                Colors = colors.ToArray(),
                Triangles = tris.ToArray(),
            };
        }

        // 实心网格索引：cell 索引范围 -1..n，整体 +1 偏移到 0..n+1。
        private static int SolidIdx(int i, int j, int k, int s) => ((i + 1) * s + (j + 1)) * s + (k + 1);

        // 若该节点边变号，连接 4 个 cell 顶点为四边形（两个三角形）。
        private static void TryQuad(
            float d0, float d1, float iso, int[] cellVertex, int cells, List<int> tris,
            int ax, int ay, int az, int bx, int by, int bz,
            int cx, int cy, int cz, int dx, int dy, int dz)
        {
            bool s0 = d0 < iso;
            bool s1 = d1 < iso;
            if (s0 == s1) return; // 无变号

            int v0 = cellVertex[CellIdx(ax, ay, az, cells)];
            int v1 = cellVertex[CellIdx(bx, by, bz, cells)];
            int v2 = cellVertex[CellIdx(cx, cy, cz, cells)];
            int v3 = cellVertex[CellIdx(dx, dy, dz, cells)];
            if (v0 < 0 || v1 < 0 || v2 < 0 || v3 < 0) return; // 某 cell 无顶点（不应发生，安全跳过）

            // d0<iso（实心在低节点侧、空气在 +轴侧）时翻转绕序，使法线朝 +轴。
            if (s0)
            {
                tris.Add(v0); tris.Add(v2); tris.Add(v1);
                tris.Add(v0); tris.Add(v3); tris.Add(v2);
            }
            else
            {
                tris.Add(v0); tris.Add(v1); tris.Add(v2);
                tris.Add(v0); tris.Add(v2); tris.Add(v3);
            }
        }

        private static int NodeIdx(int i, int j, int k, int nodes) => (i * nodes + j) * nodes + k;

        private static int CellIdx(int i, int j, int k, int cells) => (i * cells + j) * cells + k;
    }
}
