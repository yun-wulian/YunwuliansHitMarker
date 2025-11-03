using HarmonyLib;
using UnityEngine;
using static HitMarkerPlugin.HitMarkerPlugin;

namespace HitMarkerPlugin
{
    public class TestWindow : MonoBehaviour
    {
        private Rect windowRect = new Rect(20, 20, 500, 800);
        private bool isDragging = false;
        private Vector2 dragStartPosition;
        private Rect dragArea = new Rect(0, 0, 300, 20); // 标题栏拖动区域
        private void OnGUI()
        {
            if (!HitMarkerPlugin.ShowTestWindow.Value)
                return;

            // 设置窗口样式
            GUIStyle windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.background = CreateTexture(2, 2, new Color(0.1f, 0.1f, 0.1f, 0.9f));

            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.normal.textColor = Color.white;
            labelStyle.alignment = TextAnchor.MiddleCenter;

            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.yellow;

            // 绘制窗口
            windowRect = GUI.Window(9999, windowRect, DrawWindow, "Hit Marker 测试窗口", windowStyle);

            // 处理窗口拖动
            HandleWindowDrag();
        }

        private void DrawWindow(int windowID)
        {
            GUILayout.BeginVertical();

            // 状态信息
            GUILayout.Label($"命中提示: {(HitMarkerPlugin.EnableHitMarker.Value ? "启用" : "禁用")}", GetLabelStyle());
            GUILayout.Label($"击杀提示: {(HitMarkerPlugin.EnableKillMarker.Value ? "启用" : "禁用")}", GetLabelStyle());
            GUILayout.Label($"音量: 命中{HitMarkerPlugin.HitMarkerVolume.Value} 击杀{HitMarkerPlugin.KillMarkerVolume.Value}", GetLabelStyle());

            GUILayout.Space(10);

            // 连杀信息
            GUILayout.Label($"当前连杀: {HitMarkerPlugin.Instance.GetCurrentKillStreak()}", GetLabelStyle());
            GUILayout.Label($"最后击杀时间: {HitMarkerPlugin.Instance.GetType().GetField("lastKillTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(HitMarkerPlugin.Instance)}", GetLabelStyle());

            GUILayout.Space(10);

            // 在TestWindow的DrawWindow方法中添加
            GUILayout.Label($"本地玩家: {(LocalPlayerTracker.LocalPlayer != null ? "已设置" : "未设置")}", GetLabelStyle());
            GUILayout.Label($"命中图标: {(HitMarkerPlugin.HitMarkerTexture != null ? "已加载" : "未加载")}", GetLabelStyle());
            GUILayout.Label($"击杀图标: {(HitMarkerPlugin.KillMarkerTexture != null ? "已加载" : "未加载")}", GetLabelStyle());
            GUILayout.Label($"爆头图标: {(HitMarkerPlugin.HeadshotMarkerTexture != null ? "已加载" : "未加载")}", GetLabelStyle());

            GUILayout.Space(10);

            // === 调试信息 ===
            GUILayout.Label("=== 调试信息 ===", GetLabelStyle());

            // 补丁调用统计
            GUILayout.Label($"ApplyDamageInfo调用: {HitMarkerPlugin.DebugInfo.ReceiveDamageCount}", GetLabelStyle());
            GUILayout.Label($"ArmorDamage调用: {HitMarkerPlugin.DebugInfo.ArmorDamageCount}", GetLabelStyle());
            GUILayout.Label($"KillEvent调用: {HitMarkerPlugin.DebugInfo.KillEventCount}", GetLabelStyle());

            GUILayout.Space(5);

            GUILayout.Label($"有效命中: {HitMarkerPlugin.DebugInfo.ValidHitCount}", GetLabelStyle());
            GUILayout.Label($"无效命中: {HitMarkerPlugin.DebugInfo.InvalidHitCount}", GetLabelStyle());
            GUILayout.Label($"本地玩家为空: {HitMarkerPlugin.DebugInfo.LocalPlayerNullCount}", GetLabelStyle());
            GUILayout.Label($"总击杀数: {HitMarkerPlugin.DebugInfo.TotalKills}", GetLabelStyle());

            GUILayout.Space(5);

            // 最近事件
            GUILayout.Label($"最后错误: {HitMarkerPlugin.DebugInfo.LastError}", GetLabelStyle());
            GUILayout.Label($"非玩家伤害: {HitMarkerPlugin.DebugInfo.LastNonPlayerHit}", GetLabelStyle());
            GUILayout.Label($"非玩家击杀: {HitMarkerPlugin.DebugInfo.LastNonPlayerKill}", GetLabelStyle());

            GUILayout.Space(5);

            // 详细信息
            if (HitMarkerPlugin.DebugInfo.LastHitInfo != null)
            {
                GUILayout.Label("最后命中:", GetLabelStyle());
                GUILayout.TextArea(HitMarkerPlugin.DebugInfo.LastHitInfo.ToString(), GetLabelStyle());
            }

            if (HitMarkerPlugin.DebugInfo.LastArmorHit != null)
            {
                GUILayout.Label("最后护甲命中:", GetLabelStyle());
                GUILayout.TextArea(HitMarkerPlugin.DebugInfo.LastArmorHit.ToString(), GetLabelStyle());
            }

            if (HitMarkerPlugin.DebugInfo.LastKillInfo != null)
            {
                GUILayout.Label("最后击杀:", GetLabelStyle());
                GUILayout.TextArea(HitMarkerPlugin.DebugInfo.LastKillInfo.ToString(), GetLabelStyle());
            }

            GUILayout.Space(10);

            // 测试按钮
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试命中效果", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.TestHitEffect();
            }
            if (GUILayout.Button("测试爆头命中效果", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.TestHeadshotEffect();
            }
            if (GUILayout.Button("测试护甲命中效果", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.TestArmorHitEffect();
            }
            if (GUILayout.Button("测试护甲击碎效果", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.TestArmorBreakEffect();
            }
            GUILayout.EndHorizontal();


            GUILayout.BeginHorizontal();
            if (GUILayout.Button("测试击杀效果", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.TestKillEffect();
            }
            if (GUILayout.Button("测试爆头击杀效果", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.TestHeadshotKillEffect();
            }
            if (GUILayout.Button("测试连杀效果", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.TestKillStreakEffect();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 调试按钮
            if (GUILayout.Button("重置调试统计", GetButtonStyle()))
            {
                HitMarkerPlugin.DebugInfo.Reset();
            }

            if (GUILayout.Button("重置连杀", GetButtonStyle()))
            {
                HitMarkerPlugin.Instance.ResetKillStreak();
            }

            if (GUILayout.Button("关闭窗口", GetButtonStyle()))
            {
                HitMarkerPlugin.ShowTestWindow.Value = false;
            }

            GUILayout.EndVertical();

            // 允许拖动窗口
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private void HandleWindowDrag()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown)
            {
                if (windowRect.Contains(currentEvent.mousePosition))
                {
                    Vector2 localMousePos = new Vector2(
                        currentEvent.mousePosition.x - windowRect.x,
                        currentEvent.mousePosition.y - windowRect.y
                    );

                    if (dragArea.Contains(localMousePos))
                    {
                        isDragging = true;
                        dragStartPosition = currentEvent.mousePosition - new Vector2(windowRect.x, windowRect.y);
                        currentEvent.Use();
                    }
                }
            }
            else if (currentEvent.type == EventType.MouseDrag && isDragging)
            {
                windowRect.position = currentEvent.mousePosition - dragStartPosition;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                isDragging = false;
            }
        }

        private GUIStyle GetLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = Color.white;
            style.alignment = TextAnchor.MiddleCenter;
            return style;
        }

        private GUIStyle GetButtonStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.textColor = Color.white;
            style.hover.textColor = Color.yellow;
            return style;
        }

        private Texture2D CreateTexture(int width, int height, Color color)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = color;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}