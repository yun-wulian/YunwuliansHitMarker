using System;
using System.Collections;
using UnityEngine;
using static HitMarkerPlugin.HitMarkerPlugin;

namespace HitMarkerPlugin
{
    public class HitMarkerUI : MonoBehaviour
    {
        private class Marker
        {
            public Texture2D texture;
            public float showTime;
            public float alpha = 1f;
            public Vector2 position;
            public Vector2 scale = Vector2.one;
            public Vector2 basePosition;
            public bool isKillMarker;
            public string markerType;
            public float rotation; // 添加旋转字段
        }

        private Marker currentMarker;
        private Coroutine markerCoroutine;

        private const float HIT_MARKER_DURATION = 0.2f;
        private const float KILL_MARKER_DURATION = 1f;
        private const float HEADSHOT_KILL_MARKER_DURATION = 1f;
        private const float KILL_STREAK_DURATION = 1f;

        // 击杀图标位置（屏幕中间下方）
        private Vector2 killMarkerBasePosition = new Vector2(0.5f, 0.87f);

        public void ShowHitMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.HitMarkerTexture,
                showTime = Time.time,
                alpha = 1f,
                basePosition = new Vector2(0.5f, 0.5f), // 屏幕中心
                isKillMarker = false,
                markerType = "hit",
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f), // 增大缩放范围 90%-125%
                rotation = UnityEngine.Random.Range(-10f, 10f) // 增大旋转范围 -10° 到 10°
            };

            ShowMarker(marker, HIT_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayHitSound();
        }

        public void ShowKillMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.KillMarkerTexture,
                showTime = Time.time,
                alpha = 0f, // 初始完全透明
                basePosition = killMarkerBasePosition,
                isKillMarker = true,
                markerType = "kill",
                scale = Vector2.one * 3f, // 初始放大 300%
                rotation = 0f
            };

            ShowMarker(marker, KILL_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayKillSound();
        }

        public void ShowHeadshotMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.HeadshotMarkerTexture,
                showTime = Time.time,
                alpha = 1f,
                basePosition = new Vector2(0.5f, 0.5f),
                isKillMarker = false,
                markerType = "headshot",
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f), // 增大缩放范围
                rotation = UnityEngine.Random.Range(-10f, 10f) // 增大旋转范围
            };

            ShowMarker(marker, HIT_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayHeadshotSound();
        }

        public void ShowHeadshotKillMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.HeadshotKillMarkerTexture,
                showTime = Time.time,
                alpha = 0f, // 初始完全透明
                basePosition = killMarkerBasePosition,
                isKillMarker = true,
                markerType = "headshot_kill",
                scale = Vector2.one * 3f, // 初始放大 300%
                rotation = 0f
            };

            ShowMarker(marker, HEADSHOT_KILL_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayHeadshotKillSound();
        }

        public void ShowArmorHitMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.ArmorHitMarkerTexture,
                showTime = Time.time,
                alpha = 1f,
                basePosition = new Vector2(0.5f, 0.5f),
                isKillMarker = false,
                markerType = "armor_hit",
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f), // 增大缩放范围
                rotation = UnityEngine.Random.Range(-10f, 10f) // 增大旋转范围
            };

            ShowMarker(marker, HIT_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayArmorHitSound();
        }

        public void ShowArmorBreakMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.ArmorBreakMarkerTexture,
                showTime = Time.time,
                alpha = 1f,
                basePosition = new Vector2(0.5f, 0.5f),
                isKillMarker = false,
                markerType = "armor_break",
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f), // 增大缩放范围
                rotation = UnityEngine.Random.Range(-10f, 10f) // 增大旋转范围
            };

            ShowMarker(marker, HIT_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayArmorBreakSound();
        }

        public void ShowKillStreakMarker(int streak)
        {
            if (streak < 2 || streak > 6) return;

            var marker = new Marker
            {
                texture = HitMarkerPlugin.KillStreakTextures[streak],
                showTime = Time.time,
                alpha = 0f, // 初始完全透明
                basePosition = killMarkerBasePosition,
                isKillMarker = true,
                markerType = $"kill_streak_{streak}",
                scale = Vector2.one * 3f, // 初始放大 300%
                rotation = 0f
            };

            ShowMarker(marker, KILL_STREAK_DURATION);
            ExternalAudioPlayer.Instance.PlayKillStreakSound(streak);
        }

        private void ShowMarker(Marker marker, float duration)
        {
            // 如果已经有标记在显示，停止之前的协程
            if (markerCoroutine != null)
            {
                StopCoroutine(markerCoroutine);
            }

            currentMarker = marker;
            markerCoroutine = StartCoroutine(MarkerAnimation(duration));
        }

        private IEnumerator MarkerAnimation(float duration)
        {
            float startTime = Time.time;
            float endTime = startTime + duration;

            while (Time.time < endTime)
            {
                float progress = (Time.time - startTime) / duration;

                if (currentMarker != null)
                {
                    // 淡出效果（只在最后0.2秒开始淡出）
                    float fadeStart = currentMarker.isKillMarker ? 0.9f : 0.6f; // 击杀标记在最后10%开始淡出
                    if (progress > fadeStart)
                    {
                        currentMarker.alpha = 1f - ((progress - fadeStart) / (1f - fadeStart));
                    }
                    else
                    {
                        currentMarker.alpha = 1f;
                    }

                    // 击杀标记的特殊效果
                    if (currentMarker.isKillMarker)
                    {
                        ApplyKillMarkerEffects(progress);
                    }
                }

                yield return null;
            }

            currentMarker = null;
            markerCoroutine = null;
        }

        private void ApplyKillMarkerEffects(float progress)
        {
            if (currentMarker == null) return;

            // 击杀标记动画分为三个阶段：
            // 1. 0-0.05秒：快速飞入 + 缩放缩小 + 淡入
            // 2. 0.05-0.15秒：到位后的震颤效果
            // 3. 0.15秒后：保持稳定直到淡出（现在稳定阶段延长到1.5秒）

            if (progress < 0.05f) // 0.05秒飞入，按2秒总时长调整比例
            {
                // 第一阶段：快速飞入动画
                float flyProgress = progress / 0.05f;
                if (flyProgress > 1f) flyProgress = 1f;

                // 缩放效果：从300%缩小到100%
                float scale = 3f - (flyProgress * 2f);
                currentMarker.scale = Vector2.one * scale;

                // 淡入效果：快速淡入
                currentMarker.alpha = flyProgress * 2f; // 快速淡入，0.05秒就完全显示
                if (currentMarker.alpha > 1f) currentMarker.alpha = 1f;

                // 位置动画：从下方不远的位置飞入
                Vector2 targetPosition = GetScreenPosition(currentMarker.basePosition);
                Vector2 startPosition = targetPosition + new Vector2(0, Screen.height * 0.05f); // 只从下方5%屏幕高度开始
                currentMarker.position = Vector2.Lerp(startPosition, targetPosition, flyProgress);
            }
            else if (progress < 0.15f)
            {
                // 第二阶段：短促有力的震颤效果
                float shakeProgress = (progress - 0.05f) / 0.1f;

                // 基本位置
                Vector2 basePos = GetScreenPosition(currentMarker.basePosition);

                // 震颤强度快速衰减
                float shakeIntensity = (1f - shakeProgress) * 8f; // 减小震颤强度

                // 使用正弦波生成震颤
                float shakeTime = Time.time * 40f; // 提高频率
                float shakeX = Mathf.Sin(shakeTime * 1.2f) * shakeIntensity;
                float shakeY = Mathf.Cos(shakeTime * 0.8f) * shakeIntensity;

                currentMarker.position = basePos + new Vector2(shakeX, shakeY);

                // 缩放震颤（幅度减小）
                currentMarker.scale = Vector2.one * (1f + Mathf.Sin(shakeTime * 1.5f) * 0.05f); // 减小缩放震颤幅度

                // 旋转震颤 ±10度
                currentMarker.rotation = Mathf.Sin(shakeTime * 1.0f) * 10f; // 正负10度旋转
            }
            else
            {
                currentMarker.scale = Vector2.one;
                currentMarker.position = GetScreenPosition(currentMarker.basePosition);
                currentMarker.rotation = 0f;
            }
        }

        private Vector2 GetScreenPosition(Vector2 normalizedPosition)
        {
            return new Vector2(
                Screen.width * normalizedPosition.x,
                Screen.height * normalizedPosition.y
            );
        }

        private void OnGUI()
        {
            if (currentMarker == null || currentMarker.texture == null) return;

            // 计算绘制位置和尺寸
            Vector2 drawPosition = currentMarker.isKillMarker ?
                currentMarker.position :
                GetScreenPosition(currentMarker.basePosition);

            float baseSize = currentMarker.isKillMarker ? 300f : 50f;
            float size = baseSize * currentMarker.scale.x;
            Rect drawRect = new Rect(
                drawPosition.x - size * 0.5f,
                drawPosition.y - size * 0.5f,
                size,
                size
            );

            // 应用透明度
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, currentMarker.alpha);

            // 应用旋转
            if (Mathf.Abs(currentMarker.rotation) > 0.01f)
            {
                Matrix4x4 matrixBackup = GUI.matrix;

                // 计算旋转中心
                Vector2 pivotPoint = new Vector2(drawRect.x + drawRect.width * 0.5f, drawRect.y + drawRect.height * 0.5f);

                // 应用旋转矩阵
                GUIUtility.RotateAroundPivot(currentMarker.rotation, pivotPoint);
                GUI.DrawTexture(drawRect, currentMarker.texture);

                // 恢复矩阵
                GUI.matrix = matrixBackup;
            }
            else
            {
                // 不旋转，直接绘制
                GUI.DrawTexture(drawRect, currentMarker.texture);
            }

            // 恢复GUI颜色
            GUI.color = originalColor;
        }
    }
}