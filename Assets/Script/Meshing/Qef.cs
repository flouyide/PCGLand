using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 简化的二次误差函数（QEF）求解器。
    /// 累加每条 Hermite 边的 (法线 n, 交点 p)，最小化 Σ (n·(x-p))^2，
    /// 用正则化法方程 (AᵀA + λI)x = Aᵀb + λ·质心 求解，并夹紧到 cell 内。
    /// 这是 Dual Contouring 保留尖锐特征的关键。
    /// </summary>
    public struct Qef
    {
        // AᵀA 的对称 3x3（6 个独立分量）
        private float a00, a01, a02, a11, a12, a22;
        // Aᵀb
        private float b0, b1, b2;
        // 质心累加
        private Vector3 massPoint;
        private int count;

        public int Count => count;

        public void Add(Vector3 point, Vector3 normal)
        {
            float nx = normal.x, ny = normal.y, nz = normal.z;
            a00 += nx * nx; a01 += nx * ny; a02 += nx * nz;
            a11 += ny * ny; a12 += ny * nz; a22 += nz * nz;

            float d = nx * point.x + ny * point.y + nz * point.z; // n·p
            b0 += nx * d; b1 += ny * d; b2 += nz * d;

            massPoint += point;
            count++;
        }

        /// <summary>求解顶点位置，夹紧到 [cellMin, cellMax]。</summary>
        public Vector3 Solve(Vector3 cellMin, Vector3 cellMax)
        {
            if (count == 0)
            {
                return (cellMin + cellMax) * 0.5f;
            }

            Vector3 mean = massPoint / count;

            // 偏置：相对质心求解可提升数值稳定性。
            // b' = Aᵀb - AᵀA·mean
            float rb0 = b0 - (a00 * mean.x + a01 * mean.y + a02 * mean.z);
            float rb1 = b1 - (a01 * mean.x + a11 * mean.y + a12 * mean.z);
            float rb2 = b2 - (a02 * mean.x + a12 * mean.y + a22 * mean.z);

            // 正则化 AᵀA + λI
            const float lambda = 0.1f;
            float m00 = a00 + lambda, m11 = a11 + lambda, m22 = a22 + lambda;
            float m01 = a01, m02 = a02, m12 = a12;

            if (TrySolveSym3(m00, m01, m02, m11, m12, m22, rb0, rb1, rb2, out Vector3 delta))
            {
                Vector3 x = mean + delta;
                // 解可能跑出 cell，过远则退回质心。
                if (InRange(x, cellMin, cellMax, expand: 1.0f))
                {
                    return Clamp(x, cellMin, cellMax);
                }
            }
            return Clamp(mean, cellMin, cellMax);
        }

        // 解对称 3x3 线性方程组 M x = b（M 为对称正定），用伴随矩阵 / 行列式。
        private static bool TrySolveSym3(
            float m00, float m01, float m02, float m11, float m12, float m22,
            float b0, float b1, float b2, out Vector3 x)
        {
            float c00 = m11 * m22 - m12 * m12;
            float c01 = m02 * m12 - m01 * m22;
            float c02 = m01 * m12 - m02 * m11;
            float det = m00 * c00 + m01 * c01 + m02 * c02;

            if (Mathf.Abs(det) < 1e-10f)
            {
                x = Vector3.zero;
                return false;
            }

            float inv = 1f / det;
            float c11 = m00 * m22 - m02 * m02;
            float c12 = m02 * m01 - m00 * m12;
            float c22 = m00 * m11 - m01 * m01;

            x = new Vector3(
                (c00 * b0 + c01 * b1 + c02 * b2) * inv,
                (c01 * b0 + c11 * b1 + c12 * b2) * inv,
                (c02 * b0 + c12 * b1 + c22 * b2) * inv);
            return true;
        }

        private static bool InRange(Vector3 p, Vector3 min, Vector3 max, float expand)
        {
            Vector3 e = (max - min) * expand;
            return p.x >= min.x - e.x && p.x <= max.x + e.x &&
                   p.y >= min.y - e.y && p.y <= max.y + e.y &&
                   p.z >= min.z - e.z && p.z <= max.z + e.z;
        }

        private static Vector3 Clamp(Vector3 p, Vector3 min, Vector3 max)
        {
            return new Vector3(
                Mathf.Clamp(p.x, min.x, max.x),
                Mathf.Clamp(p.y, min.y, max.y),
                Mathf.Clamp(p.z, min.z, max.z));
        }
    }
}
