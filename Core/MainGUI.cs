using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Mimesis_Mod_Menu.Core.Config;
using Mimesis_Mod_Menu.Core.Features;
using Mimic.Actors;
using MimicAPI.GameAPI;
using ReluProtocol.Enum;
using shadcnui.GUIComponents.Core;
using shadcnui.GUIComponents.Layout;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mimesis_Mod_Menu.Core
{
    public class MainGUI : MonoBehaviour
    {
        #region Fields

        private GUIHelper? guiHelper;
        private Rect windowRect = new Rect(20, 20, 1000, 800);
        private bool showDemoWindow = true;
        private Vector2 scrollPosition;
        private int currentDemoTab;
        private Tabs.TabConfig[]? demoTabs;
        private float lastConfigSaveTime;
        private ConfigManager? configManager;

        private PickupManager? pickupManager;
        private MovementManager? movementManager;

        private AutoLootManager? autoLootManager;
        private FullbrightManager? fullbrightManager;
        private ItemSpawnerManager? itemSpawnerManager;

        private ProtoActor? selectedPlayer;

        public static bool godModeEnabled = false;
        public static bool infiniteStaminaEnabled = false;
        public static bool noFallDamageEnabled = false;

        public static bool speedBoostEnabled = false;
        public static float speedBoostMultiplier = 2f;

        public static bool espEnabled = false;
        public static bool espShowLoot = false;
        public static bool espShowPlayers = true;
        public static bool espShowMonsters = true;
        public static bool espShowInteractors = false;
        public static bool espShowNPCs = false;
        public static bool espShowFieldSkills = false;
        public static bool espShowProjectiles = false;
        public static bool espShowAuraSkills = false;
        public static Color espColor = Color.yellow;
        public static float espDistance = 150f;

        public static bool autoLootEnabled = false;
        public static float autoLootDistance = 50f;

        public static bool fullbright = false;

        private ProtoActor[]? cachedPlayers;
        private float lastPlayerCacheTime;
        private const float PLAYER_CACHE_INTERVAL = 5f;

        private string editingHotkey = "";
        private bool isListeningForHotkey = false;
        private KeyCode pendingKey = KeyCode.None;
        private bool pendingShift = false;
        private bool pendingCtrl = false;
        private bool pendingAlt = false;

        private string itemSpawnIDInput = "1001";
        private string itemSpawnQuantityInput = "1";

        #endregion

        #region Core

        void Start()
        {
            try
            {
                guiHelper = new GUIHelper();
                configManager = new ConfigManager();

                godModeEnabled = configManager.GetBool("godModeEnabled", false);
                infiniteStaminaEnabled = configManager.GetBool("infiniteStaminaEnabled", false);
                noFallDamageEnabled = configManager.GetBool("noFallDamageEnabled", false);
                speedBoostEnabled = configManager.GetBool("speedBoostEnabled", false);
                speedBoostMultiplier = configManager.GetFloat("speedBoostMultiplier", 2f);
                espEnabled = configManager.GetBool("espEnabled", false);
                espShowLoot = configManager.GetBool("espShowLoot", false);
                espShowPlayers = configManager.GetBool("espShowPlayers", true);
                espShowMonsters = configManager.GetBool("espShowMonsters", true);
                espShowInteractors = configManager.GetBool("espShowInteractors", false);
                espShowNPCs = configManager.GetBool("espShowNPCs", false);
                espShowFieldSkills = configManager.GetBool("espShowFieldSkills", false);
                espShowProjectiles = configManager.GetBool("espShowProjectiles", false);
                espShowAuraSkills = configManager.GetBool("espShowAuraSkills", false);
                espDistance = configManager.GetFloat("espDistance", 150f);
                autoLootEnabled = configManager.GetBool("autoLootEnabled", false);
                autoLootDistance = configManager.GetFloat("autoLootDistance", 50f);
                fullbright = configManager.GetBool("fullbright", false);

                demoTabs = new Tabs.TabConfig[]
                {
                    new Tabs.TabConfig("Player", DrawPlayerTab),
                    new Tabs.TabConfig("Combat", DrawCombatTab),
                    new Tabs.TabConfig("Loot", DrawLootTab),
                    new Tabs.TabConfig("Visual", DrawVisualTab),
                    new Tabs.TabConfig("Entities", DrawEntitiesTab),
                    new Tabs.TabConfig("Settings", DrawSettingsTab),
                };

                autoLootManager = new AutoLootManager();
                fullbrightManager = new FullbrightManager();
                pickupManager = new PickupManager();
                movementManager = new MovementManager();
                itemSpawnerManager = new ItemSpawnerManager();

                ESPManager.Initialize();
                Patches.ApplyPatches(configManager);

                MelonLogger.Msg("Mimesis Mod Menu initialized successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Start error: {ex.Message}");
            }
        }

        private float lastMenuToggleTime = 0f;
        private const float HOTKEY_COOLDOWN = 0.5f;

        void Update()
        {
            try
            {
                if (configManager == null)
                {
                    autoLootManager?.Update();
                    fullbrightManager?.Update();
                    pickupManager?.Update();
                    movementManager?.Update();
                    itemSpawnerManager?.Update();
                    return;
                }

                if (isListeningForHotkey)
                {
                    DetectHotkeyInput();
                    return;
                }

                var toggleMenuHotkey = configManager.GetHotkey("ToggleMenu");
                if (toggleMenuHotkey.Key != KeyCode.None)
                {
                    if (toggleMenuHotkey.IsPressed())
                    {
                        if (Time.time - lastMenuToggleTime > HOTKEY_COOLDOWN)
                        {
                            showDemoWindow = !showDemoWindow;
                            lastMenuToggleTime = Time.time;
                        }
                    }
                }

                if (configManager.GetHotkey("ToggleGodMode").IsPressed())
                {
                    godModeEnabled = !godModeEnabled;
                    configManager.SetBool("godModeEnabled", godModeEnabled);
                    SaveConfigDebounced();
                }

                if (configManager.GetHotkey("ToggleInfiniteStamina").IsPressed())
                {
                    infiniteStaminaEnabled = !infiniteStaminaEnabled;
                    configManager.SetBool("infiniteStaminaEnabled", infiniteStaminaEnabled);
                    SaveConfigDebounced();
                }

                if (configManager.GetHotkey("ToggleNoFallDamage").IsPressed())
                {
                    noFallDamageEnabled = !noFallDamageEnabled;
                    configManager.SetBool("noFallDamageEnabled", noFallDamageEnabled);
                    SaveConfigDebounced();
                }

                if (configManager.GetHotkey("ToggleSpeedBoost").IsPressed())
                {
                    speedBoostEnabled = !speedBoostEnabled;
                    configManager.SetBool("speedBoostEnabled", speedBoostEnabled);
                    SaveConfigDebounced();
                }

                if (configManager.GetHotkey("ToggleESP").IsPressed())
                {
                    espEnabled = !espEnabled;
                    configManager.SetBool("espEnabled", espEnabled);
                    SaveConfigDebounced();
                }

                if (configManager.GetHotkey("ToggleAutoLoot").IsPressed())
                {
                    autoLootEnabled = !autoLootEnabled;
                    autoLootManager?.SetEnabled(autoLootEnabled);
                    configManager.SetBool("autoLootEnabled", autoLootEnabled);
                    SaveConfigDebounced();
                }

                if (configManager.GetHotkey("ToggleFullbright").IsPressed())
                {
                    fullbright = !fullbright;
                    fullbrightManager?.SetEnabled(fullbright);
                    configManager.SetBool("fullbright", fullbright);
                    SaveConfigDebounced();
                }

                autoLootManager?.Update();
                fullbrightManager?.Update();
                pickupManager?.Update();
                movementManager?.Update();
                itemSpawnerManager?.Update();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Update error: {ex.Message}");
            }
        }

        private void DetectHotkeyInput()
        {
            try
            {
                var keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return;
                }

                if (keyboard.escapeKey != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    isListeningForHotkey = false;
                    return;
                }

                KeyCode[] allKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));

                foreach (KeyCode key in allKeys)
                {
                    if (key == KeyCode.None || key == KeyCode.Escape)
                        continue;

                    try
                    {
                        string keyName = key.ToString();
                        var targetKey = keyboard.FindKeyOnCurrentKeyboardLayout(keyName);

                        if (targetKey != null && targetKey.wasPressedThisFrame)
                        {
                            pendingKey = key;
                            pendingShift = (keyboard.leftShiftKey != null && keyboard.leftShiftKey.isPressed) || (keyboard.rightShiftKey != null && keyboard.rightShiftKey.isPressed);
                            pendingCtrl = (keyboard.leftCtrlKey != null && keyboard.leftCtrlKey.isPressed) || (keyboard.rightCtrlKey != null && keyboard.rightCtrlKey.isPressed);
                            pendingAlt = (keyboard.leftAltKey != null && keyboard.leftAltKey.isPressed) || (keyboard.rightAltKey != null && keyboard.rightAltKey.isPressed);
                            isListeningForHotkey = false;
                            return;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"DetectHotkeyInput error: {ex.Message}");
                isListeningForHotkey = false;
            }
        }

        void OnGUI()
        {
            try
            {
                GUI.color = Color.white;
                if (GUI.Button(new Rect(10, 10, 150, 30), showDemoWindow ? "Hide Menu" : "Show Menu"))
                    showDemoWindow = !showDemoWindow;

                if (showDemoWindow)
                    windowRect = GUI.Window(101, windowRect, DrawDemoWindow, "Mimesis Mod Menu");

                ESPManager.UpdateESP();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"OnGUI error: {ex.Message}");
            }
        }

        void OnDestroy()
        {
            try
            {
                SaveConfig();
                fullbrightManager?.Cleanup();
                ESPManager.Cleanup();
                pickupManager?.Stop();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"OnDestroy error: {ex.Message}");
            }
        }

        #endregion

        #region Window Tabs

        void DrawDemoWindow(int windowID)
        {
            try
            {
                guiHelper?.UpdateAnimations(showDemoWindow);
                if (!(guiHelper?.BeginAnimatedGUI() ?? false))
                    return;

                currentDemoTab = guiHelper?.VerticalTabs(demoTabs?.Select(t => t.Name).ToArray() ?? new string[0], currentDemoTab, DrawCurrentTabContent, tabWidth: 140f, maxLines: 1) ?? 0;

                guiHelper?.EndAnimatedGUI();
                GUI.DragWindow();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawDemoWindow error: {ex.Message}");
            }
        }

        void DrawCurrentTabContent()
        {
            try
            {
                scrollPosition =
                    guiHelper?.DrawScrollView(
                        scrollPosition,
                        () =>
                        {
                            guiHelper?.BeginVerticalGroup(GUILayout.ExpandHeight(true));
                            demoTabs?[currentDemoTab].Content?.Invoke();
                            guiHelper?.EndVerticalGroup();
                        },
                        GUILayout.Height(700)
                    ) ?? scrollPosition;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawCurrentTabContent error: {ex.Message}");
            }
        }

        #endregion

        #region Tab Player

        void DrawPlayerTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Defense");
                guiHelper?.CardContent(() =>
                {
                    bool godModeEnabled = MainGUI.godModeEnabled;
                    bool newGodMode = guiHelper?.Switch("God Mode", godModeEnabled) ?? false;
                    if (newGodMode != godModeEnabled)
                    {
                        MainGUI.godModeEnabled = newGodMode;
                        configManager?.SetBool("godModeEnabled", newGodMode);
                        SaveConfigDebounced();
                    }

                    guiHelper?.AddSpace(8);

                    bool noFallEnabled = noFallDamageEnabled;
                    bool newNoFall = guiHelper?.Switch("No Fall Damage", noFallEnabled) ?? false;
                    if (newNoFall != noFallEnabled)
                    {
                        noFallDamageEnabled = newNoFall;
                        configManager?.SetBool("noFallDamageEnabled", newNoFall);
                        SaveConfigDebounced();
                    }

                    guiHelper?.AddSpace(8);

                    bool staminaEnabled = infiniteStaminaEnabled;
                    bool newStamina = guiHelper?.Switch("Infinite Stamina", staminaEnabled) ?? false;
                    if (newStamina != staminaEnabled)
                    {
                        infiniteStaminaEnabled = newStamina;
                        configManager?.SetBool("infiniteStaminaEnabled", newStamina);
                        SaveConfigDebounced();
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Movement");
                guiHelper?.CardContent(() =>
                {
                    bool speedEnabled = speedBoostEnabled;
                    bool newSpeedEnabled = guiHelper?.Switch("Speed Boost", speedEnabled) ?? false;
                    if (newSpeedEnabled != speedEnabled)
                    {
                        speedBoostEnabled = newSpeedEnabled;
                        configManager?.SetBool("speedBoostEnabled", newSpeedEnabled);
                        SaveConfigDebounced();
                    }

                    if (speedBoostEnabled)
                    {
                        guiHelper?.AddSpace(8);
                        DrawLabeledSlider("Multiplier", ref speedBoostMultiplier, 1f, 5f, "x");
                        configManager?.SetFloat("speedBoostMultiplier", speedBoostMultiplier);
                        guiHelper?.MutedLabel($"Current: {speedBoostMultiplier:F2}x");
                    }

                    guiHelper?.AddSpace(10);

                    if (guiHelper?.Button("Forward 50u", ButtonVariant.Default, ButtonSize.Default) ?? false)
                        movementManager?.TeleportForward(50f);
                    guiHelper?.AddSpace(6);
                    if (guiHelper?.Button("Forward 100u", ButtonVariant.Default, ButtonSize.Default) ?? false)
                        movementManager?.TeleportForward(100f);
                    guiHelper?.AddSpace(6);
                    if (guiHelper?.Button("Forward 200u", ButtonVariant.Default, ButtonSize.Default) ?? false)
                        movementManager?.TeleportForward(200f);
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawPlayerTab error: {ex.Message}");
            }
        }

        #endregion

        #region Tab Combat

        void DrawCombatTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Bulk Actions");
                guiHelper?.CardContent(() =>
                {
                    if (guiHelper?.Button("Kill All Players", ButtonVariant.Destructive, ButtonSize.Default) ?? false)
                        KillAllPlayers();

                    guiHelper?.AddSpace(10);

                    if (guiHelper?.Button("Kill All Monsters", ButtonVariant.Destructive, ButtonSize.Default) ?? false)
                        KillAllMonsters();
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawCombatTab error: {ex.Message}");
            }
        }

        #endregion

        #region Tab Loot

        void DrawLootTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Item Collection");
                guiHelper?.CardContent(() =>
                {
                    string buttonText = (pickupManager?.isActive ?? false) ? "Stop Picking Up" : "Pickup All Items";
                    ButtonVariant variant = (pickupManager?.isActive ?? false) ? ButtonVariant.Destructive : ButtonVariant.Default;

                    if (guiHelper?.Button(buttonText, variant, ButtonSize.Default) ?? false)
                    {
                        if (pickupManager?.isActive ?? false)
                            pickupManager?.Stop();
                        else
                            pickupManager?.StartPickupAll();
                    }

                    guiHelper?.MutedLabel((pickupManager?.isActive ?? false) ? "Actively picking up items..." : "Click to start pickup");
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Auto Loot");
                guiHelper?.CardContent(() =>
                {
                    bool autoLootEnabled = autoLootManager?.IsEnabled ?? false;
                    bool newAutoLoot = guiHelper?.Switch("Auto Loot Enabled", autoLootEnabled) ?? false;
                    if (newAutoLoot != autoLootEnabled)
                    {
                        autoLootManager?.SetEnabled(newAutoLoot);
                        configManager?.SetBool("autoLootEnabled", newAutoLoot);
                        SaveConfigDebounced();
                    }

                    if (autoLootManager?.IsEnabled ?? false)
                    {
                        guiHelper?.AddSpace(8);
                        float distance = autoLootManager?.GetDistance() ?? 50f;
                        DrawLabeledSlider("Detection Range", ref distance, 10f, 200f, "m");
                        autoLootManager?.SetDistance(distance);
                        configManager?.SetFloat("autoLootDistance", distance);
                        guiHelper?.MutedLabel($"Current: {distance:F1}m");
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Shop Features");
                guiHelper?.CardContent(() =>
                {
                    DrawToggleWithSave("Force Buy", () => Patches.forceBuyEnabled, (v) => Patches.forceBuyEnabled = v, "");
                    guiHelper?.AddSpace(10);
                    DrawToggleWithSave("Force Repair", () => Patches.forceRepairEnabled, (v) => Patches.forceRepairEnabled = v, "");
                    guiHelper?.AddSpace(10);
                    DrawToggleWithSave("Infinite Currency", () => Patches.infiniteCurrencyEnabled, (v) => Patches.infiniteCurrencyEnabled = v, "");
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Item Properties");
                guiHelper?.CardDescription("Restart required");
                guiHelper?.CardContent(() =>
                {
                    DrawToggleWithRestart("Infinite Durability", () => Patches.durabilityPatchEnabled, (v) => Patches.durabilityPatchEnabled = v, "");
                    guiHelper?.AddSpace(10);
                    DrawToggleWithRestart("Infinite Price", () => Patches.pricePatchEnabled, (v) => Patches.pricePatchEnabled = v, "");
                    guiHelper?.AddSpace(10);
                    DrawToggleWithRestart("Infinite Gauge", () => Patches.gaugePatchEnabled, (v) => Patches.gaugePatchEnabled = v, "");
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Item Spawner");
                guiHelper?.CardContent(() =>
                {
                    guiHelper?.Label("Item Master ID:", LabelVariant.Default);
                    itemSpawnIDInput = GUILayout.TextField(itemSpawnIDInput, GUILayout.Height(25));
                    guiHelper?.AddSpace(6);

                    guiHelper?.Label("Quantity:", LabelVariant.Default);
                    itemSpawnQuantityInput = GUILayout.TextField(itemSpawnQuantityInput, GUILayout.Height(25));
                    guiHelper?.AddSpace(8);

                    if (guiHelper?.Button("Spawn Item", ButtonVariant.Default, ButtonSize.Default) ?? false)
                    {
                        if (int.TryParse(itemSpawnIDInput, out int itemID) && int.TryParse(itemSpawnQuantityInput, out int qty))
                        {
                            itemSpawnerManager?.AddItemToSpawn(itemID, qty);
                            guiHelper?.MutedLabel("Item queued - change inventory slot to spawn!");
                        }
                    }

                    guiHelper?.AddSpace(8);

                    var pending = itemSpawnerManager?.GetPendingItems() ?? new Dictionary<int, int>();
                    if (pending.Count > 0)
                    {
                        guiHelper?.MutedLabel("Pending items:");
                        foreach (var kvp in pending)
                        {
                            guiHelper?.MutedLabel($"  ID {kvp.Key} x{kvp.Value}");
                        }
                    }
                    else
                    {
                        guiHelper?.MutedLabel("No pending items");
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawLootTab error: {ex.Message}");
            }
        }

        #endregion

        #region Tab Visual

        void DrawVisualTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("ESP Settings");
                guiHelper?.CardContent(() =>
                {
                    bool newEspEnabled = guiHelper?.Switch("Enable ESP", MainGUI.espEnabled) ?? false;
                    if (newEspEnabled != MainGUI.espEnabled)
                    {
                        MainGUI.espEnabled = newEspEnabled;
                        configManager?.SetBool("espEnabled", newEspEnabled);
                        SaveConfigDebounced();
                    }

                    if (MainGUI.espEnabled)
                    {
                        guiHelper?.AddSpace(10);
                        DrawLabeledSlider("Distance", ref espDistance, 50f, 500f, "m");
                        configManager?.SetFloat("espDistance", espDistance);
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("ESP Visibility");
                guiHelper?.CardContent(() =>
                {
                    guiHelper?.BeginHorizontalGroup();
                    guiHelper?.BeginVerticalGroup(GUILayout.Width(150));
                    espShowPlayers = guiHelper?.Switch("Players", espShowPlayers) ?? false;
                    espShowMonsters = guiHelper?.Switch("Monsters", espShowMonsters) ?? false;
                    espShowLoot = guiHelper?.Switch("Loot", espShowLoot) ?? false;
                    guiHelper?.EndVerticalGroup();

                    guiHelper?.BeginVerticalGroup(GUILayout.Width(150));
                    espShowInteractors = guiHelper?.Switch("Interactors", espShowInteractors) ?? false;
                    espShowNPCs = guiHelper?.Switch("NPCs", espShowNPCs) ?? false;
                    espShowFieldSkills = guiHelper?.Switch("Field Skills", espShowFieldSkills) ?? false;
                    guiHelper?.EndVerticalGroup();

                    guiHelper?.BeginVerticalGroup(GUILayout.Width(150));
                    espShowProjectiles = guiHelper?.Switch("Projectiles", espShowProjectiles) ?? false;
                    espShowAuraSkills = guiHelper?.Switch("Aura Skills", espShowAuraSkills) ?? false;
                    guiHelper?.EndVerticalGroup();
                    guiHelper?.EndHorizontalGroup();

                    SaveConfigDebounced();
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Lighting");
                guiHelper?.CardContent(() =>
                {
                    bool fullbrightEnabled = fullbrightManager?.IsEnabled ?? false;
                    bool newFullbright = guiHelper?.Switch("Fullbright", fullbrightEnabled) ?? false;
                    if (newFullbright != fullbrightEnabled)
                    {
                        fullbrightManager?.SetEnabled(newFullbright);
                        fullbright = newFullbright;
                        configManager?.SetBool("fullbright", newFullbright);
                        SaveConfigDebounced();
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawVisualTab error: {ex.Message}");
            }
        }

        #endregion

        #region Tab Entities

        void DrawEntitiesTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                UpdatePlayerCache();

                guiHelper?.BeginHorizontalGroup();

                guiHelper?.BeginVerticalGroup(GUILayout.Width(300));
                guiHelper?.BeginCard(width: 280, height: 600);
                guiHelper?.CardTitle("Entity List");

                int totalCount = cachedPlayers?.Length ?? 0;
                guiHelper?.CardDescription($"Total: {totalCount}");

                guiHelper?.CardContent(() =>
                {
                    ProtoActor[] displayPlayers = cachedPlayers ?? System.Array.Empty<ProtoActor>();

                    if (displayPlayers.Length == 0)
                    {
                        guiHelper?.MutedLabel("No entities found");
                    }
                    else
                    {
                        int maxDisplay = Mathf.Min(displayPlayers.Length, 15);
                        for (int i = 0; i < maxDisplay; i++)
                        {
                            DrawActorListItem(displayPlayers[i]);
                        }

                        if (displayPlayers.Length > maxDisplay)
                        {
                            guiHelper?.MutedLabel($"...and {displayPlayers.Length - maxDisplay} more");
                        }
                    }
                });
                guiHelper?.EndCard();
                guiHelper?.EndVerticalGroup();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));
                DrawEntityActionsPanel();
                guiHelper?.EndVerticalGroup();

                guiHelper?.EndHorizontalGroup();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawEntitiesTab error: {ex.Message}");
            }
        }

        private void DrawEntityActionsPanel()
        {
            try
            {
                ProtoActor? localPlayer = PlayerAPI.GetLocalPlayer();

                guiHelper?.BeginCard(width: -1, height: 600);
                guiHelper?.CardTitle("Entity Actions");

                if (selectedPlayer != null)
                {
                    string actorType = selectedPlayer.ActorType == ActorType.Player ? "Player" : "Monster";
                    guiHelper?.CardDescription($"Target: {selectedPlayer.nickName} ({actorType})");
                }
                else
                {
                    guiHelper?.CardDescription("Select an entity to perform actions");
                }

                guiHelper?.CardContent(() =>
                {
                    if (selectedPlayer == null)
                    {
                        guiHelper?.MutedLabel("No entity selected");
                    }
                    else
                    {
                        DrawActorInfo(selectedPlayer, localPlayer);
                        guiHelper?.AddSpace(14);
                        guiHelper?.LabeledSeparator("Actions");
                        guiHelper?.AddSpace(8);

                        if (localPlayer != null)
                        {
                            if (guiHelper?.Button("Teleport To Target", ButtonVariant.Default, ButtonSize.Default) ?? false)
                                movementManager?.TeleportToPlayer(selectedPlayer);

                            if (selectedPlayer.ActorID != localPlayer.ActorID)
                            {
                                guiHelper?.AddSpace(8);
                                if (guiHelper?.Button("Teleport Target To Me", ButtonVariant.Default, ButtonSize.Default) ?? false)
                                    movementManager?.TeleportPlayerToSelf(selectedPlayer);

                                guiHelper?.AddSpace(8);
                                if (selectedPlayer.ActorType == ActorType.Player)
                                {
                                    if (guiHelper?.Button("Kill Player", ButtonVariant.Destructive, ButtonSize.Default) ?? false)
                                        KillPlayer(selectedPlayer);
                                }
                                else if (selectedPlayer.ActorType == ActorType.Monster)
                                {
                                    if (guiHelper?.Button("Kill Monster", ButtonVariant.Destructive, ButtonSize.Default) ?? false)
                                        KillMonster(selectedPlayer);
                                }
                            }
                        }
                    }
                });
                guiHelper?.EndCard();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawEntityActionsPanel error: {ex.Message}");
            }
        }

        #endregion

        #region Tab Settings

        void DrawSettingsTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Hotkey Configuration");
                guiHelper?.CardDescription("Click any hotkey to edit");
                guiHelper?.CardContent(() =>
                {
                    var hotkeys = configManager?.GetAllHotkeys() ?? new Dictionary<string, Config.HotkeyConfig>();

                    foreach (var kvp in hotkeys.OrderBy(x => x.Key))
                    {
                        string displayName = System.Text.RegularExpressions.Regex.Replace(kvp.Key, "([a-z])([A-Z])", "$1 $2");

                        guiHelper?.BeginHorizontalGroup();
                        guiHelper?.Label($"{displayName}:", LabelVariant.Default);

                        if (isListeningForHotkey && editingHotkey == kvp.Key)
                        {
                            guiHelper?.DestructiveLabel("Press any key (ESC to cancel)");
                        }
                        else
                        {
                            if (guiHelper?.Button(kvp.Value.ToString(), ButtonVariant.Secondary, ButtonSize.Small) ?? false)
                            {
                                editingHotkey = kvp.Key;
                                isListeningForHotkey = true;
                                pendingKey = KeyCode.None;
                            }
                        }
                        guiHelper?.EndHorizontalGroup();
                        guiHelper?.AddSpace(4);
                    }

                    if (pendingKey != KeyCode.None && !isListeningForHotkey && !string.IsNullOrEmpty(editingHotkey))
                    {
                        HotkeyConfig newHotkey = new HotkeyConfig(pendingKey, pendingShift, pendingCtrl, pendingAlt);
                        configManager?.SetHotkey(editingHotkey, newHotkey);
                        SaveConfig();
                        pendingKey = KeyCode.None;
                        editingHotkey = "";
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Configuration");
                guiHelper?.CardContent(() =>
                {
                    if (guiHelper?.Button("Save Configuration", ButtonVariant.Default, ButtonSize.Default) ?? false)
                    {
                        configManager?.SaveMainConfig();
                        configManager?.SaveHotkeysConfig();
                        MelonLogger.Msg("Configuration saved");
                    }

                    guiHelper?.AddSpace(6);

                    if (guiHelper?.Button("Reload Configuration", ButtonVariant.Default, ButtonSize.Default) ?? false)
                    {
                        configManager?.LoadAllConfigs();
                        MelonLogger.Msg("Configuration reloaded");
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawSettingsTab error: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private void UpdatePlayerCache()
        {
            try
            {
                if (Time.time - lastPlayerCacheTime < PLAYER_CACHE_INTERVAL)
                    return;

                ProtoActor[] allActors = PlayerAPI.GetAllPlayers();
                cachedPlayers = allActors.Where(p => p != null && !string.IsNullOrEmpty(p.nickName) && !p.dead).OrderBy(p => p.ActorType).ThenBy(p => p.nickName).ToArray();

                lastPlayerCacheTime = Time.time;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"UpdatePlayerCache error: {ex.Message}");
            }
        }

        private void DrawActorListItem(ProtoActor actor)
        {
            try
            {
                string label = actor.nickName;
                ProtoActor? localPlayer = PlayerAPI.GetLocalPlayer();
                string typeLabel = actor.ActorType == ActorType.Player ? "[P]" : "[M]";

                if (localPlayer != null && actor.ActorID == localPlayer.ActorID)
                    label += " [YOU]";

                label = $"{typeLabel} {label}";

                bool isSelected = selectedPlayer != null && selectedPlayer.ActorID == actor.ActorID;
                ButtonVariant variant = isSelected ? ButtonVariant.Secondary : ButtonVariant.Ghost;

                if (guiHelper?.Button(label, variant, ButtonSize.Small) ?? false)
                    selectedPlayer = actor;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawActorListItem error: {ex.Message}");
            }
        }

        private void DrawActorInfo(ProtoActor? selectedTarget, ProtoActor? localPlayer)
        {
            try
            {
                if (selectedTarget == null)
                    return;

                guiHelper?.Label($"Name: {selectedTarget.nickName}", LabelVariant.Default);
                guiHelper?.Label($"Type: {(selectedTarget.ActorType == ActorType.Player ? "Player" : "Monster")}", LabelVariant.Default);
                guiHelper?.Label($"Actor ID: {selectedTarget.ActorID}", LabelVariant.Default);

                if (localPlayer != null && selectedTarget.ActorID != localPlayer.ActorID)
                {
                    float distance = Vector3.Distance(selectedTarget.transform.position, localPlayer.transform.position);
                    guiHelper?.Label($"Distance: {distance:F1}m", LabelVariant.Default);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawActorInfo error: {ex.Message}");
            }
        }

        private void KillPlayer(ProtoActor target)
        {
            try
            {
                if (target != null && target.ActorType == ActorType.Player)
                {
                    target.OnActorDeath(
                        new ProtoActor.ActorDeathInfo
                        {
                            DeadActorID = target.ActorID,
                            ReasonOfDeath = ReasonOfDeath.None,
                            AttackerActorID = 0,
                            LinkedMasterID = 0,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"KillPlayer error: {ex.Message}");
            }
        }

        private void KillMonster(ProtoActor target)
        {
            try
            {
                if (target != null && target.ActorType == ActorType.Monster)
                {
                    target.OnActorDeath(
                        new ProtoActor.ActorDeathInfo
                        {
                            DeadActorID = target.ActorID,
                            ReasonOfDeath = ReasonOfDeath.None,
                            AttackerActorID = 0,
                            LinkedMasterID = 0,
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"KillMonster error: {ex.Message}");
            }
        }

        private void KillAllPlayers()
        {
            try
            {
                ProtoActor[] allActors = PlayerAPI.GetAllPlayers();
                foreach (ProtoActor actor in allActors)
                {
                    if (actor != null && actor.ActorType == ActorType.Player && !actor.dead)
                    {
                        actor.OnActorDeath(
                            new ProtoActor.ActorDeathInfo
                            {
                                DeadActorID = actor.ActorID,
                                ReasonOfDeath = ReasonOfDeath.None,
                                AttackerActorID = 0,
                                LinkedMasterID = 0,
                            }
                        );
                    }
                }
                MelonLogger.Msg("Killed all players");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"KillAllPlayers error: {ex.Message}");
            }
        }

        private void KillAllMonsters()
        {
            try
            {
                ProtoActor[] allActors = PlayerAPI.GetAllPlayers();
                foreach (ProtoActor actor in allActors)
                {
                    if (actor != null && actor.ActorType == ActorType.Monster && !actor.dead)
                    {
                        actor.OnActorDeath(
                            new ProtoActor.ActorDeathInfo
                            {
                                DeadActorID = actor.ActorID,
                                ReasonOfDeath = ReasonOfDeath.None,
                                AttackerActorID = 0,
                                LinkedMasterID = 0,
                            }
                        );
                    }
                }
                MelonLogger.Msg("Killed all monsters");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"KillAllMonsters error: {ex.Message}");
            }
        }

        private void DrawLabeledSlider(string label, ref float value, float min, float max, string suffix)
        {
            try
            {
                guiHelper?.BeginHorizontalGroup();
                guiHelper?.Label($"{label}: {value:F1}{suffix}", LabelVariant.Default);
                value = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
                guiHelper?.EndHorizontalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawLabeledSlider error: {ex.Message}");
            }
        }

        private void DrawToggleWithRestart(string label, Func<bool> getter, Action<bool> setter, string description = "")
        {
            try
            {
                bool oldValue = getter();
                bool newValue = guiHelper?.Switch(label, oldValue) ?? false;

                if (!string.IsNullOrEmpty(description))
                    guiHelper?.MutedLabel(description);

                if (oldValue != newValue)
                {
                    setter(newValue);
                    SaveConfigDebounced();
                    guiHelper?.AddSpace(6);
                    guiHelper?.DestructiveLabel("Restart game to apply");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawToggleWithRestart error: {ex.Message}");
            }
        }

        private void DrawToggleWithSave(string label, Func<bool> getter, Action<bool> setter, string description = "")
        {
            try
            {
                bool oldValue = getter();
                bool newValue = guiHelper?.Switch(label, oldValue) ?? false;

                if (!string.IsNullOrEmpty(description))
                    guiHelper?.MutedLabel(description);

                if (oldValue != newValue)
                {
                    setter(newValue);
                    SaveConfigDebounced();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawToggleWithSave error: {ex.Message}");
            }
        }

        private void SaveConfigDebounced()
        {
            try
            {
                if (Time.time - lastConfigSaveTime > 0.5f)
                {
                    SaveConfig();
                    lastConfigSaveTime = Time.time;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"SaveConfigDebounced error: {ex.Message}");
            }
        }

        private void SaveConfig()
        {
            try
            {
                configManager?.SaveMainConfig();
                configManager?.SaveHotkeysConfig();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"SaveConfig error: {ex.Message}");
            }
        }

        #endregion
    }
}
