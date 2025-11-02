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
            public float rotation;
            public Coroutine coroutine;
        }

        // 独立的命中标记和击杀标记
        private Marker currentHitMarker;
        private Marker currentKillMarker;

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
                basePosition = new Vector2(0.5f, 0.5f),
                isKillMarker = false,
                markerType = "hit",
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f),
                rotation = UnityEngine.Random.Range(-10f, 10f)
            };

            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayHitSound();
        }

        public void ShowKillMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.KillMarkerTexture,
                showTime = Time.time,
                alpha = 0f,
                basePosition = killMarkerBasePosition,
                isKillMarker = true,
                markerType = "kill",
                scale = Vector2.one * 3f,
                rotation = 0f
            };

            ShowKillMarkerInternal(marker, KILL_MARKER_DURATION);
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
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f),
                rotation = UnityEngine.Random.Range(-10f, 10f)
            };

            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayHeadshotSound();
        }

        public void ShowHeadshotKillMarker()
        {
            var marker = new Marker
            {
                texture = HitMarkerPlugin.HeadshotKillMarkerTexture,
                showTime = Time.time,
                alpha = 0f,
                basePosition = killMarkerBasePosition,
                isKillMarker = true,
                markerType = "headshot_kill",
                scale = Vector2.one * 3f,
                rotation = 0f
            };

            ShowKillMarkerInternal(marker, HEADSHOT_KILL_MARKER_DURATION);
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
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f),
                rotation = UnityEngine.Random.Range(-10f, 10f)
            };

            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
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
                scale = Vector2.one * UnityEngine.Random.Range(0.9f, 1.25f),
                rotation = UnityEngine.Random.Range(-10f, 10f)
            };

            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
            ExternalAudioPlayer.Instance.PlayArmorBreakSound();
        }

        public void ShowKillStreakMarker(int streak)
        {
            if (streak < 2 || streak > 6) return;

            var marker = new Marker
            {
                texture = HitMarkerPlugin.KillStreakTextures[streak],
                showTime = Time.time,
                alpha = 0f,
                basePosition = killMarkerBasePosition,
                isKillMarker = true,
                markerType = $"kill_streak_{streak}",
                scale = Vector2.one * 3f,
                rotation = 0f
            };

            ShowKillMarkerInternal(marker, KILL_STREAK_DURATION);
            ExternalAudioPlayer.Instance.PlayKillStreakSound(streak);
        }

        private void ShowHitMarkerInternal(Marker marker, float duration)
        {
            // 如果已经有命中标记在显示，停止之前的协程
            if (currentHitMarker != null && currentHitMarker.coroutine != null)
            {
                StopCoroutine(currentHitMarker.coroutine);
            }

            currentHitMarker = marker;
            currentHitMarker.coroutine = StartCoroutine(HitMarkerAnimation(duration));
        }

        private void ShowKillMarkerInternal(Marker marker, float duration)
        {
            // 如果已经有击杀标记在显示，停止之前的协程
            if (currentKillMarker != null && currentKillMarker.coroutine != null)
            {
                StopCoroutine(currentKillMarker.coroutine);
            }

            currentKillMarker = marker;
            currentKillMarker.coroutine = StartCoroutine(KillMarkerAnimation(duration));
        }

        private IEnumerator HitMarkerAnimation(float duration)
        {
            float startTime = Time.time;
            float endTime = startTime + duration;

            while (Time.time < endTime && currentHitMarker != null)
            {
                float progress = (Time.time - startTime) / duration;

                // 淡出效果（只在最后0.2秒开始淡出）
                float fadeStart = 0.6f;
                if (progress > fadeStart)
                {
                    currentHitMarker.alpha = 1f - ((progress - fadeStart) / (1f - fadeStart));
                }
                else
                {
                    currentHitMarker.alpha = 1f;
                }

                yield return null;
            }

            currentHitMarker = null;
        }

        private IEnumerator KillMarkerAnimation(float duration)
        {
            float startTime = Time.time;
            float endTime = startTime + duration;

            while (Time.time < endTime && currentKillMarker != null)
            {
                float progress = (Time.time - startTime) / duration;

                // 击杀标记的特殊效果
                ApplyKillMarkerEffects(progress);

                // 淡出效果（只在最后10%开始淡出）
                float fadeStart = 0.9f;
                if (progress > fadeStart)
                {
                    currentKillMarker.alpha = 1f - ((progress - fadeStart) / (1f - fadeStart));
                }

                yield return null;
            }

            currentKillMarker = null;
        }

        private void ApplyKillMarkerEffects(float progress)
        {
            if (currentKillMarker == null) return;

            if (progress < 0.05f)
            {
                // 第一阶段：快速飞入动画
                float flyProgress = progress / 0.05f;
                if (flyProgress > 1f) flyProgress = 1f;

                // 缩放效果：从300%缩小到100%
                float scale = 3f - (flyProgress * 2f);
                currentKillMarker.scale = Vector2.one * scale;

                // 淡入效果：快速淡入
                currentKillMarker.alpha = flyProgress * 2f;
                if (currentKillMarker.alpha > 1f) currentKillMarker.alpha = 1f;

                // 位置动画：从下方不远的位置飞入
                Vector2 targetPosition = GetScreenPosition(currentKillMarker.basePosition);
                Vector2 startPosition = targetPosition + new Vector2(0, Screen.height * 0.05f);
                currentKillMarker.position = Vector2.Lerp(startPosition, targetPosition, flyProgress);
            }
            else if (progress < 0.15f)
            {
                // 第二阶段：短促有力的震颤效果
                float shakeProgress = (progress - 0.05f) / 0.1f;

                // 基本位置
                Vector2 basePos = GetScreenPosition(currentKillMarker.basePosition);

                // 震颤强度快速衰减
                float shakeIntensity = (1f - shakeProgress) * 8f;

                // 使用正弦波生成震颤
                float shakeTime = Time.time * 40f;
                float shakeX = Mathf.Sin(shakeTime * 1.2f) * shakeIntensity;
                float shakeY = Mathf.Cos(shakeTime * 0.8f) * shakeIntensity;

                currentKillMarker.position = basePos + new Vector2(shakeX, shakeY);

                // 缩放震颤
                currentKillMarker.scale = Vector2.one * (1f + Mathf.Sin(shakeTime * 1.5f) * 0.05f);

                // 旋转震颤 ±10度
                currentKillMarker.rotation = Mathf.Sin(shakeTime * 1.0f) * 10f;
            }
            else
            {
                currentKillMarker.scale = Vector2.one;
                currentKillMarker.position = GetScreenPosition(currentKillMarker.basePosition);
                currentKillMarker.rotation = 0f;
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
            // 绘制命中标记（如果有）
            DrawMarker(currentHitMarker);

            // 绘制击杀标记（如果有）
            DrawMarker(currentKillMarker);
        }

        private void DrawMarker(Marker marker)
        {
            if (marker == null || marker.texture == null) return;

            // 计算绘制位置和尺寸
            Vector2 drawPosition = marker.isKillMarker ?
                marker.position :
                GetScreenPosition(marker.basePosition);

            float baseSize = marker.isKillMarker ? 300f : 50f;
            float size = baseSize * marker.scale.x;
            Rect drawRect = new Rect(
                drawPosition.x - size * 0.5f,
                drawPosition.y - size * 0.5f,
                size,
                size
            );

            // 应用透明度
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, marker.alpha);

            // 应用旋转
            if (Mathf.Abs(marker.rotation) > 0.01f)
            {
                Matrix4x4 matrixBackup = GUI.matrix;

                // 计算旋转中心
                Vector2 pivotPoint = new Vector2(drawRect.x + drawRect.width * 0.5f, drawRect.y + drawRect.height * 0.5f);

                // 应用旋转矩阵
                GUIUtility.RotateAroundPivot(marker.rotation, pivotPoint);
                GUI.DrawTexture(drawRect, marker.texture);

                // 恢复矩阵
                GUI.matrix = matrixBackup;
            }
            else
            {
                // 不旋转，直接绘制
                GUI.DrawTexture(drawRect, marker.texture);
            }

            // 恢复GUI颜色
            GUI.color = originalColor;
        }
    }
}