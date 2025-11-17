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
        private GUIHelper guiHelper;
        private ConfigManager configManager;
        private Rect windowRect = new Rect(20, 20, 1000, 800);
        private Vector2 scrollPosition;
        private int currentDemoTab;
        private Tabs.TabConfig[] demoTabs;

        private PickupManager pickupManager;
        private MovementManager movementManager;
        private AutoLootManager autoLootManager;
        private FullbrightManager fullbrightManager;
        private ItemSpawnerManager itemSpawnerManager;

        private ProtoActor selectedPlayer;
        private ProtoActor[] cachedPlayers;
        private float lastPlayerCacheTime;

        private string editingHotkey = "";
        private bool isListeningForHotkey = false;
        private KeyCode pendingKey = KeyCode.None;
        private bool pendingShift,
            pendingCtrl,
            pendingAlt;
        private string itemSpawnIDInput = "1001";
        private string itemSpawnQuantityInput = "1";

        private bool showDemoWindow = true;
        private float lastMenuToggleTime;

        private const float PLAYER_CACHE_INTERVAL = 5f;
        private const float HOTKEY_COOLDOWN = 0.5f;

        public class FeatureState
        {
            public bool GodMode = false;
            public bool InfiniteStamina = false;
            public bool NoFallDamage = false;
            public bool SpeedBoost = false;
            public float SpeedMultiplier = 2f;
            public bool ESP = false;
            public float ESPDistance = 150f;
            public bool ESPShowLoot = false;
            public bool ESPShowPlayers = false;
            public bool ESPShowMonsters = false;
            public bool ESPShowInteractors = false;
            public bool ESPShowNPCs = false;
            public bool ESPShowFieldSkills = false;
            public bool ESPShowProjectiles = false;
            public bool ESPShowAuraSkills = false;
            public bool AutoLoot = false;
            public float AutoLootDistance = 50f;
            public bool Fullbright = false;
            public bool InfiniteDurability = false;
            public bool InfinitePrice = false;
            public bool InfiniteGauge = false;
            public bool ForceBuy = false;
            public bool ForceRepair = false;
            public bool InfiniteCurrency = false;
        }

        private FeatureState state = new FeatureState();

        void Start()
        {
            try
            {
                guiHelper = new GUIHelper();
                configManager = new ConfigManager();

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

                var toggleMenuHotkey = configManager.GetHotkey("ToggleMenu");
                if (toggleMenuHotkey.Key == KeyCode.None)
                {
                    configManager.SetHotkey("ToggleMenu", new HotkeyConfig(KeyCode.Insert, false, false, false));
                    MelonLogger.Msg("Set ToggleMenu hotkey to Insert");
                }

                bool enabled = configManager.GetValue<bool>("Enabled", true, "Enable or disable the mod menu");
                if (!enabled)
                    showDemoWindow = false;

                MelonLogger.Msg("Mimesis Mod Menu initialized successfully");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Start error: {ex.Message}");
            }
        }

        private void SaveSetting(string key, object value)
        {
            if (value is bool b)
                configManager.SetValue<bool>(key, b);
            else if (value is float f)
                configManager.SetValue<float>(key, f);
            else if (value is string s)
                configManager.SetValue<string>(key, s);
        }

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

                bool menuEnabled = configManager.GetValue<bool>("Enabled", true);
                if (!menuEnabled)
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

                if (configManager.GetHotkey("ToggleMenu").IsPressed())
                {
                    if (Time.time - lastMenuToggleTime > HOTKEY_COOLDOWN)
                    {
                        showDemoWindow = !showDemoWindow;
                        lastMenuToggleTime = Time.time;
                    }
                }

                if (configManager.GetHotkey("ToggleGodMode").IsPressed())
                    ToggleBool("godMode", x => state.GodMode = x, () => state.GodMode);

                if (configManager.GetHotkey("ToggleInfiniteStamina").IsPressed())
                    ToggleBool("infiniteStamina", x => state.InfiniteStamina = x, () => state.InfiniteStamina);

                if (configManager.GetHotkey("ToggleNoFallDamage").IsPressed())
                    ToggleBool("noFallDamage", x => state.NoFallDamage = x, () => state.NoFallDamage);

                if (configManager.GetHotkey("ToggleSpeedBoost").IsPressed())
                    ToggleBool("speedBoost", x => state.SpeedBoost = x, () => state.SpeedBoost);

                if (configManager.GetHotkey("ToggleESP").IsPressed())
                    ToggleBool("espEnabled", x => state.ESP = x, () => state.ESP);

                if (configManager.GetHotkey("ToggleAutoLoot").IsPressed())
                {
                    state.AutoLoot = !state.AutoLoot;
                    autoLootManager?.SetEnabled(state.AutoLoot);
                }

                if (configManager.GetHotkey("ToggleFullbright").IsPressed())
                {
                    state.Fullbright = !state.Fullbright;
                    fullbrightManager?.SetEnabled(state.Fullbright);
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

        private void ToggleBool(string key, Action<bool> setter, Func<bool> getter)
        {
            bool newVal = !getter();
            setter(newVal);
        }

        private void DetectHotkeyInput()
        {
            try
            {
                var keyboard = Keyboard.current;
                if (keyboard?.escapeKey?.wasPressedThisFrame == true)
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
                        var targetKey = keyboard.FindKeyOnCurrentKeyboardLayout(key.ToString());
                        if (targetKey?.wasPressedThisFrame == true)
                        {
                            pendingKey = key;
                            pendingShift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                            pendingCtrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                            pendingAlt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
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
                bool menuEnabled = configManager?.GetValue<bool>("Enabled", true) ?? true;
                if (!menuEnabled)
                    return;

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
                fullbrightManager?.Cleanup();
                ESPManager.Cleanup();
                pickupManager?.Stop();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"OnDestroy error: {ex.Message}");
            }
        }

        public FeatureState GetFeatureState()
        {
            return state;
        }

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

        void DrawPlayerTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Defense");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch("God Mode", x => state.GodMode = x, () => state.GodMode);
                    guiHelper?.AddSpace(8);
                    DrawBoolSwitch("No Fall Damage", x => state.NoFallDamage = x, () => state.NoFallDamage);
                    guiHelper?.AddSpace(8);
                    DrawBoolSwitch("Infinite Stamina", x => state.InfiniteStamina = x, () => state.InfiniteStamina);
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Movement");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch("Speed Boost", x => state.SpeedBoost = x, () => state.SpeedBoost);

                    if (state.SpeedBoost)
                    {
                        guiHelper?.AddSpace(8);
                        DrawFloatSlider("Multiplier", x => state.SpeedMultiplier = x, () => state.SpeedMultiplier, 1f, 5f, "x");
                        guiHelper?.MutedLabel($"Current: {state.SpeedMultiplier:F2}x");
                    }

                    guiHelper?.AddSpace(10);
                    DrawTeleportButton("Forward 50u", 50f);
                    guiHelper?.AddSpace(6);
                    DrawTeleportButton("Forward 100u", 100f);
                    guiHelper?.AddSpace(6);
                    DrawTeleportButton("Forward 200u", 200f);
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawPlayerTab error: {ex.Message}");
            }
        }

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
                        KillAllActors(ActorType.Player);

                    guiHelper?.AddSpace(10);

                    if (guiHelper?.Button("Kill All Monsters", ButtonVariant.Destructive, ButtonSize.Default) ?? false)
                        KillAllActors(ActorType.Monster);
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawCombatTab error: {ex.Message}");
            }
        }

        void DrawLootTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Item Collection");
                guiHelper?.CardContent(() =>
                {
                    bool isActive = pickupManager?.isActive ?? false;
                    string buttonText = isActive ? "Stop Picking Up" : "Pickup All Items";
                    ButtonVariant variant = isActive ? ButtonVariant.Destructive : ButtonVariant.Default;

                    if (guiHelper?.Button(buttonText, variant, ButtonSize.Default) ?? false)
                    {
                        if (isActive)
                            pickupManager?.Stop();
                        else
                            pickupManager?.StartPickupAll();
                    }

                    guiHelper?.MutedLabel(isActive ? "Actively picking up items..." : "Click to start pickup");
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Auto Loot");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch("Auto Loot Enabled", x => state.AutoLoot = x, () => state.AutoLoot);

                    if (state.AutoLoot)
                    {
                        guiHelper?.AddSpace(8);
                        DrawFloatSlider("Detection Range", x => state.AutoLootDistance = x, () => state.AutoLootDistance, 10f, 200f, "m");
                        autoLootManager?.SetDistance(state.AutoLootDistance);
                        guiHelper?.MutedLabel($"Current: {state.AutoLootDistance:F1}m");
                    }
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Equipment");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch("Infinite Durability", x => state.InfiniteDurability = x, () => state.InfiniteDurability);
                    guiHelper?.AddSpace(8);
                    DrawBoolSwitch("Infinite Gauge", x => state.InfiniteGauge = x, () => state.InfiniteGauge);
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Commerce");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch("Infinite Currency", x => state.InfiniteCurrency = x, () => state.InfiniteCurrency);
                    guiHelper?.AddSpace(8);
                    DrawBoolSwitch("Infinite Price", x => state.InfinitePrice = x, () => state.InfinitePrice);
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Shop Actions");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch("Force Buy", x => state.ForceBuy = x, () => state.ForceBuy);
                    guiHelper?.AddSpace(8);
                    DrawBoolSwitch("Force Repair", x => state.ForceRepair = x, () => state.ForceRepair);
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawLootTab error: {ex.Message}");
            }
        }

        void DrawVisualTab()
        {
            try
            {
                guiHelper?.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("ESP Settings");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch("Enable ESP", x => state.ESP = x, () => state.ESP);

                    if (state.ESP)
                    {
                        guiHelper?.AddSpace(10);
                        DrawFloatSlider("Distance", x => state.ESPDistance = x, () => state.ESPDistance, 50f, 500f, "m");
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
                    DrawBoolSwitch("Players", x => state.ESPShowPlayers = x, () => state.ESPShowPlayers);
                    DrawBoolSwitch("Monsters", x => state.ESPShowMonsters = x, () => state.ESPShowMonsters);
                    DrawBoolSwitch("Loot", x => state.ESPShowLoot = x, () => state.ESPShowLoot);
                    guiHelper?.EndVerticalGroup();

                    guiHelper?.BeginVerticalGroup(GUILayout.Width(150));
                    DrawBoolSwitch("Interactors", x => state.ESPShowInteractors = x, () => state.ESPShowInteractors);
                    DrawBoolSwitch("NPCs", x => state.ESPShowNPCs = x, () => state.ESPShowNPCs);
                    DrawBoolSwitch("Field Skills", x => state.ESPShowFieldSkills = x, () => state.ESPShowFieldSkills);
                    guiHelper?.EndVerticalGroup();

                    guiHelper?.BeginVerticalGroup(GUILayout.Width(150));
                    DrawBoolSwitch("Projectiles", x => state.ESPShowProjectiles = x, () => state.ESPShowProjectiles);
                    DrawBoolSwitch("Aura Skills", x => state.ESPShowAuraSkills = x, () => state.ESPShowAuraSkills);
                    guiHelper?.EndVerticalGroup();

                    guiHelper?.EndHorizontalGroup();
                });
                guiHelper?.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Lighting");
                guiHelper?.CardContent(() =>
                {
                    DrawBoolSwitch(
                        "Fullbright",
                        x =>
                        {
                            state.Fullbright = x;
                            fullbrightManager?.SetEnabled(state.Fullbright);
                        },
                        () => state.Fullbright
                    );
                });
                guiHelper?.EndCard();

                guiHelper?.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawVisualTab error: {ex.Message}");
            }
        }

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
                            DrawActorListItem(displayPlayers[i]);

                        if (displayPlayers.Length > maxDisplay)
                            guiHelper?.MutedLabel($"...and {displayPlayers.Length - maxDisplay} more");
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
                ProtoActor localPlayer = PlayerAPI.GetLocalPlayer();

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
                                        KillActor(selectedPlayer);
                                }
                                else if (selectedPlayer.ActorType == ActorType.Monster)
                                {
                                    if (guiHelper?.Button("Kill Monster", ButtonVariant.Destructive, ButtonSize.Default) ?? false)
                                        KillActor(selectedPlayer);
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
                            var currentHotkey = configManager?.GetHotkey(kvp.Key);
                            if (guiHelper?.Button(currentHotkey?.ToString() ?? "None", ButtonVariant.Secondary, ButtonSize.Small) ?? false)
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
                        var newHotkey = new HotkeyConfig(pendingKey, pendingShift, pendingCtrl, pendingAlt);
                        configManager?.SetHotkey(editingHotkey, newHotkey);
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
                        MelonLogger.Msg("Configuration saved");

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

        private void DrawBoolSwitch(string label, Action<bool> setter, Func<bool> getter)
        {
            bool oldValue = getter();
            bool newValue = guiHelper?.Switch(label, oldValue) ?? false;
            if (newValue != oldValue)
            {
                setter(newValue);
            }
        }

        private void DrawFloatSlider(string label, Action<float> setter, Func<float> getter, float min, float max, string suffix)
        {
            float oldValue = getter();
            guiHelper?.BeginHorizontalGroup();
            guiHelper?.Label($"{label}: {oldValue:F1}{suffix}", LabelVariant.Default);
            float newValue = GUILayout.HorizontalSlider(oldValue, min, max, GUILayout.ExpandWidth(true));
            guiHelper?.EndHorizontalGroup();

            if (newValue != oldValue)
            {
                setter(newValue);
            }
        }

        private void DrawTeleportButton(string label, float distance)
        {
            if (guiHelper?.Button(label, ButtonVariant.Default, ButtonSize.Default) ?? false)
                movementManager?.TeleportForward(distance);
        }

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
                ProtoActor localPlayer = PlayerAPI.GetLocalPlayer();
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

        private void DrawActorInfo(ProtoActor selectedTarget, ProtoActor localPlayer)
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

        private void KillActor(ProtoActor target)
        {
            try
            {
                if (target == null)
                    return;

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
            catch (Exception ex)
            {
                MelonLogger.Error($"KillActor error: {ex.Message}");
            }
        }

        private void KillAllActors(ActorType type)
        {
            try
            {
                ProtoActor[] allActors = PlayerAPI.GetAllPlayers();
                foreach (ProtoActor actor in allActors)
                {
                    if (actor != null && actor.ActorType == type && !actor.dead)
                        KillActor(actor);
                }

                string typeName = type == ActorType.Player ? "players" : "monsters";
                MelonLogger.Msg($"Killed all {typeName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"KillAllActors error: {ex.Message}");
            }
        }
    }
}
