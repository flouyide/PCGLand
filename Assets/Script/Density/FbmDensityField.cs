using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 基础多倍频 fBm 密度场。density = (y - 地表高度)，由 Biome 调制幅值。
    /// 法线用中心有限差分。完全无状态、可复现。
    /// </summary>
    public sealed class FbmDensityField : IDensityField
    {
        private readonly uint _seed;
        private readonly float _groundHeight;
        private readonly float _amplitude;
        private readonly float _baseFrequency;
        private readonly int _octaves;
        private readonly float _lacunarity;
        private readonly float _gain;
        private readonly BiomeSampler _biome;

        // 有限差分步长（米）
        private const float Eps = 0.5f;

        public FbmDensityField(WorldSettings settings, BiomeSampler biome)
        {
            _seed = (uint)settings.seed;
            _groundHeight = settings.groundHeight;
            _amplitude = settings.terrainAmplitude;
            _baseFrequency = settings.baseFrequency;
            _octaves = Mathf.Max(1, settings.octaves);
            _lacunarity = settings.lacunarity;
            _gain = settings.gain;
            _biome = biome;
        }

        /// <summary>该 (x,z) 处的地表高度。</summary>
        private float SurfaceHeight(float x, float z)
        {
            float ampScale = _biome.AmplitudeScale(x, z);
            float n = Hash.Fbm(x, 0f, z, _seed, _octaves, _baseFrequency, _lacunarity, _gain);
            return _groundHeight + n * _amplitude * ampScale;
        }

        public float Sample(Vector3 worldPos)
        {
            // 有符号：地表以下为负（实心），以上为正（空气）。
            return worldPos.y - SurfaceHeight(worldPos.x, worldPos.z);
        }

        public Vector3 Gradient(Vector3 p)
        {
            float dx = Sample(new Vector3(p.x + Eps, p.y, p.z)) - Sample(new Vector3(p.x - Eps, p.y, p.z));
            float dy = Sample(new Vector3(p.x, p.y + Eps, p.z)) - Sample(new Vector3(p.x, p.y - Eps, p.z));
            float dz = Sample(new Vector3(p.x, p.y, p.z + Eps)) - Sample(new Vector3(p.x, p.y, p.z - Eps));
            Vector3 g = new Vector3(dx, dy, dz);
            float m = g.magnitude;
            return m > 1e-6f ? g / m : Vector3.up;
        }

        public Color SampleColor(Vector3 worldPos)
        {
            return _biome.SampleColor(worldPos, _groundHeight, _amplitude);
        }
    }
}
