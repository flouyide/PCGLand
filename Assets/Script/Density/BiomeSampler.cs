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

        // Biome 颜色与高度阈值（来自 WorldSettings，可在 Inspector 调整）。
        private readonly Color _snow;
        private readonly Color _rock;
        private readonly Color _grass;
        private readonly Color _sand;
        private readonly Color _forest;
        private readonly float _snowRockHeight;
        private readonly float _sandHeight;
        private readonly float _coldRockBias;
        private readonly float _reliefNormalize;

        public BiomeSampler(WorldSettings settings)
        {
            uint s = (uint)settings.seed;
            _seedTemp = Hash.Mix(s + 101u);
            _seedMoist = Hash.Mix(s + 202u);
            _seedWarp = Hash.Mix(s + 303u);
            _frequency = settings.biomeFrequency;
            _warp = settings.biomeWarp;

            _snow = settings.snowColor;
            _rock = settings.rockColor;
            _grass = settings.grassColor;
            _sand = settings.sandColor;
            _forest = settings.forestColor;
            _snowRockHeight = settings.snowRockHeight;
            _sandHeight = settings.sandHeight;
            _coldRockBias = settings.coldRockBias;
            _reliefNormalize = Mathf.Max(0.01f, settings.reliefNormalize);
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

            // 相对高度（-1 低洼 .. +1 高海拔）。
            // 用「实际能达到的起伏」归一化：amplitude × 该处幅值乘子 × 噪声典型峰值。
            // fBm 实际很少触到 ±amplitude，故按整幅归一会让 rel 长期停在中间带（全绿）；
            // 这里除以真实起伏，使 rel 真正铺满 [-1,1]，雪/岩/沙才会按比例出现。
            float ampScale = AmplitudeScale(worldPos.x, worldPos.z);
            float relief = amplitude * ampScale * _reliefNormalize;
            float rel = relief > 0.001f
                ? Mathf.Clamp((worldPos.y - groundHeight) / relief, -1f, 1f)
                : 0f;

            // 高海拔：雪/岩；中海拔：草/森林；低洼：沙
            Color baseCol;
            if (rel > _snowRockHeight)
            {
                baseCol = Color.Lerp(_rock, _snow, Mathf.InverseLerp(_snowRockHeight, 1f, rel) * temp);
            }
            else if (rel < _sandHeight)
            {
                baseCol = _sand;
            }
            else
            {
                baseCol = Color.Lerp(_grass, _forest, moist);
                // 寒冷处草地偏向岩石
                baseCol = Color.Lerp(baseCol, _rock, Mathf.Clamp01(0.5f - temp) * _coldRockBias);
            }
            return baseCol;
        }
    }
}
