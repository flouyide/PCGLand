using UnityEngine;
using UnityEngine.InputSystem;

namespace PCGLand
{
    /// <summary>
    /// 运行时调参面板（IMGUI）。在游戏内编辑 WorldSettings 的各项参数，
    /// 点击「重建地形」手动应用。无需场景 UI 资源，挂上即用。
    ///
    /// 注意：密度场/采样器在重建时按当前 WorldSettings 重新构造，因此
    /// 改完地形相关参数后必须点「重建地形」才会生效（顶部按钮）。
    /// 直接编辑的是 WorldSettings 资产实例：在编辑器中运行会写回资产文件。
    /// 按 F1 显示/隐藏面板。
    /// </summary>
    public sealed class SettingsDebugUI : MonoBehaviour
    {
        public WorldSettings settings;
        public PCGBootstrap bootstrap;
        public Key toggleKey = Key.F1;
        public bool visible = true;

        private Vector2 _scroll;
        private bool _autoRebuild;
        private bool _dirty;
        private string _status = "";
        private float _statusTime;

        // 面板基准设计尺寸，按屏幕高度等比缩放，避免高分屏字过小。
        private const float DesignWidth = 380f;
        private const float DesignHeight = 900f;

        public void Initialize(WorldSettings s, PCGBootstrap b)
        {
            settings = s;
            bootstrap = b;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame) visible = !visible;
        }

        private void OnGUI()
        {
            if (settings == null) return;

            // 按屏高等比缩放整个 GUI。
            float scale = Mathf.Max(1f, Screen.height / DesignHeight);
            Matrix4x4 prev = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float w = DesignWidth;
            float h = Screen.height / scale - 20f;
            var rect = new Rect(10f, 10f, w, h);

            if (!visible)
            {
                GUI.Label(new Rect(10f, 10f, 300f, 24f), $"[{toggleKey}] 打开调参面板");
                GUI.matrix = prev;
                return;
            }

            GUILayout.BeginArea(rect, GUI.skin.box);
            DrawToolbar();
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawFields();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            // 自动重建：拖动过程中累积「脏」标记，鼠标松开时统一重建一次。
            if (_autoRebuild && _dirty && Event.current.type == EventType.MouseUp)
            {
                _dirty = false;
                Rebuild();
            }

            GUI.matrix = prev;
        }

        private void DrawToolbar()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>世界调参</b>", RichLabel());
            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"隐藏[{toggleKey}]", GUILayout.Width(90f))) visible = false;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重建地形", GUILayout.Height(28f)))
            {
                Rebuild();
            }
            if (GUILayout.Button("随机种子", GUILayout.Width(90f), GUILayout.Height(28f)))
            {
                settings.seed = Random.Range(0, 1000000);
                Rebuild();
            }
            GUILayout.EndHorizontal();

            _autoRebuild = GUILayout.Toggle(_autoRebuild, " 自动重建（拖动结束后实时应用，开销大）");

            if (_statusTime > Time.unscaledTime && !string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status);
            }
            GUILayout.Space(4f);
        }

        private void DrawFields()
        {
            Header("种子 / 分块");
            SeedRow();
            settings.chunkSize = FloatField("chunkSize", settings.chunkSize, 4f, 64f);
            settings.voxelResolution = IntField("voxelResolution", settings.voxelResolution, 4, 64);
            settings.isoLevel = FloatField("isoLevel", settings.isoLevel, -1f, 1f);

            Header("流式加载");
            settings.viewRadiusChunks = IntField("viewRadiusChunks", settings.viewRadiusChunks, 1, 12);
            settings.verticalRadiusChunks = IntField("verticalRadiusChunks", settings.verticalRadiusChunks, 0, 8);
            settings.uploadsPerFrame = IntField("uploadsPerFrame", settings.uploadsPerFrame, 1, 16);

            Header("地形 fBm");
            settings.groundHeight = FloatField("groundHeight", settings.groundHeight, -50f, 50f);
            settings.terrainAmplitude = FloatField("terrainAmplitude", settings.terrainAmplitude, 0f, 150f);
            settings.baseFrequency = FloatField("baseFrequency", settings.baseFrequency, 0.001f, 0.05f, "F4");
            settings.octaves = IntField("octaves", settings.octaves, 1, 10);
            settings.lacunarity = FloatField("lacunarity", settings.lacunarity, 1f, 4f);
            settings.gain = FloatField("gain", settings.gain, 0f, 1f);

            Header("山脊 / 坐标扰动");
            settings.ridgedWeight = FloatField("ridgedWeight", settings.ridgedWeight, 0f, 1f);
            settings.ridgeSharpness = FloatField("ridgeSharpness", settings.ridgeSharpness, 0.5f, 6f);
            settings.ridgeFrequencyMultiplier = FloatField("ridgeFrequencyMultiplier", settings.ridgeFrequencyMultiplier, 0.1f, 4f);
            settings.domainWarpStrength = FloatField("domainWarpStrength", settings.domainWarpStrength, 0f, 128f);
            settings.domainWarpFrequency = FloatField("domainWarpFrequency", settings.domainWarpFrequency, 0.0001f, 0.05f, "F4");

            Header("Biome");
            settings.biomeFrequency = FloatField("biomeFrequency", settings.biomeFrequency, 0.0005f, 0.02f, "F4");
            settings.biomeWarp = FloatField("biomeWarp", settings.biomeWarp, 0f, 128f);
            settings.snowRockHeight = FloatField("snowRockHeight", settings.snowRockHeight, 0f, 1f);
            settings.sandHeight = FloatField("sandHeight", settings.sandHeight, -1f, 0f);
            settings.coldRockBias = FloatField("coldRockBias", settings.coldRockBias, 0f, 1f);
            settings.reliefNormalize = FloatField("reliefNormalize", settings.reliefNormalize, 0.1f, 1f);

            Header("Biome 配色");
            settings.snowColor = ColorField("snow", settings.snowColor);
            settings.rockColor = ColorField("rock", settings.rockColor);
            settings.grassColor = ColorField("grass", settings.grassColor);
            settings.sandColor = ColorField("sand", settings.sandColor);
            settings.forestColor = ColorField("forest", settings.forestColor);

            Header("调试");
            settings.generateColliders = BoolField("generateColliders", settings.generateColliders);
            settings.debugVoxelBlocks = BoolField("debugVoxelBlocks（体素方块）", settings.debugVoxelBlocks);

            GUILayout.Space(8f);
        }

        private void Rebuild()
        {
            _dirty = false;
            if (bootstrap == null)
            {
                bootstrap = FindFirstObjectByType<PCGBootstrap>();
            }
            if (bootstrap != null)
            {
                bootstrap.BuildWorld();
                Flash("已重建");
            }
            else
            {
                Flash("找不到 PCGBootstrap");
            }
        }

        private void Flash(string msg)
        {
            _status = msg;
            _statusTime = Time.unscaledTime + 2f;
        }

        // ---- 控件 ----

        // seed 用文本框 + 步进，避免连续滑块拖动触发逐帧重建。
        private string _seedText;
        private void SeedRow()
        {
            // 外部（随机按钮/资产）改了 seed 时同步显示。
            if (_seedText == null || (int.TryParse(_seedText, out int shown) && shown != settings.seed))
            {
                _seedText = settings.seed.ToString();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("seed", GUILayout.Width(180f));
            _seedText = GUILayout.TextField(_seedText, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("◀", GUILayout.Width(28f))) { settings.seed--; _seedText = settings.seed.ToString(); }
            if (GUILayout.Button("▶", GUILayout.Width(28f))) { settings.seed++; _seedText = settings.seed.ToString(); }
            GUILayout.EndHorizontal();

            if (int.TryParse(_seedText, out int parsed)) settings.seed = parsed;
        }

        private void Header(string title)
        {
            GUILayout.Space(6f);
            GUILayout.Label($"<b><color=#ffd479>{title}</color></b>", RichLabel());
        }

        private float FloatField(string label, float value, float min, float max, string fmt = "F3")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180f));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
            GUILayout.Label(v.ToString(fmt), GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(v, value)) _dirty = true;
            return v;
        }

        private int IntField(string label, int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(180f));
            int v = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true)));
            GUILayout.Label(v.ToString(), GUILayout.Width(56f));
            GUILayout.EndHorizontal();
            if (v != value) _dirty = true;
            return v;
        }

        private bool BoolField(string label, bool value)
        {
            bool v = GUILayout.Toggle(value, " " + label);
            if (v != value) _dirty = true;
            return v;
        }

        private Color ColorField(string label, Color c)
        {
            Color prevColor = c;
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(60f));
            c.r = GUILayout.HorizontalSlider(c.r, 0f, 1f);
            c.g = GUILayout.HorizontalSlider(c.g, 0f, 1f);
            c.b = GUILayout.HorizontalSlider(c.b, 0f, 1f);
            var swatch = GUILayoutUtility.GetRect(24f, 18f, GUILayout.Width(24f));
            Color prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, 1f);
            GUI.DrawTexture(swatch, Texture2D.whiteTexture);
            GUI.color = prev;
            GUILayout.EndHorizontal();
            if (c != prevColor) _dirty = true;
            return c;
        }

        private GUIStyle _richLabel;
        private GUIStyle RichLabel()
        {
            if (_richLabel == null)
            {
                _richLabel = new GUIStyle(GUI.skin.label) { richText = true };
            }
            return _richLabel;
        }
    }
}
