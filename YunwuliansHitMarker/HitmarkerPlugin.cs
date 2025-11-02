using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;

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

            // 创建配置
            CreateConfig();

            // 初始化UI管理器
            uiManager = gameObject.AddComponent<HitMarkerUI>();
            testWindow = gameObject.AddComponent<TestWindow>();

            // 应用Harmony补丁
            var harmony = new Harmony("com.yourname.hitmarker");
            harmony.PatchAll();

            // 延迟加载资源，确保Unity完全初始化
            Invoke("DelayedLoadResources", 1f);

            Logger.LogInfo("Hit Marker插件加载完成！");
        }

        private void DelayedLoadResources()
        {
            LoadResources();
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
                string soundPath = Path.Combine(resourcePath, "sound");

                // 确保目录存在
                Directory.CreateDirectory(texturePath);
                Directory.CreateDirectory(soundPath);

                // 加载基础图标
                HitMarkerTexture = LoadTexture(Path.Combine(texturePath, "hit_marker.png"));
                KillMarkerTexture = LoadTexture(Path.Combine(texturePath, "kill_marker.png"));
                HeadshotMarkerTexture = LoadTexture(Path.Combine(texturePath, "headshot_marker.png")) ?? KillMarkerTexture;
                HeadshotKillMarkerTexture = LoadTexture(Path.Combine(texturePath, "headshot_kill_marker.png")) ?? KillMarkerTexture;
                ArmorHitMarkerTexture = LoadTexture(Path.Combine(texturePath, "armor_hit_marker.png")) ?? HitMarkerTexture;
                ArmorBreakMarkerTexture = LoadTexture(Path.Combine(texturePath, "armor_break_marker.png")) ?? HitMarkerTexture;

                // 加载连杀图标 (2-6连杀)
                for (int i = 2; i <= 6; i++)
                {
                    KillStreakTextures[i] = LoadTexture(Path.Combine(texturePath, $"kill_{i}_marker.png")) ?? KillMarkerTexture;
                }

                // 创建默认图标
                if (HitMarkerTexture == null) HitMarkerTexture = CreateDefaultHitMarker();
                if (KillMarkerTexture == null) KillMarkerTexture = CreateDefaultKillMarker();

                Logger.LogInfo("资源加载完成");
            }
            catch (Exception e)
            {
                Logger.LogError($"资源加载失败: {e.Message}");
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

        public void PlayHitEffect()
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                uiManager.ShowHitMarker();
            }
        }

        public void PlayKillEffect()
        {
            if (EnableKillMarker.Value && uiManager != null)
            {
                uiManager.ShowKillMarker();
            }
        }

        public void PlayHeadshotEffect()
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发爆头命中效果");
                uiManager.ShowHeadshotMarker();
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

        public void PlayArmorHitEffect()
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发护甲命中效果");
                uiManager.ShowArmorHitMarker();
            }
        }

        public void PlayArmorBreakEffect()
        {
            if (EnableHitMarker.Value && uiManager != null)
            {
                Logger.LogInfo($"触发护甲击穿效果");
                uiManager.ShowArmorBreakMarker();
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
            PlayHitEffect();
        }

        public void TestArmorHitEffect()
        {
            Logger.LogInfo("测试命中效果");
            PlayArmorHitEffect();
        }

        public void TestArmorBreakEffect()
        {
            Logger.LogInfo("测试命中效果");
            PlayArmorBreakEffect();
        }
        public void TestHeadshotEffect()
        {
            Logger.LogInfo("测试爆头效果");
            PlayHeadshotEffect();
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
            public static int ApplyDamageInfoCount { get; set; }
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
                ApplyDamageInfoCount = 0;
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
    }
}