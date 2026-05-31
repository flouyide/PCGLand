using UnityEngine;

namespace PCGLand
{
    /// <summary>
    /// 一键引导：把 WorldSettings → 采样器 → 网格器 → ChunkManager 接好，
    /// 创建飞行相机与平行光，并在运行时监听种子变化触发整体重建。
    /// 用法：空场景中新建一个空 GameObject，挂上本组件，点击 Play 即可。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PCGBootstrap : MonoBehaviour
    {
        [Tooltip("世界设置资产；留空则在运行时创建默认值。")]
        public WorldSettings settings;

        [Tooltip("流式加载中心；留空则自动创建一个飞行相机。")]
        public Transform viewer;

        [Tooltip("相机移动速度（单位/秒）；应用到飞行相机的 moveSpeed。")]
        public float cameraSpeed = 30f;

        [Tooltip("运行时显示调参面板（按 F1 开关）；可在游戏内编辑参数并手动重建地形。")]
        public bool showDebugUI = true;

        private ChunkManager _manager;
        private Material _material;
        private int _lastSeed;
        private bool _lastDebugVoxelBlocks;
        private bool _running;

        private void Start()
        {
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WorldSettings>();
                settings.name = "WorldSettings (Runtime)";
            }

            EnsureViewer();
            EnsureLight();
            _material = CreateMaterial();

            _manager = gameObject.GetComponent<ChunkManager>();
            if (_manager == null) _manager = gameObject.AddComponent<ChunkManager>();

            BuildWorld();
            EnsureDebugUI();
            _running = true;
        }

        private void EnsureDebugUI()
        {
            if (!showDebugUI) return;
            var ui = gameObject.GetComponent<SettingsDebugUI>();
            if (ui == null) ui = gameObject.AddComponent<SettingsDebugUI>();
            ui.Initialize(settings, this);
        }

        private void Update()
        {
            // 运行时修改种子或调试方块开关 → 整体重建（地形 fBm/Biome 参数同理可手动调用 Rebuild）。
            if (_running && settings != null &&
                (settings.seed != _lastSeed || settings.debugVoxelBlocks != _lastDebugVoxelBlocks))
            {
                BuildWorld();
            }
        }

        [ContextMenu("重建世界")]
        public void BuildWorld()
        {
            _lastSeed = settings.seed;
            _lastDebugVoxelBlocks = settings.debugVoxelBlocks;
            var biome = new BiomeSampler(settings);
            var field = new FbmDensityField(settings, biome);
            var mesher = new DualContouringMesher();
            _manager.Initialize(settings, field, mesher, viewer, _material);
        }

        private void EnsureViewer()
        {
            if (viewer != null) return;

            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            var fly = cam.GetComponent<FlyCamera>();
            if (fly == null) fly = cam.gameObject.AddComponent<FlyCamera>();
            fly.moveSpeed = cameraSpeed;

            cam.farClipPlane = Mathf.Max(cam.farClipPlane,
                settings.chunkSize * (settings.viewRadiusChunks + settings.verticalRadiusChunks + 3));
            // 放到地表峰顶略上方俯视。注意：竖直流式加载以相机所在分块为中心，
            // 相机 Y 必须落在地形高度范围附近（±terrainAmplitude），否则地表分块不会加载。
            float h = settings.groundHeight + settings.terrainAmplitude + 6f;
            cam.transform.position = new Vector3(0f, h, -settings.chunkSize * 2f);
            cam.transform.rotation = Quaternion.Euler(40f, 0f, 0f);

            viewer = cam.transform;
        }

        private void EnsureLight()
        {
            if (Object.FindFirstObjectByType<Light>() != null) return;
            var go = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 1.1f;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("PCGLand/TerrainTriplanar");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            return new Material(shader) { name = "TerrainMaterial (Runtime)" };
        }

        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
