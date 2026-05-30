using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 确定性哈希与梯度（Perlin）噪声基元。
    /// 所有输出仅依赖于 (坐标, 种子)，因此完全无状态、可复现、多线程安全。
    /// </summary>
    public static class Hash
    {
        // 32 位整数混合（xxHash/PCG 风格）
        public static uint Mix(uint x)
        {
            x ^= x >> 16;
            x *= 2246822519u;
            x ^= x >> 13;
            x *= 3266489917u;
            x ^= x >> 16;
            return x;
        }

        public static uint Coord(int x, int y, int z, uint seed)
        {
            uint h = seed;
            h = Mix(h + (uint)x * 73856093u);
            h = Mix(h + (uint)y * 19349663u);
            h = Mix(h + (uint)z * 83492791u);
            return h;
        }

        // 返回 [0,1)
        public static float Float01(int x, int y, int z, uint seed)
        {
            return (Coord(x, y, z, seed) & 0x00FFFFFFu) / 16777216.0f;
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // 12 条标准 Perlin 梯度方向
        private static float Grad(uint hash, float x, float y, float z)
        {
            switch (hash & 15u)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x + z;
                case 5: return -x + z;
                case 6: return x - z;
                case 7: return -x - z;
                case 8: return y + z;
                case 9: return -y + z;
                case 10: return y - z;
                case 11: return -y - z;
                case 12: return x + y;
                case 13: return -y + z;
                case 14: return -x + y;
                default: return -y - z;
            }
        }

        /// <summary>3D 梯度噪声，输出近似 [-1, 1]。</summary>
        public static float Perlin3(float x, float y, float z, uint seed)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            int zi = Mathf.FloorToInt(z);
            float xf = x - xi;
            float yf = y - yi;
            float zf = z - zi;

            float u = Fade(xf);
            float v = Fade(yf);
            float w = Fade(zf);

            float n000 = Grad(Coord(xi, yi, zi, seed), xf, yf, zf);
            float n100 = Grad(Coord(xi + 1, yi, zi, seed), xf - 1f, yf, zf);
            float n010 = Grad(Coord(xi, yi + 1, zi, seed), xf, yf - 1f, zf);
            float n110 = Grad(Coord(xi + 1, yi + 1, zi, seed), xf - 1f, yf - 1f, zf);
            float n001 = Grad(Coord(xi, yi, zi + 1, seed), xf, yf, zf - 1f);
            float n101 = Grad(Coord(xi + 1, yi, zi + 1, seed), xf - 1f, yf, zf - 1f);
            float n011 = Grad(Coord(xi, yi + 1, zi + 1, seed), xf, yf - 1f, zf - 1f);
            float n111 = Grad(Coord(xi + 1, yi + 1, zi + 1, seed), xf - 1f, yf - 1f, zf - 1f);

            float x00 = Lerp(n000, n100, u);
            float x10 = Lerp(n010, n110, u);
            float x01 = Lerp(n001, n101, u);
            float x11 = Lerp(n011, n111, u);

            float y0 = Lerp(x00, x10, v);
            float y1 = Lerp(x01, x11, v);

            return Lerp(y0, y1, w);
        }

        /// <summary>多倍频 fBm，叠加 Perlin3。</summary>
        public static float Fbm(float x, float y, float z, uint seed,
            int octaves, float frequency, float lacunarity, float gain)
        {
            float sum = 0f;
            float amp = 1f;
            float norm = 0f;
            float f = frequency;
            for (int o = 0; o < octaves; o++)
            {
                // 每个倍频用偏移种子避免相位锁定
                uint os = seed + (uint)(o * 1013) + 1u;
                sum += amp * Perlin3(x * f, y * f, z * f, os);
                norm += amp;
                amp *= gain;
                f *= lacunarity;
            }
            return norm > 0f ? sum / norm : 0f;
        }
    }
}
