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
        public int uploadsPerFrame = 6;

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

        [Header("山脊 / 坐标扰动")]
        [Tooltip("山脊噪声与普通 fBm 的混合比例；越高山脊越尖锐、谷地越明显。")]
        [Range(0f, 1f)]
        public float ridgedWeight = 0.45f;

        [Tooltip("山脊锐度；越高山脊线越窄越硬。")]
        [Range(0.5f, 6f)]
        public float ridgeSharpness = 2.4f;

        [Tooltip("山脊噪声相对基础频率的倍率；越高细节越密。")]
        public float ridgeFrequencyMultiplier = 1.8f;

        [Tooltip("Domain Warp 坐标扰动强度（米）；让山脉走势弯曲、避免规则噪声感。")]
        [Range(0f, 128f)]
        public float domainWarpStrength = 28f;

        [Tooltip("Domain Warp 坐标扰动频率；通常低于或接近基础地形频率。")]
        [Range(0.0001f, 0.05f)]
        public float domainWarpFrequency = 0.006f;

        [Header("Biome")]
        [Tooltip("Biome 场的频率（远小于地形频率，宏观分区）。")]
        public float biomeFrequency = 0.0025f;

        [Tooltip("Biome 边界域扰动强度，避免笔直边界。")]
        public float biomeWarp = 40f;

        [Header("Biome 配色")]
        [Tooltip("雪山顶颜色（高海拔）。")]
        public Color snowColor = new Color(0.92f, 0.93f, 0.96f);

        [Tooltip("岩石颜色（高海拔/陡坡/寒冷处）。")]
        public Color rockColor = new Color(0.45f, 0.42f, 0.38f);

        [Tooltip("草原颜色（中海拔、干燥）。")]
        public Color grassColor = new Color(0.32f, 0.55f, 0.22f);

        [Tooltip("沙漠颜色（低洼）。")]
        public Color sandColor = new Color(0.76f, 0.70f, 0.45f);

        [Tooltip("森林颜色（中海拔、潮湿）。")]
        public Color forestColor = new Color(0.18f, 0.40f, 0.20f);

        [Tooltip("相对高度高于此值进入岩石/雪带（-1低洼..+1高海拔）。")]
        [Range(0f, 1f)]
        public float snowRockHeight = 0.55f;

        [Tooltip("相对高度低于此值视为沙漠（-1低洼..+1高海拔）。")]
        [Range(-1f, 0f)]
        public float sandHeight = -0.45f;

        [Tooltip("寒冷处草地偏向岩石的强度。")]
        [Range(0f, 1f)]
        public float coldRockBias = 0.35f;

        [Tooltip("地形起伏归一化系数（fBm 噪声的典型峰值幅度，约 0.3~0.6）。\n用它把相对高度归一到 [-1,1] 再分带；越小则雪/沙等极端带越容易出现。")]
        [Range(0.1f, 1f)]
        public float reliefNormalize = 0.85f;

        [Header("碰撞（扩展点，默认关闭）")]
        public bool generateColliders = false;

        [Header("调试")]
        [Tooltip("以体素方块（Minecraft 风格立方体）替代平滑等值面渲染，用于直观查看底层体素网格。\n按 cell 中心是否实心生成立方体，仅输出朝向空气的外表面。运行时切换会触发整体重建。")]
        public bool debugVoxelBlocks = false;
    }
}
