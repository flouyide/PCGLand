using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 由低频噪声场（温度/湿度）+ 高度派生 Biome。
    /// 输出：(a) 地形幅值乘子，(b) 用于顶点色的 Biome 颜色。
    /// 全部为种子+坐标的纯函数。
    /// </summary>
    public sealed class BiomeSampler
    {
        private readonly uint _seedTemp;
        private readonly uint _seedMoist;
        private readonly uint _seedWarp;
        private readonly float _frequency;
        private readonly float _warp;

        // 一组示例 Biome 颜色（可按需扩展）。
        private static readonly Color Snow = new Color(0.92f, 0.93f, 0.96f);
        private static readonly Color Rock = new Color(0.45f, 0.42f, 0.38f);
        private static readonly Color Grass = new Color(0.32f, 0.55f, 0.22f);
        private static readonly Color Sand = new Color(0.76f, 0.70f, 0.45f);
        private static readonly Color Forest = new Color(0.18f, 0.40f, 0.20f);

        public BiomeSampler(WorldSettings settings)
        {
            uint s = (uint)settings.seed;
            _seedTemp = Hash.Mix(s + 101u);
            _seedMoist = Hash.Mix(s + 202u);
            _seedWarp = Hash.Mix(s + 303u);
            _frequency = settings.biomeFrequency;
            _warp = settings.biomeWarp;
        }

        /// <summary>地形幅值乘子（不同 Biome 起伏程度不同），范围约 [0.4, 1.6]。</summary>
        public float AmplitudeScale(float worldX, float worldZ)
        {
            float t = Temperature(worldX, worldZ); // 0..1
            // 寒冷/炎热区更崎岖，温和区更平缓
            return 0.6f + Mathf.Abs(t - 0.5f) * 2f;
        }

        /// <summary>温度场 0..1（含域扰动）。</summary>
        public float Temperature(float worldX, float worldZ)
        {
            float wx = worldX + _warp * Hash.Perlin3(worldX * _frequency * 2f, 0f, worldZ * _frequency * 2f, _seedWarp);
            float wz = worldZ + _warp * Hash.Perlin3(worldX * _frequency * 2f, 11.3f, worldZ * _frequency * 2f, _seedWarp);
            float n = Hash.Perlin3(wx * _frequency, 0f, wz * _frequency, _seedTemp);
            return Mathf.Clamp01(n * 0.5f + 0.5f);
        }

        /// <summary>湿度场 0..1。</summary>
        public float Moisture(float worldX, float worldZ)
        {
            float n = Hash.Perlin3(worldX * _frequency, 5.5f, worldZ * _frequency, _seedMoist);
            return Mathf.Clamp01(n * 0.5f + 0.5f);
        }

        /// <summary>给定世界位置的 Biome 颜色，结合高度/温度/湿度选择并混合。</summary>
        public Color SampleColor(Vector3 worldPos, float groundHeight, float amplitude)
        {
            float temp = Temperature(worldPos.x, worldPos.z);
            float moist = Moisture(worldPos.x, worldPos.z);

            // 相对高度（-1 低洼 .. +1 高海拔）
            float rel = amplitude > 0.001f
                ? Mathf.Clamp((worldPos.y - groundHeight) / amplitude, -1f, 1f)
                : 0f;

            // 高海拔：雪/岩；中海拔：草/森林；低洼：沙
            Color baseCol;
            if (rel > 0.55f)
            {
                baseCol = Color.Lerp(Rock, Snow, Mathf.InverseLerp(0.55f, 1f, rel) * temp);
            }
            else if (rel < -0.45f)
            {
                baseCol = Sand;
            }
            else
            {
                baseCol = Color.Lerp(Grass, Forest, moist);
                // 寒冷处草地偏向岩石
                baseCol = Color.Lerp(baseCol, Rock, Mathf.Clamp01(0.5f - temp) * 0.8f);
            }
            return baseCol;
        }
    }
}
