using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static HitMarkerPlugin.HitMarkerPlugin;

namespace HitMarkerPlugin
{
    public class HitMarkerUI : MonoBehaviour
    {
        // 独立的命中标记和击杀标记
        private Marker currentHitMarker;
        private Marker currentKillMarker;

        private const float HIT_MARKER_DURATION = 0.2f;
        private const float KILL_MARKER_DURATION = 1f;
        private const float HEADSHOT_KILL_MARKER_DURATION = 1f;
        private const float KILL_STREAK_DURATION = 1f;

        // 动画阶段时长（占HIT_MARKER_DURATION的比例）
        private const float FADE_IN_DURATION_RATIO = 0.3f; // 渐入阶段：0.03秒
        private const float STABLE_DURATION_RATIO = 0.4f;   // 稳定阶段：0.1秒
        private const float FADE_OUT_DURATION_RATIO = 0.3f; // 渐出阶段：0.07秒

        // 移除固定大小，使用配置
        private Vector2 killMarkerBasePosition = new Vector2(0.5f, 0.87f);

        // 添加图标原始尺寸存储
        private Dictionary<Texture2D, Vector2> textureOriginalSizes = new Dictionary<Texture2D, Vector2>();

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
            public bool useFireportPoint = false;
            public Vector2 originalSize; // 新增：存储原始尺寸
        }
        // 基础标记创建方法
        private Marker CreateBaseMarker(Texture2D texture, string type, bool useHitPoint)
        {
            Vector2 originalSize = GetTextureOriginalSize(texture);

            return new Marker
            {
                texture = texture,
                showTime = Time.time,
                alpha = 0f,
                basePosition = new Vector2(0.5f, 0.5f),
                isKillMarker = false,
                markerType = type,
                scale = Vector2.one * 2f,
                rotation = UnityEngine.Random.Range(-10f, 10f),
                useFireportPoint = useHitPoint,
                originalSize = originalSize
            };
        }

        // 获取纹理原始尺寸
        private Vector2 GetTextureOriginalSize(Texture2D texture)
        {
            if (texture == null) return Vector2.zero;

            if (!textureOriginalSizes.ContainsKey(texture))
            {
                textureOriginalSizes[texture] = new Vector2(texture.width, texture.height);
            }
            return textureOriginalSizes[texture];
        }

        // 修改显示方法，添加 useHitPoint 参数
        // 修改标记创建方法，记录原始尺寸
        public void ShowHitMarker(bool useHitPoint = true)
        {
            var marker = CreateBaseMarker(HitMarkerPlugin.HitMarkerTexture, "hit", useHitPoint);
            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
            //ExternalAudioPlayer.Instance.PlayHitSound();
            InternalAudioPlayer.Instance.PlayHitSound();
        }

        public void ShowHeadshotMarker(bool useHitPoint = true)
        {
            var marker = CreateBaseMarker(HitMarkerPlugin.HeadshotMarkerTexture, "headshot", useHitPoint);
            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
            //ExternalAudioPlayer.Instance.PlayHeadshotSound();
            InternalAudioPlayer.Instance.PlayHeadshotSound();
        }

        public void ShowArmorHitMarker(bool useHitPoint = true)
        {
            var marker = CreateBaseMarker(HitMarkerPlugin.ArmorHitMarkerTexture, "armor_hit", useHitPoint);
            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
            //ExternalAudioPlayer.Instance.PlayArmorHitSound();
            InternalAudioPlayer.Instance.PlayArmorHitSound();
        }

        public void ShowArmorBreakMarker(bool useHitPoint = true)
        {
            var marker = CreateBaseMarker(HitMarkerPlugin.ArmorBreakMarkerTexture, "armor_break", useHitPoint);
            ShowHitMarkerInternal(marker, HIT_MARKER_DURATION);
            //ExternalAudioPlayer.Instance.PlayArmorBreakSound();
            InternalAudioPlayer.Instance.PlayArmorBreakSound();
        }

        public void ShowKillMarker()
        {
            var marker = CreateBaseMarker(HitMarkerPlugin.KillMarkerTexture, "kill", false);
            marker.isKillMarker = true;
            marker.basePosition = new Vector2(
                HitMarkerPlugin.KillMarkerPositionX.Value,
                HitMarkerPlugin.KillMarkerPositionY.Value
            );
            ShowKillMarkerInternal(marker, KILL_MARKER_DURATION);
            //ExternalAudioPlayer.Instance.PlayKillSound();
            InternalAudioPlayer.Instance.PlayKillSound();
        }

        public void ShowHeadshotKillMarker()
        {
            var marker = CreateBaseMarker(HitMarkerPlugin.HeadshotKillMarkerTexture, "headshot_kill", false);
            marker.isKillMarker = true;
            marker.basePosition = new Vector2(
                HitMarkerPlugin.KillMarkerPositionX.Value,
                HitMarkerPlugin.KillMarkerPositionY.Value
            );
            ShowKillMarkerInternal(marker, HEADSHOT_KILL_MARKER_DURATION);
            //ExternalAudioPlayer.Instance.PlayHeadshotKillSound();
            InternalAudioPlayer.Instance.PlayHeadshotKillSound();
        }



        public void ShowKillStreakMarker(int streak)
        {
            if (streak < 2 || streak > 6) return;

            var marker = CreateBaseMarker(HitMarkerPlugin.KillStreakTextures[streak], $"kill_streak_{streak}", false);
            marker.isKillMarker = true;
            marker.basePosition = new Vector2(
                HitMarkerPlugin.KillMarkerPositionX.Value,
                HitMarkerPlugin.KillMarkerPositionY.Value
            );
            ShowKillMarkerInternal(marker, KILL_STREAK_DURATION);
            //ExternalAudioPlayer.Instance.PlayKillStreakSound(streak);
            InternalAudioPlayer.Instance.PlayKillStreakSound(streak);
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

            // 计算各阶段结束时间
            float fadeInEndTime = startTime + duration * FADE_IN_DURATION_RATIO;
            float stableEndTime = fadeInEndTime + duration * STABLE_DURATION_RATIO;
            float fadeOutEndTime = endTime;

            while (Time.time < endTime && currentHitMarker != null)
            {
                float currentTime = Time.time;
                float progress = (currentTime - startTime) / duration;

                if (currentTime < fadeInEndTime)
                {
                    // 渐入阶段：从2倍大小缩小到1倍，从半透明转为不透明
                    float fadeInProgress = (currentTime - startTime) / (fadeInEndTime - startTime);
                    currentHitMarker.scale = Vector2.one * (2f - fadeInProgress); // 2 -> 1
                    currentHitMarker.alpha = fadeInProgress; // 0 -> 1
                }
                else if (currentTime < stableEndTime)
                {
                    // 稳定阶段：保持1倍大小和不透明
                    currentHitMarker.scale = Vector2.one;
                    currentHitMarker.alpha = 1f;
                }
                else if (currentTime < fadeOutEndTime)
                {
                    // 渐出阶段：保持1倍大小，从不透明转为透明
                    float fadeOutProgress = (currentTime - stableEndTime) / (fadeOutEndTime - stableEndTime);
                    currentHitMarker.scale = Vector2.one;
                    currentHitMarker.alpha = 1f - fadeOutProgress; // 1 -> 0
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

            // 获取配置的目标位置
            Vector2 targetPosition = GetConfiguredKillMarkerPosition();

            if (progress < 0.05f)
            {
                // 第一阶段：从配置位置的下方飞入
                float flyProgress = progress / 0.05f;
                if (flyProgress > 1f) flyProgress = 1f;

                float scale = 3f - (flyProgress * 2f);
                currentKillMarker.scale = Vector2.one * scale;
                currentKillMarker.alpha = flyProgress * 2f;
                if (currentKillMarker.alpha > 1f) currentKillMarker.alpha = 1f;

                // 根据配置位置计算飞入起点
                Vector2 startPosition = targetPosition + new Vector2(0, Screen.height * 0.05f);
                currentKillMarker.position = Vector2.Lerp(startPosition, targetPosition, flyProgress);
            }
            else if (progress < 0.15f)
            {
                // 第二阶段：以配置位置为中心震颤
                float shakeProgress = (progress - 0.05f) / 0.1f;
                float shakeIntensity = (1f - shakeProgress) * 8f;
                float shakeTime = Time.time * 40f;
                float shakeX = Mathf.Sin(shakeTime * 1.2f) * shakeIntensity;
                float shakeY = Mathf.Cos(shakeTime * 0.8f) * shakeIntensity;

                currentKillMarker.position = targetPosition + new Vector2(shakeX, shakeY);
                currentKillMarker.scale = Vector2.one * (1f + Mathf.Sin(shakeTime * 1.5f) * 0.05f);
                currentKillMarker.rotation = Mathf.Sin(shakeTime * 1.0f) * 10f;
            }
            else
            {
                currentKillMarker.scale = Vector2.one;
                currentKillMarker.position = targetPosition;
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

        // 获取配置的击杀图标位置
        private Vector2 GetConfiguredKillMarkerPosition()
        {
            return new Vector2(
                Screen.width * HitMarkerPlugin.KillMarkerPositionX.Value,
                Screen.height * HitMarkerPlugin.KillMarkerPositionY.Value
            );
        }


        private void OnGUI()
        {
            // 绘制命中标记（如果有）
            DrawMarker(currentHitMarker);

            // 绘制击杀标记（如果有）
            DrawMarker(currentKillMarker);
        }
        // 绘制方法
        private void DrawMarker(Marker marker)
        {
            if (marker == null || marker.texture == null) return;

            Vector2 drawPosition;

            if (marker.isKillMarker)
            {
                drawPosition = GetConfiguredKillMarkerPosition();
            }
            else if (marker.useFireportPoint)
            {
                drawPosition = HitMarkerPlugin.GetFireportScreenPoint();
            }
            else
            {
                drawPosition = GetScreenPosition(marker.basePosition);
            }

            Rect drawRect;

            if (HitMarkerPlugin.UseOriginalIconSize.Value && marker.originalSize != Vector2.zero)
            {
                // 直接使用原始尺寸，不进行任何额外的缩放计算
                float originalWidth = marker.originalSize.x;
                float originalHeight = marker.originalSize.y;

                // 只应用动画缩放和配置缩放
                float finalWidth = originalWidth * marker.scale.x;
                float finalHeight = originalHeight * marker.scale.y;

                // 应用配置的尺寸缩放
                float sizeScale = marker.isKillMarker ?
                    HitMarkerPlugin.KillMarkerSizeScale.Value :
                    HitMarkerPlugin.HitMarkerSizeScale.Value;

                finalWidth *= sizeScale;
                finalHeight *= sizeScale;

                drawRect = new Rect(
                    drawPosition.x - finalWidth * 0.5f,
                    drawPosition.y - finalHeight * 0.5f,
                    finalWidth,
                    finalHeight
                );
            }
            else
            {
                // 非原始尺寸模式保持原有逻辑
                float baseSize = marker.isKillMarker ? 300f : 50f;
                float sizeScale = marker.isKillMarker ?
                    HitMarkerPlugin.KillMarkerSizeScale.Value :
                    HitMarkerPlugin.HitMarkerSizeScale.Value;

                float size = baseSize * marker.scale.x * sizeScale;

                drawRect = new Rect(
                    drawPosition.x - size * 0.5f,
                    drawPosition.y - size * 0.5f,
                    size,
                    size
                );
            }

            // 其余绘制代码保持不变...
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, marker.alpha);

            if (Mathf.Abs(marker.rotation) > 0.01f)
            {
                Matrix4x4 matrixBackup = GUI.matrix;
                Vector2 pivotPoint = new Vector2(drawRect.x + drawRect.width * 0.5f, drawRect.y + drawRect.height * 0.5f);
                GUIUtility.RotateAroundPivot(marker.rotation, pivotPoint);
                GUI.DrawTexture(drawRect, marker.texture);
                GUI.matrix = matrixBackup;
            }
            else
            {
                GUI.DrawTexture(drawRect, marker.texture);
            }

            GUI.color = originalColor;
        }
    }
}