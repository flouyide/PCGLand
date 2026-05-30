using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 全局生成参数。作为 ScriptableObject 资产，可在 Inspector 编辑；
    /// 也可由 Bootstrap 在运行时构造默认值。所有生成均以 seed 为根。
    /// </summary>
    [CreateAssetMenu(menuName = "PCGLand/World Settings", fileName = "WorldSettings")]
    public sealed class WorldSettings : ScriptableObject
    {
        [Header("种子")]
        [Tooltip("世界全局种子，决定一切生成结果，可复现。")]
        public int seed = 1337;

        [Header("分块 / 体素")]
        [Tooltip("单个分块的世界边长（米）。")]
        public float chunkSize = 16f;

        [Tooltip("单个分块每轴的体素 cell 数。")]
        [Range(4, 64)]
        public int voxelResolution = 16;

        [Tooltip("等值面阈值，density 小于该值视为实心。")]
        public float isoLevel = 0f;

        [Header("流式加载")]
        [Tooltip("以观察者为中心、加载分块的半径（按分块计）。")]
        [Range(1, 12)]
        public int viewRadiusChunks = 5;

        [Tooltip("竖直方向加载的分块层数（上下各延伸多少）。需覆盖地形高度范围(±terrainAmplitude)。")]
        [Range(0, 8)]
        public int verticalRadiusChunks = 4;

        [Tooltip("每帧最多向 Mesh 上传多少个分块，用于平摊卡顿。")]
        [Range(1, 16)]
        public int uploadsPerFrame = 2;

        [Header("地形 fBm")]
        [Tooltip("地表基准高度（世界 Y）。")]
        public float groundHeight = 0f;

        [Tooltip("地形起伏的整体幅度（米）。")]
        public float terrainAmplitude = 24f;

        [Tooltip("fBm 基础频率。")]
        public float baseFrequency = 0.015f;

        [Range(1, 10)]
        public int octaves = 5;

        public float lacunarity = 2.0f;

        [Range(0f, 1f)]
        public float gain = 0.5f;

        [Header("Biome")]
        [Tooltip("Biome 场的频率（远小于地形频率，宏观分区）。")]
        public float biomeFrequency = 0.0025f;

        [Tooltip("Biome 边界域扰动强度，避免笔直边界。")]
        public float biomeWarp = 40f;

        [Header("碰撞（扩展点，默认关闭）")]
        public bool generateColliders = false;
    }
}
