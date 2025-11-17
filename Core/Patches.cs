using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bifrost.Cooked;
using HarmonyLib;
using MelonLoader;
using Mimesis_Mod_Menu.Core.Config;
using Mimic;
using Mimic.Actors;
using MimicAPI.GameAPI;
using ReluProtocol;
using ReluProtocol.Enum;
using UnityEngine;
using static Mimic.Actors.ProtoActor;

namespace Mimesis_Mod_Menu.Core
{
    public static class Patches
    {
        private static MainGUI.FeatureState featureState;
        private static string itemLogPath = "";
        private static HashSet<int> loggedItems = new HashSet<int>();

        public static MainGUI.FeatureState GetFeatureState()
        {
            if (featureState == null)
            {
                var mainGUI = UnityEngine.Object.FindObjectOfType<MainGUI>();
                if (mainGUI != null)
                    featureState = mainGUI.GetFeatureState();
            }
            return featureState;
        }

        public static void ApplyPatches(ConfigManager config)
        {
            try
            {
                itemLogPath = Path.Combine(Directory.GetCurrentDirectory(), "ItemMasterIDLog.txt");

                if (!File.Exists(itemLogPath))
                {
                    File.WriteAllText(itemLogPath, "ItemID,ItemName\n");
                }

                var mainGUI = UnityEngine.Object.FindObjectOfType<MainGUI>();
                if (mainGUI != null)
                    featureState = mainGUI.GetFeatureState();

                HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("com.Mimesis.modmenu");
                harmony.PatchAll(typeof(Patches).Assembly);
                MelonLogger.Msg("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error applying patches: {ex.Message}");
            }
        }

        public static void LogItemMasterID(int itemMasterID, ItemMasterInfo masterInfo)
        {
            try
            {
                if (loggedItems.Contains(itemMasterID))
                    return;

                if (masterInfo == null)
                    return;

                loggedItems.Add(itemMasterID);
                string itemName = masterInfo.Name;
                string line = $"{itemMasterID},{itemName}";
                File.AppendAllText(itemLogPath, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error logging item: {ex.Message}");
            }
        }

        private static readonly BindingFlags AllFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;
        private static readonly string[] NumericFieldPatterns = new[] { "<{0}>k__BackingField", "{0}", "_{0}", "m_{0}" };

        public static void SetIntField(object instance, string fieldName, int value)
        {
            try
            {
                if (instance == null || string.IsNullOrEmpty(fieldName))
                    return;

                Type type = instance.GetType();

                foreach (string pattern in NumericFieldPatterns)
                {
                    string actualFieldName = string.Format(pattern, fieldName);
                    FieldInfo field = type.GetField(actualFieldName, AllFieldFlags);

                    if (field != null && IsNumericType(field.FieldType))
                    {
                        try
                        {
                            field.SetValue(instance, Convert.ChangeType(value, field.FieldType));
                            return;
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"Failed to set field {fieldName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in SetIntField: {ex.Message}");
            }
        }

        private static bool IsNumericType(Type type)
        {
            try
            {
                return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in IsNumericType: {ex.Message}");
                return false;
            }
        }
    }

    #region Item Spawner Patches

    public static class ItemSpawnerPatches
    {
        public static int pendingItemMasterID = 0;
        public static int pendingItemQuantity = 0;

        public static void SetItemToSpawn(int itemMasterID, int quantity)
        {
            pendingItemMasterID = itemMasterID;
            pendingItemQuantity = quantity;
        }
    }

    [HarmonyPatch]
    internal static class InventoryControllerHandleChangeActiveInvenSlotPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(InventoryController), nameof(InventoryController.HandleChangeActiveInvenSlot), new Type[] { typeof(int), typeof(bool), typeof(int) });
        }

        private static void Prefix(InventoryController __instance, int slotIndex)
        {
            try
            {
                if (ItemSpawnerPatches.pendingItemMasterID == 0)
                    return;

                int itemMasterID = ItemSpawnerPatches.pendingItemMasterID;
                int quantity = ItemSpawnerPatches.pendingItemQuantity;

                ItemMasterInfo masterInfo = Inventory.GetItemMasterInfo(itemMasterID);
                if (masterInfo == null)
                {
                    ItemSpawnerPatches.pendingItemMasterID = 0;
                    return;
                }

                VCreature self = ReflectionHelper.GetFieldValue(__instance, "_self") as VCreature;
                if (self == null || self.VRoom == null)
                    return;

                ItemElement newItemElement = self.VRoom.GetNewItemElement(itemMasterID, false, quantity, 0, 0, 0);
                if (newItemElement == null)
                {
                    ItemSpawnerPatches.pendingItemMasterID = 0;
                    return;
                }

                __instance.AddInvenItem(slotIndex, newItemElement, false);

                ItemSpawnerPatches.pendingItemMasterID = 0;
                ItemSpawnerPatches.pendingItemQuantity = 0;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"InventoryControllerHandleChangeActiveInvenSlotPatch error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Item Patches

    [HarmonyPatch(typeof(ItemInfo), MethodType.Constructor)]
    internal static class ItemInfoConstructorPatch
    {
        private static void Postfix(ItemInfo __instance)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null)
                    return;

                if (fs.InfiniteDurability)
                    Patches.SetIntField(__instance, "durability", int.MaxValue);
                if (fs.InfinitePrice)
                    Patches.SetIntField(__instance, "price", int.MaxValue);
                if (fs.InfiniteGauge)
                    Patches.SetIntField(__instance, "remainGauge", int.MaxValue);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"ItemInfoConstructorPatch error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(InventoryItem), nameof(InventoryItem.UpdateInfo))]
    internal static class InventoryItemUpdateInfoPatch
    {
        private static void Postfix(InventoryItem __instance)
        {
            try
            {
                if (__instance.ItemMasterID > 0)
                {
                    ItemMasterInfo masterInfo = Inventory.GetItemMasterInfo(__instance.ItemMasterID);
                    if (masterInfo != null)
                    {
                        Patches.LogItemMasterID(__instance.ItemMasterID, masterInfo);
                    }
                }

                var fs = Patches.GetFeatureState();
                if (fs == null)
                    return;

                if (fs.InfiniteDurability)
                    Patches.SetIntField(__instance, "Durability", int.MaxValue);
                if (fs.InfinitePrice)
                    Patches.SetIntField(__instance, "Price", int.MaxValue);
                if (fs.InfiniteGauge)
                    Patches.SetIntField(__instance, "RemainGauge", int.MaxValue);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"InventoryItemUpdateInfoPatch error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EquipmentItemElement), MethodType.Constructor, new[] { typeof(int), typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int) })]
    internal static class EquipmentItemElementConstructorPatch
    {
        private static void Postfix(EquipmentItemElement __instance)
        {
            try
            {
                if (__instance.ItemMasterID > 0)
                {
                    ItemMasterInfo masterInfo = Inventory.GetItemMasterInfo(__instance.ItemMasterID);
                    if (masterInfo != null)
                    {
                        Patches.LogItemMasterID(__instance.ItemMasterID, masterInfo);
                    }
                }

                var fs = Patches.GetFeatureState();
                if (fs == null)
                    return;

                if (fs.InfiniteDurability)
                {
                    __instance.SetDurability(int.MaxValue);
                }
                if (fs.InfiniteGauge)
                {
                    __instance.SetAmount(int.MaxValue);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"EquipmentItemElementConstructorPatch error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Player Stat Patches

    [HarmonyPatch(typeof(StatManager), "OnDamaged")]
    internal static class StatManagerOnDamagedPatch
    {
        private static bool Prefix(object __instance, object args)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.GodMode)
                    return true;

                object victim = ReflectionHelper.GetFieldValue(args, "Victim");
                if (victim is VPlayer vplayer)
                {
                    ProtoActor localPlayer = PlayerAPI.GetLocalPlayer();
                    if (localPlayer != null && localPlayer.ActorID == vplayer.ObjectID)
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"StatManagerOnDamagedPatch error: {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(StatManager), "ConsumeStamina")]
    internal static class StatManagerConsumeStaminaPatch
    {
        private static bool Prefix()
        {
            try
            {
                var fs = Patches.GetFeatureState();
                return fs == null || !fs.InfiniteStamina;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"StatManagerConsumeStaminaPatch error: {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(MovementController), "CheckFallDamage")]
    internal static class MovementControllerCheckFallDamagePatch
    {
        private static bool Prefix(ref float __result)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.NoFallDamage)
                    return true;

                __result = 0f;
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"MovementControllerCheckFallDamagePatch error: {ex.Message}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ProtoActor), "CaculateSpeed")]
    internal static class ProtoActorCaculateSpeedPatch
    {
        private static void Postfix(ref float __result)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs != null && fs.SpeedBoost)
                    __result *= fs.SpeedMultiplier;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"ProtoActorCaculateSpeedPatch error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Maintenance Room Patches

    [HarmonyPatch(typeof(VPlayer), nameof(VPlayer.HandleBuyItem))]
    internal static class VPlayerHandleBuyItemPatch
    {
        private static bool Prefix(VPlayer __instance, int itemMasterID, int hashCode, int machineIndex, ref MsgErrorCode __result)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.ForceBuy)
                    return true;

                MaintenanceRoom maintenanceRoom = __instance.VRoom as MaintenanceRoom;
                if (maintenanceRoom == null)
                {
                    __result = MsgErrorCode.InvalidRoomType;
                    return false;
                }

                ItemElement itemElement = maintenanceRoom.GetNewItemElement(itemMasterID, false, 1, 0, 0);
                if (itemElement == null)
                {
                    __result = MsgErrorCode.ItemNotFound;
                    return false;
                }

                __instance.InventoryControlUnit.HandleAddItem(itemElement, out _, true, true);
                __instance.SendToMe(new BuyItemRes(hashCode) { remainCurrency = maintenanceRoom.Currency });
                __instance.SendInSight(new BuyItemSig { itemMasterID = itemMasterID, machineIndex = machineIndex }, false);

                __result = MsgErrorCode.Success;
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Force buy patch error: {ex.Message}");
                __result = MsgErrorCode.InvalidErrorCode;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(VPlayer), nameof(VPlayer.HandleRepairTrain))]
    internal static class VPlayerHandleRepairTrainPatch
    {
        private static bool Prefix(VPlayer __instance, int hashCode, ref MsgErrorCode __result)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.ForceRepair)
                    return true;

                MaintenanceRoom maintenanceRoom = __instance.VRoom as MaintenanceRoom;
                if (maintenanceRoom == null)
                {
                    __result = MsgErrorCode.InvalidRoomType;
                    return false;
                }

                __instance.SendToMe(new RepairTramRes(hashCode) { errorCode = MsgErrorCode.Success });
                __instance.SendToChannel(new StartRepairTramSig { remainCurrency = maintenanceRoom.Currency });

                __result = MsgErrorCode.Success;
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Force repair patch error: {ex.Message}");
                __result = MsgErrorCode.InvalidErrorCode;
                return false;
            }
        }
    }

    [HarmonyPatch]
    internal static class MaintenanceRoomBuyItemCurrencyPatch
    {
        private static MethodBase TargetMethod()
        {
            try
            {
                return AccessTools.Method(typeof(MaintenanceRoom), "BuyItem", new Type[] { typeof(int), typeof(VCreature) });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"MaintenanceRoomBuyItemCurrencyPatch TargetMethod error: {ex.Message}");
                return null;
            }
        }

        private static void Postfix(MaintenanceRoom __instance, int itemMasterID, VCreature creature, ref MsgErrorCode __result)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.InfiniteCurrency)
                    return;

                if (__result == MsgErrorCode.Success)
                {
                    ReflectionHelper.InvokeMethod(__instance, "AddCurrency", int.MaxValue);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"MaintenanceRoomBuyItemCurrencyPatch error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Currency Patches

    [HarmonyPatch]
    internal static class GameMainBaseUpdateCurrencyPatch
    {
        private static MethodBase TargetMethod()
        {
            try
            {
                return AccessTools.Method(typeof(GameMainBase), "UpdateCurrency", new Type[] { typeof(int) });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"GameMainBaseUpdateCurrencyPatch TargetMethod error: {ex.Message}");
                return null;
            }
        }

        private static void Prefix(GameMainBase __instance, ref int currentCurrency)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.InfiniteCurrency)
                    return;

                currentCurrency = int.MaxValue;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"GameMainBaseUpdateCurrencyPatch error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    internal static class GameMainBaseOnCurrencyChangedPatch
    {
        private static MethodBase TargetMethod()
        {
            try
            {
                return AccessTools.Method(typeof(GameMainBase), "OnCurrencyChanged", new Type[] { typeof(int), typeof(int) });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"GameMainBaseOnCurrencyChangedPatch TargetMethod error: {ex.Message}");
                return null;
            }
        }

        private static void Prefix(GameMainBase __instance, ref int prev, ref int curr)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.InfiniteCurrency)
                    return;

                curr = int.MaxValue;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"GameMainBaseOnCurrencyChangedPatch error: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    internal static class MaintenanceSceneOnCurrencyChangedPatch
    {
        private static MethodBase TargetMethod()
        {
            try
            {
                return AccessTools.Method(typeof(MaintenanceScene), "OnCurrencyChanged", new Type[] { typeof(int), typeof(int) });
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"MaintenanceSceneOnCurrencyChangedPatch TargetMethod error: {ex.Message}");
                return null;
            }
        }

        private static void Prefix(MaintenanceScene __instance, ref int prev, ref int curr)
        {
            try
            {
                var fs = Patches.GetFeatureState();
                if (fs == null || !fs.InfiniteCurrency)
                    return;

                curr = int.MaxValue;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"MaintenanceSceneOnCurrencyChangedPatch error: {ex.Message}");
            }
        }
    }

    #endregion
}
