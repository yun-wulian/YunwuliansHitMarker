using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using static EFT.Player;

namespace HitMarkerPlugin
{
    [BepInPlugin("com.yunwulian.hitmarker", "Hit Marker", "1.0.0")]
    public class HitMarkerPlugin : BaseUnityPlugin
    {
        public static HitMarkerPlugin Instance;

        // 配置项
        public static ConfigEntry<bool> EnableHitMarker;
        public static ConfigEntry<bool> EnableKillMarker;
        public static ConfigEntry<float> HitMarkerVolume;
        public static ConfigEntry<float> KillMarkerVolume;
        public static ConfigEntry<bool> ShowTestWindow;
        public static ConfigEntry<KeyboardShortcut> TestWindowToggleKey;
        public static ConfigEntry<float> KillMarkerPositionX;
        public static ConfigEntry<float> KillMarkerPositionY;
        public static ConfigEntry<bool> UseOriginalIconSize;
        public static ConfigEntry<float> KillMarkerSizeScale;
        public static ConfigEntry<float> HitMarkerSizeScale;

        // 资源
        public static Texture2D HitMarkerTexture;
        public static Texture2D KillMarkerTexture;
        public static Texture2D HeadshotMarkerTexture;
        public static Texture2D HeadshotKillMarkerTexture;
        public static Texture2D ArmorHitMarkerTexture;
        public static Texture2D ArmorBreakMarkerTexture;

        // 连杀图标
        public static Texture2D[] KillStreakTextures = new Texture2D[7]; // 0-6，0不使用

        // UI管理器
        private HitMarkerUI uiManager;
        private TestWindow testWindow;

        // 公开的Logger属性
        public new static BepInEx.Logging.ManualLogSource Logger { get; private set; }

        // 连杀机制
        private int currentKillStreak = 0;
        private float lastKillTime = 0f;
        private const float KILL_STREAK_TIMEOUT = 10f; // 5秒内连杀有效

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            // 初始化调试信息
            DebugInfo.Reset();

            // 预初始化音频管理器（确保在UI之前）
            var audioManager = AudioManager.Instance;

            // 创建配置
            CreateConfig();

            // 初始化UI管理器
            uiManager = gameObject.AddComponent<HitMarkerUI>();
            testWindow = gameObject.AddComponent<TestWindow>();

            // 应用Harmony补丁
            var harmony = new Harmony("com.yunwulian.hitmarker");
            harmony.PatchAll();

            // 延迟加载资源
            Invoke("DelayedLoadResources", 1f);

            Logger.LogInfo("Hit Marker插件加载完成！");
        }

        private void DelayedLoadResources()
        {
            LoadResources();
        }

        // 战局中重新加载音频
        public void ReloadAudioInRaid()
        {
            Logger.LogInfo("战局中重新加载音频资源");

            var audioManager = AudioManager.Instance;
            if (audioManager != null)
            {
                // 使用反射调用重新加载方法
                var reloadMethod = audioManager.GetType().GetMethod("ReloadAllAudio");
                reloadMethod?.Invoke(audioManager, null);
            }
        }

        private void Update()
        {
            // 检测测试窗口切换按键
            if (TestWindowToggleKey.Value.IsDown())
            {
                ShowTestWindow.Value = !ShowTestWindow.Value;
                Logger.LogInfo($"测试窗口: {(ShowTestWindow.Value ? "显示" : "隐藏")}");
            }

            // 连杀超时检测
            if (currentKillStreak > 0 && Time.time - lastKillTime > KILL_STREAK_TIMEOUT)
            {
                Logger.LogInfo($"连杀中断，最终连杀数: {currentKillStreak}");
                currentKillStreak = 0;
            }
        }

        private void CreateConfig()
        {
            EnableHitMarker = Config.Bind("通用", "启用命中提示", true, "是否显示命中提示");
            EnableKillMarker = Config.Bind("通用", "启用击杀提示", true, "是否显示击杀提示");
            HitMarkerVolume = Config.Bind("音效", "命中音效音量", 0.8f, new ConfigDescription("命中音效音量", new AcceptableValueRange<float>(0f, 1f)));
            KillMarkerVolume = Config.Bind("音效", "击杀音效音量", 1.0f, new ConfigDescription("击杀音效音量", new AcceptableValueRange<float>(0f, 1f)));
            KillMarkerPositionX = Config.Bind("位置", "击杀图标X位置", 0.5f, new ConfigDescription("击杀图标的水平位置 (0-1)", new AcceptableValueRange<float>(0f, 1f)));
            KillMarkerPositionY = Config.Bind("位置", "击杀图标Y位置", 0.87f, new ConfigDescription("击杀图标的垂直位置 (0-1)", new AcceptableValueRange<float>(0f, 1f)));
            UseOriginalIconSize = Config.Bind("图标", "使用原始图标大小", true, "是否使用图标的原始大小（否则使用统一缩放）");
            KillMarkerSizeScale = Config.Bind("图标", "击杀图标缩放", 1.0f, new ConfigDescription("击杀图标的缩放系数", new AcceptableValueRange<float>(0.1f, 10f)));
            HitMarkerSizeScale = Config.Bind("图标", "命中图标缩放", 1.0f, new ConfigDescription("命中图标的缩放系数", new AcceptableValueRange<float>(0.1f, 10f)));

            ShowTestWindow = Config.Bind("测试", "显示测试窗口", false, "是否显示测试窗口");
            TestWindowToggleKey = Config.Bind("测试", "测试窗口切换按键", new KeyboardShortcut(KeyCode.F10), "切换测试窗口显示/隐藏的按键");
        }

        private void LoadResources()
        {
            try
            {
                string pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string resourcePath = Path.Combine(pluginPath, "resource");
                string texturePath = Path.Combine(resourcePath, "texture");

                Directory.CreateDirectory(texturePath);

                // 加载基础图标
                HitMarkerTexture = LoadTexture(Path.Combine(texturePath, "hit_marker.png"));
                KillMarkerTexture = LoadTexture(Path.Combine(texturePath, "kill_marker.png"));
                HeadshotMarkerTexture = LoadTexture(Path.Combine(texturePath, "headshot_marker.png")) ?? KillMarkerTexture;
                HeadshotKillMarkerTexture = LoadTexture(Path.Combine(texturePath, "headshot_kill_marker.png")) ?? KillMarkerTexture;
                ArmorHitMarkerTexture = LoadTexture(Path.Combine(texturePath, "armor_hit_marker.png")) ?? HitMarkerTexture;
                ArmorBreakMarkerTexture = LoadTexture(Path.Combine(texturePath, "armor_break_marker.png")) ?? HitMarkerTexture;

                // 加载连杀图标 (2-6连杀)，支持非矩形图标
                for (int i = 2; i <= 6; i++)
                {
                    KillStreakTextures[i] = LoadTexture(Path.Combine(texturePath, $"kill_{i}_marker.png")) ?? KillMarkerTexture;
                }

                // 创建默认图标
                if (HitMarkerTexture == null) HitMarkerTexture = CreateDefaultHitMarker();
                if (KillMarkerTexture == null) KillMarkerTexture = CreateDefaultKillMarker();

                Logger.LogInfo("资源加载完成");

                // 记录加载的纹理信息
                LogTextureInfo("命中标记", HitMarkerTexture);
                LogTextureInfo("击杀标记", KillMarkerTexture);
                for (int i = 2; i <= 6; i++)
                {
                    LogTextureInfo($"{i}连杀标记", KillStreakTextures[i]);
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"资源加载失败: {e.Message}");
            }
        }

        // 添加纹理信息日志
        private void LogTextureInfo(string name, Texture2D texture)
        {
            if (texture != null)
            {
                Logger.LogInfo($"{name}: {texture.width}x{texture.height}");
            }
            else
            {
                Logger.LogWarning($"{name}: 加载失败或为空");
            }
        }

        private Texture2D LoadTexture(string path)
        {
            if (!File.Exists(path))
            {
                Logger.LogWarning($"图片文件不存在: {path}");
                return null;
            }

            try
            {
                byte[] fileData = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(fileData))
                {
                    return texture;
                }
                return null;
            }
            catch (Exception e)
            {
                Logger.LogError($"加载图片失败 {path}: {e}");
                return null;
            }
        }

        // 创建默认命中标记（白色十字）
        private Texture2D CreateDefaultHitMarker()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color[] pixels = new Color[size * size];
            int center = size / 2;
            int thickness = 4;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isCross =
                        (x >= center - thickness && x <= center + thickness) ||
                        (y >= center - thickness && y <= center + thickness);

                    pixels[y * size + x] = isCross ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // 创建默认击杀标记（红色骷髅头）
        private Texture2D CreateDefaultKillMarker()
        {
            int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

            Color[] pixels = new Color[size * size];
            Vector2 center = new Vector2(size / 2, size / 2);
            float radius = size / 3f;

            // 简化的骷髅头形状
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y);
                    float dist = Vector2.Distance(pos, center);

                    // 头部圆形
                    bool inHead = dist <= radius;

                    // 眼睛孔
                    bool inLeftEye = Vector2.Distance(pos, center + new Vector2(-radius / 2, radius / 4)) <= radius / 4;
                    bool inRightEye = Vector2.Distance(pos, center + new Vector2(radius / 2, radius / 4)) <= radius / 4;

                    // 嘴巴
                    bool inMouth =
                        y > center.y + radius / 4 && y < center.y + radius / 2 &&
                        x > center.x - radius / 2 && x < center.x + radius / 2 &&
                        Mathf.Abs(x - center.x) > radius / 4;

                    pixels[y * size + x] = (inHead && !inLeftEye && !inRightEye && !inMouth) ? Color.red : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // 处理击杀事件
        public void OnKill(bool isHeadshot)
        {
            float currentTime = Time.time;

            // 检查连杀
            if (currentTime - lastKillTime <= KILL_STREAK_TIMEOUT)
            {
                currentKillStreak++;
                if (currentKillStreak > 6) currentKillStreak = 6;

                Logger.LogInfo($"连杀! 当前连杀数: {currentKillStreak}");
                PlayKillStreakEffect(currentKillStreak);
            }
            else
            {
                // 新的连杀开始
                currentKillStreak = 1;
                if (isHeadshot)
                {
                    PlayHeadshotKillEffect();
                }
                else
                {
                    PlayKillEffect();
                }
            }

            lastKillTime = currentTime;
        }

        public void PlayHitEffect(bool usefireport = true)
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                uiManager.ShowHitMarker(usefireport);
            }
        }

        public void PlayKillEffect()
        {
            if (EnableKillMarker.Value && uiManager != null)
            {
                uiManager.ShowKillMarker();
            }
        }

        public void PlayHeadshotEffect(bool usefireport = true)
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发爆头命中效果");
                uiManager.ShowHeadshotMarker(usefireport);
            }
        }

        public void PlayHeadshotKillEffect()
        {
            if (EnableKillMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发爆头击杀效果");
                uiManager.ShowHeadshotKillMarker();
            }
        }

        public void PlayArmorHitEffect(bool usefireport = true)
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发护甲命中效果");
                uiManager.ShowArmorHitMarker(usefireport);
            }
        }

        public void PlayArmorBreakEffect(bool usefireport = true)
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发护甲击穿效果");
                uiManager.ShowArmorBreakMarker(usefireport);
            }
        }

        public void PlayKillStreakEffect(int streak)
        {
            if (EnableKillMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发{streak}连杀效果");
                uiManager.ShowKillStreakMarker(streak);
            }
        }

        // 测试方法
        public void TestHitEffect()
        {
            Logger.LogInfo("测试命中效果");
            PlayHitEffect(false);
        }

        public void TestArmorHitEffect()
        {
            Logger.LogInfo("测试命中效果");
            PlayArmorHitEffect(false);
        }

        public void TestArmorBreakEffect()
        {
            Logger.LogInfo("测试命中效果");
            PlayArmorBreakEffect(false);
        }
        public void TestHeadshotEffect()
        {
            Logger.LogInfo("测试爆头效果");
            PlayHeadshotEffect(false);
        }

        public void TestKillEffect()
        {
            Logger.LogInfo("测试击杀效果");
            PlayKillEffect();
        }

        public void TestHeadshotKillEffect()
        {
            Logger.LogInfo("测试爆头效果");
            PlayHeadshotKillEffect();
        }

        public void TestKillStreakEffect()
        {
            Logger.LogInfo("测试连杀效果");
            OnKill(false);
        }

        // 获取当前连杀数（用于测试窗口显示）
        public int GetCurrentKillStreak()
        {
            return currentKillStreak;
        }

        // 重置连杀（用于测试）
        public void ResetKillStreak()
        {
            currentKillStreak = 0;
            lastKillTime = 0f;
        }

        // 在HitMarkerPlugin类中替换ExternalAudioPlayer
        public class InternalAudioPlayer
        {
            private static InternalAudioPlayer _instance;
            public static InternalAudioPlayer Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        _instance = new InternalAudioPlayer();
                    }
                    return _instance;
                }
            }

            public void PlayHitSound()
            {
                AudioManager.Instance.PlaySound("hit", HitMarkerVolume.Value, false);
            }

            public void PlayKillSound()
            {
                AudioManager.Instance.PlaySound("kill", KillMarkerVolume.Value, true);
            }

            public void PlayHeadshotSound()
            {
                AudioManager.Instance.PlaySound("headshot", HitMarkerVolume.Value, false);
            }

            public void PlayHeadshotKillSound()
            {
                AudioManager.Instance.PlaySound("headshot_kill", KillMarkerVolume.Value, true);
            }

            public void PlayArmorHitSound()
            {
                AudioManager.Instance.PlaySound("armor_hit", HitMarkerVolume.Value, false);
            }

            public void PlayArmorBreakSound()
            {
                AudioManager.Instance.PlaySound("armor_break", HitMarkerVolume.Value, false);
            }

            public void PlayKillStreakSound(int streak)
            {
                if (streak >= 2 && streak <= 6)
                {
                    AudioManager.Instance.PlaySound($"kill_{streak}", KillMarkerVolume.Value, true);
                }
            }
        }

        public class ExternalAudioPlayer
        {
            private static ExternalAudioPlayer _instance;
            public static ExternalAudioPlayer Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        _instance = new ExternalAudioPlayer();
                    }
                    return _instance;
                }
            }

            [DllImport("winmm.dll")]
            private static extern bool PlaySound(string pszSound, IntPtr hmod, uint fdwSound);

            private const uint SND_FILENAME = 0x00020000;
            private const uint SND_ASYNC = 0x0001;

            private string pluginPath;
            private string soundPath;

            public ExternalAudioPlayer()
            {
                pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                soundPath = Path.Combine(pluginPath, "resource", "sound");
                Directory.CreateDirectory(soundPath);
            }

            public void PlayHitSound()
            {
                PlaySoundFile("hit_sound.wav", "命中");
            }

            public void PlayKillSound()
            {
                PlaySoundFile("kill_sound.wav", "击杀");
            }

            public void PlayHeadshotSound()
            {
                PlaySoundFile("headshot_sound.wav", "爆头");
            }

            public void PlayHeadshotKillSound()
            {
                PlaySoundFile("headshot_kill_sound.wav", "爆头击杀");
            }

            public void PlayArmorHitSound()
            {
                PlaySoundFile("armor_hit_sound.wav", "护甲命中");
            }

            public void PlayArmorBreakSound()
            {
                PlaySoundFile("armor_break_sound.wav", "护甲击碎");
            }

            public void PlayKillStreakSound(int streak)
            {
                if (streak >= 2 && streak <= 6)
                {
                    PlaySoundFile($"kill_{streak}_sound.wav", $"{streak}连杀");
                }
            }

            private bool PlaySoundFile(string fileName, string soundType)
            {
                string filePath = Path.Combine(soundPath, fileName);

                if (!File.Exists(filePath))
                {
                    HitMarkerPlugin.Logger.LogWarning($"音效文件不存在: {filePath}");
                    return false;
                }

                try
                {
                    // 方法1: Windows API (最快)
                    bool success = PlaySound(filePath, IntPtr.Zero, SND_FILENAME | SND_ASYNC);

                    if (success)
                    {
                        HitMarkerPlugin.Logger.LogInfo($"API播放{soundType}音效: {fileName}");
                        return true;
                    }
                    else
                    {
                        // 方法2: 进程回退
                        return PlaySoundWithProcess(filePath, soundType);
                    }
                }
                catch (Exception e)
                {
                    HitMarkerPlugin.Logger.LogError($"播放{soundType}音效异常: {e.Message}");
                    // 最终回退：进程方式
                    return PlaySoundWithProcess(filePath, soundType);
                }
            }

            private bool PlaySoundWithProcess(string filePath, string soundType)
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/C start /min \"\" \"{filePath}\"",
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };

                    Process.Start(startInfo);
                    HitMarkerPlugin.Logger.LogInfo($"进程播放{soundType}音效: {Path.GetFileName(filePath)}");
                    return true;
                }
                catch (Exception e)
                {
                    HitMarkerPlugin.Logger.LogError($"进程播放音效失败: {e.Message}");
                    return false;
                }
            }
        }

        public static class DebugInfo
        {
            public static int ReceiveDamageCount { get; set; }
            public static int ArmorDamageCount { get; set; }
            public static int KillEventCount { get; set; }
            public static int ValidHitCount { get; set; }
            public static int InvalidHitCount { get; set; }
            public static int LocalPlayerNullCount { get; set; }
            public static int TotalKills { get; set; }

            public static string LastError { get; set; }
            public static string LastNonPlayerHit { get; set; }
            public static string LastNonPlayerKill { get; set; }

            public static HitData LastHitInfo { get; set; }
            public static ArmorData LastArmorHit { get; set; }
            public static KillData LastKillInfo { get; set; }

            public static void Reset()
            {
                ReceiveDamageCount = 0;
                ArmorDamageCount = 0;
                KillEventCount = 0;
                ValidHitCount = 0;
                InvalidHitCount = 0;
                LocalPlayerNullCount = 0;
                TotalKills = 0;
                LastError = "无";
                LastNonPlayerHit = "无";
                LastNonPlayerKill = "无";
                LastHitInfo = null;
                LastArmorHit = null;
                LastKillInfo = null;
            }

            public class HitData
            {
                public DateTime Timestamp { get; set; }
                public string TargetName { get; set; }
                public string BodyPart { get; set; }
                public float Damage { get; set; }
                public float ArmorDamage { get; set; }
                public float Distance { get; set; }

                public override string ToString()
                {
                    return $"{Timestamp:HH:mm:ss} | 目标: {TargetName} | 部位: {BodyPart}\n伤害: {Damage} | 护甲伤害: {ArmorDamage} | 距离: {Distance:F1}m";
                }
            }

            public class ArmorData
            {
                public DateTime Timestamp { get; set; }
                public float ArmorDamage { get; set; }
                public float RemainingDurability { get; set; }
                public bool IsBroken { get; set; }

                public override string ToString()
                {
                    return $"{Timestamp:HH:mm:ss} | 护甲伤害: {ArmorDamage} | 剩余耐久: {RemainingDurability} | 击穿: {IsBroken}";
                }
            }

            public class KillData
            {
                public DateTime Timestamp { get; set; }
                public string VictimName { get; set; }
                public string VictimSide { get; set; }
                public float Distance { get; set; }

                public override string ToString()
                {
                    return $"{Timestamp:HH:mm:ss} | 目标: {VictimName} | 阵营: {VictimSide} | 距离: {Distance:F1}m";
                }
            }
        }
        // 添加命中点存储
        public static Vector3 FireportWorldPoint = Vector3.zero;
        private static Vector2 FireportScreenPoint = Vector2.zero;
        public static SSAA FPSCameraSSAA;
        // 添加摄像机引用
        private static Camera mainCamera;

        // 更新屏幕坐标
        private static void UpdateScreenPoint()
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    // 尝试找到其他可用的摄像机
                    var cameras = FindObjectsOfType<Camera>();
                    foreach (var cam in cameras)
                    {
                        if (cam.isActiveAndEnabled && cam.gameObject.activeInHierarchy)
                        {
                            mainCamera = cam;
                            break;
                        }
                    }
                }
            }
            FirearmController firearmController = LocalPlayerTracker.LocalPlayer.HandsController as FirearmController;
            if (firearmController != null && firearmController.CurrentFireport != null && Camera.main != null)
            {
                Vector3 position = firearmController.CurrentFireport.position;
                Vector3 weaponDirection = firearmController.WeaponDirection;
                HitMarkerPlugin.FireportWorldPoint = position + (weaponDirection * 100f);
            }

            if (mainCamera != null && FireportWorldPoint != Vector3.zero)
            {
                float FPSCameraSSAARatio = (float)FPSCameraSSAA.GetOutputHeight() / (float)FPSCameraSSAA.GetInputHeight();
                Vector3 screenPoint = mainCamera.WorldToScreenPoint(FireportWorldPoint) * FPSCameraSSAARatio;

                // 检查点是否在屏幕内
                if (screenPoint.z > 0)
                {
                    // 转换为以屏幕中心为原点的坐标系（这是关键修正）
                    Vector2 centeredPosition = new Vector2(
                        screenPoint.x - (Screen.width / 2f),
                        screenPoint.y - (Screen.height / 2f)
                    );

                    // 转换为GUI坐标（Y轴翻转）
                    FireportScreenPoint = new Vector2(
                        centeredPosition.x + (Screen.width / 2f),
                        Screen.height - (centeredPosition.y + (Screen.height / 2f))
                    );
                }
                else
                {
                    FireportScreenPoint = new Vector2(-1000, -1000);
                }
            }
            else
            {
                // 后备方案：使用屏幕中心
                FireportScreenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }
        }

        // 获取最后命中的屏幕坐标
        public static Vector2 GetFireportScreenPoint()
        {
            UpdateScreenPoint(); // 确保坐标是最新的
            return FireportScreenPoint;
        }

        // 重置命中点（在游戏场景切换时调用）
        public static void ResetFireport()
        {
            FireportWorldPoint = Vector3.zero;
            FireportScreenPoint = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }
        // 在HitMarkerPlugin类中添加音频管理器
        public class AudioManager : MonoBehaviour
        {
            private static AudioManager _instance;
            public static AudioManager Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        // 确保在游戏运行期间创建
                        if (!Application.isPlaying) return null;

                        GameObject go = new GameObject("HitMarkerAudioManager");
                        _instance = go.AddComponent<AudioManager>();
                        DontDestroyOnLoad(go);
                        _instance.Initialize();
                    }
                    return _instance;
                }
            }

            // 使用更可靠的音频加载方式
            private Dictionary<string, AudioClip> audioClips = new Dictionary<string, AudioClip>();
            private List<AudioSource> audioChannels = new List<AudioSource>();
            private const int CHANNEL_COUNT = 12; // 增加通道数

            // 音频文件映射
            private readonly Dictionary<string, string> soundFileMap = new Dictionary<string, string>
    {
        { "hit", "hit_sound.wav" },
        { "kill", "kill_sound.wav" },
        { "headshot", "headshot_sound.wav" },
        { "headshot_kill", "headshot_kill_sound.wav" },
        { "armor_hit", "armor_hit_sound.wav" },
        { "armor_break", "armor_break_sound.wav" },
        { "kill_2", "kill_2_sound.wav" },
        { "kill_3", "kill_3_sound.wav" },
        { "kill_4", "kill_4_sound.wav" },
        { "kill_5", "kill_5_sound.wav" },
        { "kill_6", "kill_6_sound.wav" }
    };

            private bool isInitialized = false;

            private void Initialize()
            {
                if (isInitialized) return;

                try
                {
                    // 创建音频通道
                    for (int i = 0; i < CHANNEL_COUNT; i++)
                    {
                        AudioSource source = gameObject.AddComponent<AudioSource>();
                        source.spatialBlend = 0f; // 2D音效
                        source.playOnAwake = false;
                        source.volume = 1.0f;
                        audioChannels.Add(source);
                    }

                    // 预加载所有音频文件
                    PreloadAllAudioClips();

                    isInitialized = true;
                    Logger.LogInfo("音频管理器初始化完成");
                }
                catch (Exception e)
                {
                    Logger.LogError($"音频管理器初始化失败: {e}");
                }
            }

            private void PreloadAllAudioClips()
            {
                string pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string soundPath = Path.Combine(pluginPath, "resource", "sound");

                if (!Directory.Exists(soundPath))
                {
                    Logger.LogWarning($"音效目录不存在: {soundPath}");
                    CreateDefaultSoundFiles(soundPath);
                }

                foreach (var soundPair in soundFileMap)
                {
                    string filePath = Path.Combine(soundPath, soundPair.Value);
                    AudioClip clip = LoadAudioClipDirect(filePath);

                    if (clip != null)
                    {
                        audioClips[soundPair.Key] = clip;
                        Logger.LogInfo($"预加载音效: {soundPair.Key} -> {clip.length:F2}秒");
                    }
                    else
                    {
                        Logger.LogWarning($"音效加载失败: {soundPair.Key} -> {filePath}");
                    }
                }
            }

            // 直接加载音频文件，不使用UnityWebRequest
            private AudioClip LoadAudioClipDirect(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    Logger.LogWarning($"音频文件不存在: {filePath}");
                    return null;
                }

                try
                {
                    // 方法1: 使用File.ReadAllBytes和WWW（兼容性更好）
                    byte[] fileData = File.ReadAllBytes(filePath);

                    // 创建临时文件路径
                    string tempPath = Path.Combine(Application.temporaryCachePath, Path.GetFileName(filePath));
                    File.WriteAllBytes(tempPath, fileData);

                    // 使用WWW加载（比UnityWebRequest更稳定）
                    var www = new WWW("file://" + tempPath);
                    while (!www.isDone) { } // 简单同步等待

                    AudioClip clip = www.GetAudioClip(false, false);
                    clip.name = Path.GetFileName(filePath);

                    www.Dispose();

                    // 清理临时文件
                    try { File.Delete(tempPath); } catch { }

                    if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                    {
                        Logger.LogInfo($"音频加载成功: {clip.name} ({clip.length:F2}秒)");
                        return clip;
                    }
                    else
                    {
                        Logger.LogError($"音频加载状态异常: {clip?.loadState}");
                        return null;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"加载音频文件失败 {filePath}: {e}");
                    return null;
                }
            }

            // 创建默认音效文件（如果缺失）
            private void CreateDefaultSoundFiles(string soundPath)
            {
                Directory.CreateDirectory(soundPath);
                Logger.LogInfo($"创建默认音效目录: {soundPath}");

                // 这里可以添加创建默认音效文件的逻辑
                // 由于音效文件需要特定格式，建议用户自行提供
            }

            public void PlaySound(string soundType, float volume = 1.0f, bool isHighPriority = false)
            {
                if (!isInitialized)
                {
                    Logger.LogWarning("音频管理器未初始化");
                    return;
                }

                if (!audioClips.ContainsKey(soundType))
                {
                    Logger.LogWarning($"音效未加载: {soundType}");
                    return;
                }

                AudioClip clip = audioClips[soundType];
                if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
                {
                    Logger.LogError($"音效剪辑无效: {soundType} (状态: {clip?.loadState})");
                    return;
                }

                AudioSource channel = GetAvailableChannel(soundType, isHighPriority);
                if (channel != null)
                {
                    channel.clip = clip;
                    channel.volume = Mathf.Clamp01(volume);
                    channel.Play();

                    Logger.LogInfo($"播放音效: {soundType} (音量: {volume}, 时长: {clip.length:F2}秒)");

                    // 验证播放状态
                    StartCoroutine(VerifyPlayback(channel, soundType));
                }
                else
                {
                    Logger.LogWarning($"没有可用音频通道: {soundType}");
                    // 降级到外部音频播放器
                    FallbackToExternalPlayer(soundType);
                }
            }

            private IEnumerator VerifyPlayback(AudioSource source, string soundType)
            {
                yield return new WaitForSeconds(0.1f);

                if (!source.isPlaying)
                {
                    Logger.LogError($"音频播放失败: {soundType}");
                    // 尝试重新初始化
                    if (!isInitialized)
                    {
                        Logger.LogInfo("尝试重新初始化音频管理器");
                        Initialize();
                    }
                }
            }

            private void FallbackToExternalPlayer(string soundType)
            {
                Logger.LogInfo($"降级到外部音频播放器: {soundType}");

                switch (soundType)
                {
                    case "hit": ExternalAudioPlayer.Instance.PlayHitSound(); break;
                    case "kill": ExternalAudioPlayer.Instance.PlayKillSound(); break;
                    case "headshot": ExternalAudioPlayer.Instance.PlayHeadshotSound(); break;
                    case "headshot_kill": ExternalAudioPlayer.Instance.PlayHeadshotKillSound(); break;
                    case "armor_hit": ExternalAudioPlayer.Instance.PlayArmorHitSound(); break;
                    case "armor_break": ExternalAudioPlayer.Instance.PlayArmorBreakSound(); break;
                    default:
                        if (soundType.StartsWith("kill_") && int.TryParse(soundType.Split('_')[1], out int streak))
                            ExternalAudioPlayer.Instance.PlayKillStreakSound(streak);
                        break;
                }
            }

            private AudioSource GetAvailableChannel(string soundType, bool isHighPriority)
            {
                // 查找空闲通道
                foreach (var channel in audioChannels)
                {
                    if (!channel.isPlaying)
                        return channel;
                }

                // 高优先级音效可以中断低优先级音效
                if (isHighPriority)
                {
                    foreach (var channel in audioChannels)
                    {
                        if (channel.isPlaying && IsLowPrioritySound(GetPlayingSoundType(channel)))
                        {
                            channel.Stop();
                            return channel;
                        }
                    }
                }

                return null;
            }

            private bool IsLowPrioritySound(string soundType)
            {
                return soundType == "hit" || soundType == "armor_hit";
            }

            private string GetPlayingSoundType(AudioSource channel)
            {
                if (!channel.isPlaying || channel.clip == null) return null;

                foreach (var pair in audioClips)
                {
                    if (pair.Value == channel.clip)
                        return pair.Key;
                }
                return null;
            }

            // 强制重新加载所有音频
            public void ReloadAllAudio()
            {
                Logger.LogInfo("强制重新加载所有音频");
                audioClips.Clear();
                PreloadAllAudioClips();
            }

            void OnDestroy()
            {
                // 清理资源
                foreach (var clip in audioClips.Values)
                {
                    if (clip != null)
                        DestroyImmediate(clip, true);
                }
                audioClips.Clear();
            }
        }
    }
}