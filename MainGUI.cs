using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mimic;
using Mimic.Actors;
using ReluProtocol;
using shadcnui.GUIComponents.Controls;
using shadcnui.GUIComponents.Core;
using shadcnui.GUIComponents.Data;
using shadcnui.GUIComponents.Display;
using shadcnui.GUIComponents.Layout;
using UnityEngine;
using Input = UnityEngine.Input;

public class MainGUI : MonoBehaviour
{
    private GUIHelper guiHelper;
    private Rect windowRect = new Rect(20, 20, 900, 750);
    private bool showDemoWindow = true;
    private Vector2 scrollPosition;
    private int currentDemoTab = 0;
    private Tabs.TabConfig[] demoTabs;
    private static List<LootingLevelObject> pickupQueue = new List<LootingLevelObject>();
    private static float pickupCooldown = 0f;
    private static bool isPickingUp = false;
    private Vector2 playerScrollPosition;

    public static bool godModeEnabled = false;
    public static bool infiniteStaminaEnabled = false;
    public static bool noFallDamageEnabled = false;
    public static bool espEnabled = false;
    public static Color espColor = Color.yellow;
    public static float espDistance = 100f;

    void Start()
    {
        guiHelper = new GUIHelper();
        demoTabs = new Tabs.TabConfig[] { new Tabs.TabConfig("Local Player", DrawLocalPlayerTab), new Tabs.TabConfig("ESP", DrawESPTab), new Tabs.TabConfig("Inventory", DrawInventoryTab) };

        ESPMain.Initialize();
        ApplyHarmonyPatches();
    }

    void Update()
    {
        if (isPickingUp && pickupQueue.Count > 0)
        {
            pickupCooldown -= Time.deltaTime;
            if (pickupCooldown <= 0)
            {
                ProcessNextPickup();
            }
        }
    }

    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 150, 30), "Open Mod Menu"))
        {
            showDemoWindow = !showDemoWindow;
        }

        if (showDemoWindow)
        {
            windowRect = GUI.Window(101, windowRect, (GUI.WindowFunction)DrawDemoWindow, "MIMESIS_Mod_Menu Menu");
        }

        if (espEnabled)
        {
            ESPMain.UpdateESP();
        }
    }

    void OnDestroy()
    {
        ESPMain.Cleanup();
    }

    void DrawDemoWindow(int windowID)
    {
        guiHelper.UpdateAnimations(showDemoWindow);
        if (guiHelper.BeginAnimatedGUI())
        {
            currentDemoTab = guiHelper.VerticalTabs(
                demoTabs.Select(tab => tab.Name).ToArray(),
                currentDemoTab,
                () =>
                {
                    scrollPosition = guiHelper.DrawScrollView(scrollPosition, DrawCurrentTabContent, GUILayout.Height(650));
                },
                maxLines: 1
            );

            guiHelper.EndAnimatedGUI();
        }
        GUI.DragWindow();
    }

    void DrawCurrentTabContent()
    {
        guiHelper.BeginVerticalGroup();
        if (currentDemoTab >= 0 && currentDemoTab < demoTabs.Length)
        {
            demoTabs[currentDemoTab].Content?.Invoke();
        }
        guiHelper.EndVerticalGroup();
    }

    void DrawLocalPlayerTab()
    {
        guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

        guiHelper.Label("Protection", LabelVariant.Default);
        godModeEnabled = guiHelper.Switch("God Mode", godModeEnabled);
        noFallDamageEnabled = guiHelper.Switch("No Fall Damage", noFallDamageEnabled);
        guiHelper.HorizontalSeparator();

        guiHelper.Label("Recovery", LabelVariant.Default);
        infiniteStaminaEnabled = guiHelper.Switch("Infinite Stamina", infiniteStaminaEnabled);
        guiHelper.HorizontalSeparator();

        guiHelper.Label("Utilities", LabelVariant.Default);
        if (guiHelper.Button("Max Durability", ButtonVariant.Default, ButtonSize.Small))
        {
            MaxItemDurability(GameAPI.GetLocalPlayer());
        }
        guiHelper.MutedLabel("Restore all equipment durability to max");

        guiHelper.EndVerticalGroup();
    }

    void DrawESPTab()
    {
        guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

        guiHelper.Label("Loot ESP Settings", LabelVariant.Default);
        guiHelper.MutedLabel("Visualize nearby loot and items");
        guiHelper.HorizontalSeparator();

        espEnabled = guiHelper.Switch("Loot ESP", espEnabled);
        guiHelper.MutedLabel("Shows all nearby loot items on screen");

        guiHelper.HorizontalSeparator();
        guiHelper.Label("ESP Distance: " + espDistance.ToString("F0") + "m", LabelVariant.Default);

        guiHelper.BeginHorizontalGroup();
        guiHelper.Label("50m");
        espDistance = GUILayout.HorizontalSlider(espDistance, 50f, 500f, GUILayout.ExpandWidth(true));
        guiHelper.Label("500m");
        guiHelper.EndHorizontalGroup();

        guiHelper.HorizontalSeparator();
        guiHelper.Label("ESP Color", LabelVariant.Default);

        guiHelper.BeginHorizontalGroup();
        if (guiHelper.Button("Yellow", espColor == Color.yellow ? ButtonVariant.Secondary : ButtonVariant.Outline, ButtonSize.Small))
        {
            espColor = Color.yellow;
            ESPMain.UpdateColors();
        }
        if (guiHelper.Button("Green", espColor == Color.green ? ButtonVariant.Secondary : ButtonVariant.Outline, ButtonSize.Small))
        {
            espColor = Color.green;
            ESPMain.UpdateColors();
        }
        if (guiHelper.Button("Cyan", espColor == Color.cyan ? ButtonVariant.Secondary : ButtonVariant.Outline, ButtonSize.Small))
        {
            espColor = Color.cyan;
            ESPMain.UpdateColors();
        }
        if (guiHelper.Button("Magenta", espColor == Color.magenta ? ButtonVariant.Secondary : ButtonVariant.Outline, ButtonSize.Small))
        {
            espColor = Color.magenta;
            ESPMain.UpdateColors();
        }
        guiHelper.EndHorizontalGroup();

        guiHelper.EndVerticalGroup();
    }

    void DrawInventoryTab()
    {
        guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

        guiHelper.Label("Item Management", LabelVariant.Default);
        guiHelper.MutedLabel("Manage and organize inventory items");
        guiHelper.HorizontalSeparator();

        if (guiHelper.Button(isPickingUp ? "Stop Pickup" : "Pickup All Items", isPickingUp ? ButtonVariant.Destructive : ButtonVariant.Default, ButtonSize.Small))
        {
            if (isPickingUp)
            {
                StopPickup();
            }
            else
            {
                StartPickupAllItems();
            }
        }
        guiHelper.MutedLabel("Automatically picks up all nearby loot");

        guiHelper.EndVerticalGroup();
    }

    void StartPickupAllItems()
    {
        try
        {
            pickupQueue.Clear();
            LootingLevelObject[] allLoot = GameAPI.GetAllLoot();

            if (allLoot.Length > 0)
            {
                pickupQueue.AddRange(allLoot);
                isPickingUp = true;
                pickupCooldown = 0.05f;
                Debug.Log($"Starting to pickup {pickupQueue.Count} items");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"StartPickupAllItems error: {ex.Message}");
        }
    }

    void ProcessNextPickup()
    {
        if (pickupQueue.Count == 0)
        {
            isPickingUp = false;
            Debug.Log("Pickup complete");
            return;
        }

        try
        {
            ProtoActor player = GameAPI.GetLocalPlayer();
            if (player == null)
            {
                StopPickup();
                return;
            }

            LootingLevelObject loot = pickupQueue[0];
            if (loot != null && loot.gameObject.activeInHierarchy)
            {
                player.GrapLootingObject(loot.ActorID);
            }

            pickupQueue.RemoveAt(0);
            pickupCooldown = 0.05f;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error picking up item: {ex.Message}");
            if (pickupQueue.Count > 0)
                pickupQueue.RemoveAt(0);
            pickupCooldown = 0.05f;
        }
    }

    void StopPickup()
    {
        isPickingUp = false;
        pickupQueue.Clear();
        Debug.Log("Pickup stopped");
    }

    void MaxItemDurability(ProtoActor player)
    {
        if (player == null)
            return;
        try
        {
            List<InventoryItem> items = GameAPI.GetInventoryItems(player);

            if (items.Count > 0)
            {
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        ModHelper.SetFieldValue(item, "Durability", 999999);
                        ModHelper.SetFieldValue(item, "StackCount", 999999);
                    }
                }
                Debug.Log($"Maxed durability for {player.gameObject.name}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"MaxItemDurability error: {ex.Message}");
        }
    }

    void ApplyHarmonyPatches()
    {
        HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("com.mod.patches");

        var originalOnDamaged = AccessTools.Method(typeof(StatManager), "OnDamaged");
        if (originalOnDamaged != null)
        {
            var prefixOnDamaged = new HarmonyMethod(typeof(MainGUI), nameof(PrefixOnDamaged));
            harmony.Patch(originalOnDamaged, prefixOnDamaged);
        }

        var originalConsumeStamina = AccessTools.Method(typeof(StatManager), "ConsumeStamina");
        if (originalConsumeStamina != null)
        {
            var prefixConsumeStamina = new HarmonyMethod(typeof(MainGUI), nameof(PrefixConsumeStamina));
            harmony.Patch(originalConsumeStamina, prefixConsumeStamina);
        }

        var originalCheckFallDamage = AccessTools.Method(typeof(MovementController), "CheckFallDamage");
        if (originalCheckFallDamage != null)
        {
            var prefixCheckFallDamage = new HarmonyMethod(typeof(MainGUI), nameof(PrefixCheckFallDamage));
            harmony.Patch(originalCheckFallDamage, prefixCheckFallDamage);
        }
    }

    static bool PrefixOnDamaged(object __instance, object args)
    {
        if (godModeEnabled)
        {
            object victim = ModHelper.GetFieldValue(args, "Victim");
            if (victim is VPlayer)
            {
                return false;
            }
        }

        return true;
    }

    static bool PrefixConsumeStamina(long amount)
    {
        if (infiniteStaminaEnabled)
        {
            return false;
        }
        return true;
    }

    static bool PrefixCheckFallDamage(ref float __result)
    {
        if (noFallDamageEnabled)
        {
            __result = 0f;
            return false;
        }
        return true;
    }
}
