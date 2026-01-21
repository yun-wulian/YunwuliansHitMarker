using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HitMarkerPlugin
{
    // 伤害事件类型
    public enum DamageEventType
    {
        Headshot,       // 爆头
        ArmorBreak,     // 护甲击穿
        ArmorHit,       // 护甲命中
        BodyHit         // 纯肉体伤害
    }

    // 伤害事件数据
    public class DamageEvent
    {
        public string EventId { get; set; }
        public DateTime Timestamp { get; set; }
        public EBodyPart BodyPart { get; set; }
        public float Damage { get; set; }
        public float ArmorDamage { get; set; }
        public IPlayer Target { get; set; }
        public bool IsHeadshot => BodyPart == EBodyPart.Head;
        public bool IsArmorHit { get; set; }
        public bool IsArmorBroken { get; set; }
        public bool IsProcessed { get; set; }
        public DamageEventType EventType
        {
            get
            {
                if (IsHeadshot) return DamageEventType.Headshot;
                if (IsArmorBroken) return DamageEventType.ArmorBreak;
                if (IsArmorHit) return DamageEventType.ArmorHit;
                return DamageEventType.BodyHit;
            }
        }

        public DamageEvent()
        {
            EventId = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
        }
    }

    // 伤害事件收集器
    public static class DamageEventCollector
    {
        private static readonly Dictionary<string, DamageEvent> _pendingEvents = new Dictionary<string, DamageEvent>();
        private static readonly object _lock = new object();
        private const float EVENT_EXPIRE_TIME = 2.0f; // 事件过期时间（秒）

        public static void AddArmorHit(DamageInfoStruct damageInfo, float armorDamage, bool isBroken, ArmorComponent armorComponent)
        {
            // 使用原有的护甲伤害来源判断逻辑
            if (!IsDamageFromLocalPlayer(damageInfo) || armorDamage <= 0)
                return;

            try
            {
                var victim = damageInfo.Player?.iPlayer;
                if (victim == null) return;

                string eventKey = $"{victim.ProfileId}_{DateTime.Now.Ticks}";

                lock (_lock)
                {
                    if (!_pendingEvents.ContainsKey(eventKey))
                    {
                        _pendingEvents[eventKey] = new DamageEvent
                        {
                            Target = victim,
                            IsArmorHit = true,
                            IsArmorBroken = isBroken,
                            ArmorDamage = armorDamage
                        };
                    }
                    else
                    {
                        var existingEvent = _pendingEvents[eventKey];
                        existingEvent.IsArmorHit = true;
                        existingEvent.IsArmorBroken = isBroken;
                        existingEvent.ArmorDamage = armorDamage;
                    }
                }

                HitMarkerPlugin.Logger.LogInfo($"护甲命中记录: 伤害 {armorDamage}, 击穿: {isBroken}");
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"AddArmorHit错误: {e}");
            }
        }

        public static void AddBodyHit(Player victim, float damage, EBodyPart bodyPart)
        {
            // 使用原有的身体伤害来源判断逻辑
            if (!IsDamageFromLocalPlayer(victim) || damage <= 0)
                return;

            try
            {
                string eventKey = $"{victim.ProfileId}_{DateTime.Now.Ticks}";

                lock (_lock)
                {
                    if (!_pendingEvents.ContainsKey(eventKey))
                    {
                        _pendingEvents[eventKey] = new DamageEvent
                        {
                            Target = victim,
                            Damage = damage,
                            BodyPart = bodyPart
                        };
                    }
                    else
                    {
                        var existingEvent = _pendingEvents[eventKey];
                        existingEvent.Damage = damage;
                        existingEvent.BodyPart = bodyPart;
                    }
                }

                HitMarkerPlugin.Logger.LogInfo($"身体命中记录: 部位 {bodyPart}, 伤害 {damage}");
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"AddBodyHit错误: {e}");
            }
        }

        public static DamageEvent GetHighestPriorityEvent()
        {
            lock (_lock)
            {
                // 清理过期事件
                CleanExpiredEvents();

                if (_pendingEvents.Count == 0)
                    return null;

                // 获取最高优先级事件
                var highestPriorityEvent = _pendingEvents.Values
                    .Where(e => !e.IsProcessed)
                    .OrderByDescending(GetEventPriority)
                    .FirstOrDefault();

                if (highestPriorityEvent != null)
                {
                    highestPriorityEvent.IsProcessed = true;
                    _pendingEvents.Remove(_pendingEvents.First(kv => kv.Value == highestPriorityEvent).Key);
                }

                return highestPriorityEvent;
            }
        }

        private static int GetEventPriority(DamageEvent e)
        {
            if (e.IsHeadshot) return 100;          // 爆头最高
            if (e.IsArmorBroken) return 80;        // 护甲击穿
            if (e.IsArmorHit) return 60;           // 护甲命中
            return 40;                             // 纯肉体伤害
        }

        private static void CleanExpiredEvents()
        {
            var expiredKeys = _pendingEvents
                .Where(kv => (DateTime.Now - kv.Value.Timestamp).TotalSeconds > EVENT_EXPIRE_TIME)
                .Select(kv => kv.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _pendingEvents.Remove(key);
            }

            if (expiredKeys.Count > 0)
            {
                HitMarkerPlugin.Logger.LogDebug($"清理了 {expiredKeys.Count} 个过期事件");
            }
        }

        // 使用原有的护甲伤害来源判断逻辑
        private static bool IsDamageFromLocalPlayer(DamageInfoStruct damageInfo)
        {
            var localPlayer = LocalPlayerTracker.LocalPlayer;
            if (localPlayer == null)
            {
                HitMarkerPlugin.DebugInfo.LocalPlayerNullCount++;
                return false;
            }

            if (damageInfo.Player?.iPlayer?.IsYourPlayer == true)
            {
                HitMarkerPlugin.DebugInfo.ValidHitCount++;
                return true;
            }

            HitMarkerPlugin.DebugInfo.InvalidHitCount++;
            return false;
        }

        // 使用原有的身体伤害来源判断逻辑
        private static bool IsDamageFromLocalPlayer(Player victim)
        {
            var localPlayer = LocalPlayerTracker.LocalPlayer;
            if (localPlayer == null)
            {
                HitMarkerPlugin.DebugInfo.LocalPlayerNullCount++;
                return false;
            }

            var lastDamageInfo = Traverse.Create(victim).Field("LastDamageInfo").GetValue<DamageInfoStruct>();
            if (localPlayer != victim && lastDamageInfo.Player?.iPlayer?.IsYourPlayer == true)
            {
                HitMarkerPlugin.DebugInfo.ValidHitCount++;
                return true;
            }

            HitMarkerPlugin.DebugInfo.InvalidHitCount++;
            return false;
        }
    }

    // 伤害事件处理器
    public class DamageEventProcessor : MonoBehaviour
    {
        private float _lastProcessTime;
        private const float PROCESS_INTERVAL = 0.05f; // 处理间隔50ms

        void Update()
        {
            // 限制处理频率
            if (Time.time - _lastProcessTime < PROCESS_INTERVAL)
                return;

            _lastProcessTime = Time.time;
            ProcessDamageEvents();
        }

        private void ProcessDamageEvents()
        {
            try
            {
                var damageEvent = DamageEventCollector.GetHighestPriorityEvent();
                if (damageEvent == null)
                    return;

                HitMarkerPlugin.Logger.LogInfo($"处理伤害事件: {damageEvent.EventType}, 部位: {damageEvent.BodyPart}");

                switch (damageEvent.EventType)
                {
                    case DamageEventType.Headshot:
                        HitMarkerPlugin.Instance?.PlayHeadshotEffect();
                        break;
                    case DamageEventType.ArmorBreak:
                        HitMarkerPlugin.Instance?.PlayArmorBreakEffect();
                        break;
                    case DamageEventType.ArmorHit:
                        HitMarkerPlugin.Instance?.PlayArmorHitEffect();
                        break;
                    case DamageEventType.BodyHit:
                        HitMarkerPlugin.Instance?.PlayHitEffect();
                        break;
                }

                // 更新调试信息
                UpdateDebugInfo(damageEvent);
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"ProcessDamageEvents错误: {e}");
            }
        }

        private void UpdateDebugInfo(DamageEvent damageEvent)
        {
            HitMarkerPlugin.DebugInfo.LastHitInfo = new HitMarkerPlugin.DebugInfo.HitData
            {
                Timestamp = DateTime.Now,
                TargetName = damageEvent.Target?.Profile?.Nickname ?? "Unknown",
                BodyPart = damageEvent.BodyPart.ToString(),
                Damage = damageEvent.Damage,
                ArmorDamage = damageEvent.ArmorDamage,
                Distance = damageEvent.Target != null && LocalPlayerTracker.LocalPlayer != null
                    ? Vector3.Distance(LocalPlayerTracker.LocalPlayer.Position, damageEvent.Target.Position)
                    : 0f
            };
        }
    }

    // 修改后的 Harmony 补丁
    [HarmonyPatch(typeof(Player), "ReceiveDamage")]
    class ReceiveDamagePatch
    {
        static void Postfix(Player __instance, float damage, EBodyPart part, EDamageType type, float absorbed, MaterialType special)
        {
            try
            {
                HitMarkerPlugin.DebugInfo.ReceiveDamageCount++;

                if (damage > 0f)
                {
                    DamageEventCollector.AddBodyHit(__instance, damage, part);
                    HitMarkerPlugin.Logger.LogInfo($"命中: 对 {__instance.Profile.Nickname} 造成 {damage} 身体伤害, 部位: {part}");
                }
                else
                {
                    HitMarkerPlugin.Logger.LogInfo($"零伤害命中: 对 {__instance.Profile.Nickname}, 部位: {part}");
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"ReceiveDamage错误: {e}");
                HitMarkerPlugin.DebugInfo.LastError = $"ReceiveDamage: {e.Message}";
            }
        }
    }

    [HarmonyPatch(typeof(ArmorComponent), "ApplyDamage")]
    class ApplyArmorDamageInfoPatch
    {
        static void Postfix(ref ArmorComponent __instance, ref DamageInfoStruct damageInfo, EBodyPartColliderType colliderType,
            EArmorPlateCollider armorPlateCollider, bool damageInfoIsLocal, List<ArmorComponent> armorComponents,
            SkillManager.SkillBuffClass lightVestsDamageReduction, SkillManager.SkillBuffClass heavyVestsDamageReduction, ref float __result)
        {
            try
            {
                HitMarkerPlugin.DebugInfo.ArmorDamageCount++;

                // 保留原有的判断逻辑：伤害大于0且在护甲组件列表中，并且伤害来源是本地玩家
                if (__result > 0 && armorComponents.Contains(__instance))
                {
                    bool isBroken = __instance.Repairable.Durability - __result <= 0;

                    // 使用原有的伤害来源判断逻辑
                    var localPlayer = LocalPlayerTracker.LocalPlayer;
                    if (localPlayer == null)
                    {
                        HitMarkerPlugin.DebugInfo.LocalPlayerNullCount++;
                        return;
                    }

                    // 关键修复：添加 damageInfoIsLocal 检查
                    // damageInfoIsLocal == true 表示本地玩家的护甲被击中，不应触发效果
                    if (!damageInfoIsLocal && damageInfo.Player?.iPlayer?.IsYourPlayer == true)
                    {
                        HitMarkerPlugin.DebugInfo.ValidHitCount++;

                        DamageEventCollector.AddArmorHit(damageInfo, __result, isBroken, __instance);

                        HitMarkerPlugin.DebugInfo.LastArmorHit = new HitMarkerPlugin.DebugInfo.ArmorData
                        {
                            Timestamp = DateTime.Now,
                            ArmorDamage = __result,
                            RemainingDurability = __instance.Repairable.Durability,
                            IsBroken = isBroken
                        };

                        HitMarkerPlugin.Logger.LogInfo($"护甲命中: 造成 {__result} 护甲伤害, 剩余耐久: {__instance.Repairable.Durability}");
                    }
                    else
                    {
                        HitMarkerPlugin.DebugInfo.InvalidHitCount++;
                    }
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"ApplyArmorDamageInfoPatch错误: {e}");
                HitMarkerPlugin.DebugInfo.LastError = $"ApplyArmorDamageInfoPatch: {e.Message}";
            }
        }
    }

    // 其他补丁保持不变...
    [HarmonyPatch(typeof(Player), "OnBeenKilledByAggressor")]
    class OnBeenKilledPatch
    {
        static void Postfix(Player __instance, EFT.IPlayer aggressor)
        {
            try
            {
                HitMarkerPlugin.DebugInfo.KillEventCount++;

                if (IsKillByLocalPlayer(aggressor))
                {
                    var localPlayer = LocalPlayerTracker.LocalPlayer;
                    bool isHeadshot = HitMarkerPlugin.DebugInfo.LastHitInfo?.BodyPart == EBodyPart.Head.ToString();
                    EBodyPart bodyPart = EBodyPart.Chest;
                    string weaponName = "未知";

                    // 尝试从 LastDamageInfo 获取武器和部位信息
                    try
                    {
                        var lastDamageInfo = Traverse.Create(__instance).Field("LastDamageInfo").GetValue<DamageInfoStruct>();
                        if (lastDamageInfo.Weapon != null)
                        {
                            weaponName = lastDamageInfo.Weapon.ShortName?.Localized()
                                ?? lastDamageInfo.Weapon.Name?.Localized()
                                ?? "未知";
                        }

                        var lastBodyPart = Traverse.Create(__instance).Field("LastBodyPart").GetValue<EBodyPart>();
                        bodyPart = lastBodyPart;
                        isHeadshot = bodyPart == EBodyPart.Head;
                    }
                    catch { }

                    float distance = localPlayer != null
                        ? Vector3.Distance(localPlayer.Position, __instance.Position)
                        : 0f;

                    HitMarkerPlugin.DebugInfo.LastKillInfo = new HitMarkerPlugin.DebugInfo.KillData
                    {
                        Timestamp = DateTime.Now,
                        VictimName = __instance.Profile.Nickname,
                        VictimSide = __instance.Side.ToString(),
                        Distance = distance,
                        WeaponName = weaponName,
                        BodyPart = bodyPart,
                        IsHeadshot = isHeadshot
                    };

                    HitMarkerPlugin.Logger.LogInfo($"击杀确认: 本地玩家击杀了 {__instance.Profile.Nickname}, 爆头: {isHeadshot}, 武器: {weaponName}");

                    HitMarkerPlugin.Instance?.OnKill(isHeadshot, HitMarkerPlugin.DebugInfo.LastKillInfo);
                    HitMarkerPlugin.DebugInfo.TotalKills++;
                }
                else
                {
                    HitMarkerPlugin.DebugInfo.LastNonPlayerKill = $"{aggressor?.Profile?.Nickname} 击杀了 {__instance.Profile.Nickname}";
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"OnBeenKilledPatch错误: {e}");
                HitMarkerPlugin.DebugInfo.LastError = $"OnBeenKilledPatch: {e.Message}";
            }
        }

        private static bool IsKillByLocalPlayer(EFT.IPlayer aggressor)
        {
            var localPlayer = LocalPlayerTracker.LocalPlayer;
            if (localPlayer == null)
            {
                HitMarkerPlugin.DebugInfo.LocalPlayerNullCount++;
                return false;
            }
            return aggressor != null && aggressor.ProfileId == localPlayer.ProfileId;
        }
    }

    [HarmonyPatch(typeof(Player), "Init")]
    class PlayerInitPatchDoSomething
    {
        static void Postfix(Player __instance)
        {
            try
            {
                if (__instance != null && __instance.IsYourPlayer)
                {
                    LocalPlayerTracker.LocalPlayer = __instance;
                    HitMarkerPlugin.Logger.LogInfo($"本地玩家已设置, ID = {__instance.ProfileId}");

                    // 确保伤害事件处理器存在
                    if (HitMarkerPlugin.Instance != null && HitMarkerPlugin.Instance.GetComponent<DamageEventProcessor>() == null)
                    {
                        HitMarkerPlugin.Logger.LogInfo("创建DamageEventProcessor");
                        HitMarkerPlugin.Instance.gameObject.AddComponent<DamageEventProcessor>();
                    }

                    // 重新初始化UI组件并同步引用
                    var uiManager = HitMarkerPlugin.Instance?.GetComponent<HitMarkerUI>();
                    if (uiManager == null)
                    {
                        HitMarkerPlugin.Logger.LogInfo("重新创建HitMarkerUI");
                        uiManager = HitMarkerPlugin.Instance?.gameObject.AddComponent<HitMarkerUI>();
                    }

                    // 同步 UIManager 引用
                    if (uiManager != null && HitMarkerPlugin.Instance != null)
                    {
                        HitMarkerPlugin.Instance.UIManager = uiManager;
                        HitMarkerPlugin.Logger.LogInfo($"PlayerInit: UIManager 已同步, enabled={uiManager.enabled}");
                    }
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"PlayerInitPatch错误: {e}");
            }
        }
    }

    // 其他原有补丁保持不变...
    [HarmonyPatch(typeof(SSAA), "Awake")]
    public class SSAAPatch
    {
        [HarmonyPrefix]
        private static void PatchPostFix(ref SSAA __instance)
        {
            HitMarkerPlugin.FPSCameraSSAA = __instance;
        }
    }

    [HarmonyPatch(typeof(Player), "OnDestroy")]
    class PlayerDestroyPatch
    {
        static void Prefix(Player __instance)
        {
            try
            {
                if (__instance != null && __instance.IsYourPlayer)
                {
                    HitMarkerPlugin.Logger.LogInfo("本地玩家被销毁");
                    LocalPlayerTracker.LocalPlayer = null;
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"PlayerDestroyPatch错误: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(GameWorld), "OnGameStarted")]
    class GameWorldStartPatch
    {
        static void Postfix()
        {
            try
            {
                HitMarkerPlugin.Logger.LogInfo("战局开始，检查资源状态");

                // 检查纹理资源是否有效
                if (HitMarkerPlugin.HitMarkerTexture == null)
                {
                    HitMarkerPlugin.Logger.LogWarning("HitMarkerTexture 为 null，重新加载资源");
                    HitMarkerPlugin.Instance?.ReloadResources();
                }
                else
                {
                    HitMarkerPlugin.Logger.LogInfo($"HitMarkerTexture 有效: {HitMarkerPlugin.HitMarkerTexture.width}x{HitMarkerPlugin.HitMarkerTexture.height}");
                }

                // 检查 UI 组件并同步引用
                var uiManager = HitMarkerPlugin.Instance?.GetComponent<HitMarkerUI>();
                if (uiManager == null)
                {
                    HitMarkerPlugin.Logger.LogWarning("HitMarkerUI 组件不存在，重新创建");
                    uiManager = HitMarkerPlugin.Instance?.gameObject.AddComponent<HitMarkerUI>();
                }

                // 同步 UIManager 引用
                if (uiManager != null && HitMarkerPlugin.Instance != null)
                {
                    HitMarkerPlugin.Instance.UIManager = uiManager;
                    HitMarkerPlugin.Logger.LogInfo($"UIManager 已同步, enabled={uiManager.enabled}");
                }
                else
                {
                    HitMarkerPlugin.Logger.LogError("UIManager 同步失败");
                }

                // 重新加载音频
                if (HitMarkerPlugin.Instance != null)
                {
                    HitMarkerPlugin.Instance.Invoke("ReloadAudioInRaid", 3f);
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"战局开始处理失败: {e}");
            }
        }
    }

    public static class LocalPlayerTracker
    {
        public static Player LocalPlayer { get; set; }
    }
}