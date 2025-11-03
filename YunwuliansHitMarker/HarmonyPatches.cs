using EFT;
using EFT.Ballistics;
using EFT.InventoryLogic;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HitMarkerPlugin
{
    // 监控伤害应用
    [HarmonyPatch(typeof(Player), "ReceiveDamage")]
    class ReceiveDamagePatch
    {
        static void Postfix(Player __instance, float damage, EBodyPart part, EDamageType type, float absorbed, MaterialType special)
        {
            try
            {
                HitMarkerPlugin.DebugInfo.ReceiveDamageCount++;
                HitMarkerPlugin.DebugInfo.LastHitInfo = new HitMarkerPlugin.DebugInfo.HitData
                {
                    Timestamp = DateTime.Now,
                    TargetName = __instance.Profile.Nickname,
                    BodyPart = part.ToString(),
                    Damage = damage,
                    ArmorDamage = 0,
                    Distance = Vector3.Distance(LocalPlayerTracker.LocalPlayer.Position, __instance.Position)
                };
                if (IsDamageFromLocalPlayer(__instance))
                {
                    // 检查是否造成了实际伤害
                    if (damage > 0f)
                    {
                        HitMarkerPlugin.Logger.LogInfo($"命中: 对 {__instance.Profile.Nickname} 造成 {damage} 身体伤害, 部位: {part}");

                        // 根据命中部位决定效果
                        if (part == EBodyPart.Head)
                        {
                            HitMarkerPlugin.Instance?.PlayHeadshotEffect();
                        }
                        else
                        {
                            HitMarkerPlugin.Instance?.PlayHitEffect();
                        }
                    }
                    else
                    {
                        HitMarkerPlugin.Logger.LogInfo($"零伤害命中: 对 {__instance.Profile.Nickname}, 部位: {part}");
                    }
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"ApplyDamageInfoPatch错误: {e}");
                HitMarkerPlugin.DebugInfo.LastError = $"ApplyDamageInfoPatch: {e.Message}";
            }
        }

        private static bool IsDamageFromLocalPlayer(Player victim)
        {
            var localPlayer = LocalPlayerTracker.LocalPlayer;
            if (localPlayer == null)
            {
                HitMarkerPlugin.DebugInfo.LocalPlayerNullCount++;
                return false;
            }
            var lastDamageInfo = Traverse.Create(victim).Field("LastDamageInfo").GetValue<DamageInfoStruct>();
            if (localPlayer != victim&& lastDamageInfo.Player.iPlayer.IsYourPlayer)
            {
                HitMarkerPlugin.DebugInfo.ValidHitCount++;
                return true;
            }
            HitMarkerPlugin.DebugInfo.InvalidHitCount++;
            return false;
        }
    }


    // 护甲伤害补丁（保留用于护甲击穿检测）
    [HarmonyPatch(typeof(ArmorComponent), "ApplyDurabilityDamage")]
    class ApplyArmorDamageInfoPatch
    {
        static void Postfix(ref ArmorComponent __instance, float armorDamage, List<ArmorComponent> armorComponents)
        {
            try
            {
                HitMarkerPlugin.DebugInfo.ArmorDamageCount++;

                if (armorDamage > 0 && armorComponents.Contains(__instance))
                {
                    HitMarkerPlugin.DebugInfo.LastArmorHit = new HitMarkerPlugin.DebugInfo.ArmorData
                    {
                        Timestamp = DateTime.Now,
                        ArmorDamage = armorDamage,
                        RemainingDurability = __instance.Repairable.Durability,
                        IsBroken = __instance.Repairable.Durability - armorDamage <= 0
                    };

                    HitMarkerPlugin.Logger.LogInfo($"护甲命中: 造成 {armorDamage} 护甲伤害, 剩余耐久: {__instance.Repairable.Durability}");

                    // 检查护甲是否被击穿
                    if (__instance.Repairable.Durability - armorDamage <= 0)
                    {
                        HitMarkerPlugin.Instance?.PlayArmorBreakEffect();
                    }
                    else
                    {
                        HitMarkerPlugin.Instance?.PlayArmorHitEffect();
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

    // 击杀事件补丁
    [HarmonyPatch(typeof(Player), "OnBeenKilledByAggressor")]
    class OnBeenKilledPatch
    {
        static void Postfix(Player __instance, EFT.IPlayer aggressor)
        {
            try
            {
                HitMarkerPlugin.DebugInfo.KillEventCount++;

                // 检查击杀者是否是主玩家
                if (IsKillByLocalPlayer(aggressor))
                {
                    // 判断是否为爆头击杀
                    bool isHeadshot = HitMarkerPlugin.DebugInfo.LastHitInfo.BodyPart == EBodyPart.Head.ToString();

                    HitMarkerPlugin.DebugInfo.LastKillInfo = new HitMarkerPlugin.DebugInfo.KillData
                    {
                        Timestamp = DateTime.Now,
                        VictimName = __instance.Profile.Nickname,
                        VictimSide = __instance.Side.ToString(),
                        Distance = Vector3.Distance(LocalPlayerTracker.LocalPlayer.Position, __instance.Position)
                    };

                    HitMarkerPlugin.Logger.LogInfo($"击杀确认: 本地玩家击杀了 {__instance.Profile.Nickname}, 爆头: {isHeadshot}");

                    // 使用新的击杀处理系统
                    HitMarkerPlugin.Instance?.OnKill(isHeadshot);
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
    // 追踪本地玩家
    [HarmonyPatch(typeof(Player), "Init")]
    class PlayerInitPatch
    {
        static void Postfix(Player __instance)
        {
            try
            {
                if (__instance != null && __instance.IsYourPlayer)
                {
                    LocalPlayerTracker.LocalPlayer = __instance;
                    HitMarkerPlugin.Logger.LogInfo($"本地玩家已设置, ID =  {__instance.ProfileId}");

                    // 重新初始化UI组件，确保在正确的场景中
                    if (HitMarkerPlugin.Instance != null)
                    {
                        var uiManager = HitMarkerPlugin.Instance.GetComponent<HitMarkerUI>();
                        if (uiManager == null)
                        {
                            HitMarkerPlugin.Logger.LogInfo("重新创建HitMarkerUI");
                            HitMarkerPlugin.Instance.gameObject.AddComponent<HitMarkerUI>();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                HitMarkerPlugin.Logger.LogError($"PlayerInitPatch错误: {e}");
            }
        }
    }

    // 添加场景切换时的处理
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

    // 本地玩家追踪器
    public static class LocalPlayerTracker
    {
        public static Player LocalPlayer { get; set; }
    }
}