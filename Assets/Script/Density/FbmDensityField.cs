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
        private readonly uint _ridgeSeed;
        private readonly uint _warpSeedX;
        private readonly uint _warpSeedZ;
        private readonly float _ridgedWeight;
        private readonly float _ridgeSharpness;
        private readonly float _ridgeFrequencyMultiplier;
        private readonly float _domainWarpStrength;
        private readonly float _domainWarpFrequency;
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
            _ridgeSeed = Hash.Mix(_seed + 401u);
            _warpSeedX = Hash.Mix(_seed + 503u);
            _warpSeedZ = Hash.Mix(_seed + 607u);
            _ridgedWeight = Mathf.Clamp01(settings.ridgedWeight);
            _ridgeSharpness = Mathf.Max(0.01f, settings.ridgeSharpness);
            _ridgeFrequencyMultiplier = Mathf.Max(0.01f, settings.ridgeFrequencyMultiplier);
            _domainWarpStrength = Mathf.Max(0f, settings.domainWarpStrength);
            _domainWarpFrequency = Mathf.Max(0f, settings.domainWarpFrequency);
            _biome = biome;
        }

        /// <summary>该 (x,z) 处的地表高度。</summary>
        private float SurfaceHeight(float x, float z)
        {
            Vector2 warped = WarpPosition(x, z);
            float ampScale = _biome.AmplitudeScale(warped.x, warped.y);
            float baseNoise = Hash.Fbm(warped.x, 0f, warped.y, _seed, _octaves, _baseFrequency, _lacunarity, _gain);
            float ridgeNoise = RidgedFbm(warped.x, warped.y);
            float n = Mathf.Lerp(baseNoise, ridgeNoise, _ridgedWeight);
            return _groundHeight + n * _amplitude * ampScale;
        }

        private Vector2 WarpPosition(float x, float z)
        {
            if (_domainWarpStrength <= 0f || _domainWarpFrequency <= 0f)
            {
                return new Vector2(x, z);
            }

            float warpX = Hash.Fbm(x, 17.31f, z, _warpSeedX, 3, _domainWarpFrequency, 2f, 0.5f);
            float warpZ = Hash.Fbm(x, 53.77f, z, _warpSeedZ, 3, _domainWarpFrequency, 2f, 0.5f);
            return new Vector2(
                x + warpX * _domainWarpStrength,
                z + warpZ * _domainWarpStrength);
        }

        private float RidgedFbm(float x, float z)
        {
            float sum = 0f;
            float amp = 1f;
            float norm = 0f;
            float f = _baseFrequency * _ridgeFrequencyMultiplier;

            for (int o = 0; o < _octaves; o++)
            {
                uint octaveSeed = _ridgeSeed + (uint)(o * 1013) + 1u;
                float n = Hash.Perlin3(x * f, 23.17f, z * f, octaveSeed);
                float ridge = 1f - Mathf.Abs(n);
                ridge = Mathf.Pow(Mathf.Clamp01(ridge), _ridgeSharpness);

                sum += ridge * amp;
                norm += amp;
                amp *= _gain;
                f *= _lacunarity;
            }

            return norm > 0f ? (sum / norm) * 2f - 1f : 0f;
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
            Vector2 warped = WarpPosition(worldPos.x, worldPos.z);
            return _biome.SampleColor(new Vector3(warped.x, worldPos.y, warped.y), _groundHeight, _amplitude);
        }
    }
}
