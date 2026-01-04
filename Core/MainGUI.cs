using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Mimesis_Mod_Menu.Core.Config;
using Mimesis_Mod_Menu.Core.Features;
using Mimic;
using Mimic.Actors;
using MimicAPI.GameAPI;
using ReluProtocol.Enum;
using shadcnui.GUIComponents.Core;
using shadcnui.GUIComponents.Core.Base;
using shadcnui.GUIComponents.Core.Styling;
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
        private string itemSearchFilter = "";
        private Vector2 itemListScrollPosition;
        private List<(int id, string name)> cachedItemList = new List<(int, string)>();
        private bool itemListLoaded = false;

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
            public bool Fly = false;
            public float FlySpeed = 10f;
            public bool DamageMultiplier = false;
            public float DamageMultiplierValue = 10f;
            public bool CustomScale = false;
            public float PlayerScale = 1f;
            public Vector3 SavedPosition1 = Vector3.zero;
            public Vector3 SavedPosition2 = Vector3.zero;
            public Vector3 SavedPosition3 = Vector3.zero;
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

        void Update()
        {
            try
            {
                if (configManager == null)
                {
                    autoLootManager.Update();
                    fullbrightManager.Update();
                    pickupManager.Update();
                    movementManager.Update();
                    itemSpawnerManager.Update();
                    return;
                }

                bool menuEnabled = configManager.GetValue<bool>("Enabled", true);
                if (!menuEnabled)
                {
                    autoLootManager.Update();
                    fullbrightManager.Update();
                    pickupManager.Update();
                    movementManager.Update();
                    itemSpawnerManager.Update();
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
                    autoLootManager.SetEnabled(state.AutoLoot);
                }

                if (configManager.GetHotkey("ToggleFullbright").IsPressed())
                {
                    state.Fullbright = !state.Fullbright;
                    fullbrightManager.SetEnabled(state.Fullbright);
                }

                autoLootManager.Update();
                fullbrightManager.Update();
                pickupManager.Update();
                movementManager.Update();
                itemSpawnerManager.Update();
                UpdateFly();
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
                if (keyboard.escapeKey.wasPressedThisFrame == true)
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
                        if (targetKey.wasPressedThisFrame == true)
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
                bool menuEnabled = configManager.GetValue<bool>("Enabled", true);
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
                fullbrightManager.Cleanup();
                ESPManager.Cleanup();
                pickupManager.Stop();
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
                guiHelper.UpdateGUI(showDemoWindow);
                if (!(guiHelper.BeginGUI()))
                    return;

                currentDemoTab = guiHelper.VerticalTabs(demoTabs.Select(t => t.Name).ToArray() ?? new string[0], currentDemoTab, DrawCurrentTabContent, tabWidth: 140f, maxLines: 1);

                guiHelper.EndGUI();
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
                scrollPosition = guiHelper.ScrollView(
                    scrollPosition,
                    () =>
                    {
                        guiHelper.BeginVerticalGroup(GUILayout.ExpandHeight(true));
                        demoTabs[currentDemoTab].Content.Invoke();
                        guiHelper.EndVerticalGroup();
                    },
                    GUILayout.Height(700)
                );
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
                guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Defense");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("God Mode", x => state.GodMode = x, () => state.GodMode);
                    guiHelper.AddSpace(8);
                    DrawToggleButton("No Fall Damage", x => state.NoFallDamage = x, () => state.NoFallDamage);
                    guiHelper.AddSpace(8);
                    DrawToggleButton("Infinite Stamina", x => state.InfiniteStamina = x, () => state.InfiniteStamina);
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Movement");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("Speed Boost", x => state.SpeedBoost = x, () => state.SpeedBoost);

                    if (state.SpeedBoost)
                    {
                        guiHelper.AddSpace(8);
                        DrawFloatSlider("Multiplier", x => state.SpeedMultiplier = x, () => state.SpeedMultiplier, 1f, 5f, "x");
                        guiHelper.MutedLabel($"Current: {state.SpeedMultiplier:F2}x");
                    }

                    guiHelper.AddSpace(10);

                    DrawToggleButton(
                        "Fly",
                        x =>
                        {
                            state.Fly = x;
                            if (x)
                                EnableFly();
                            else
                                DisableFly();
                        },
                        () => state.Fly
                    );

                    if (state.Fly)
                    {
                        guiHelper.AddSpace(8);
                        DrawFloatSlider("Fly Speed", x => state.FlySpeed = x, () => state.FlySpeed, 5f, 50f, "m/s");
                        guiHelper.MutedLabel("WASD to move, Space/Ctrl for up/down");
                    }

                    guiHelper.AddSpace(10);
                    DrawTeleportButton("Forward 50u", 50f);
                    guiHelper.AddSpace(6);
                    DrawTeleportButton("Forward 100u", 100f);
                    guiHelper.AddSpace(6);
                    DrawTeleportButton("Forward 200u", 200f);
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Saved Positions");
                guiHelper.CardContent(() =>
                {
                    GUILayout.BeginHorizontal();
                    if (guiHelper.Button("Save 1", ControlVariant.Default, ControlSize.Small))
                        SavePosition(1);
                    if (guiHelper.Button("Load 1", ControlVariant.Secondary, ControlSize.Small))
                        LoadPosition(1);
                    GUILayout.EndHorizontal();

                    guiHelper.AddSpace(4);

                    GUILayout.BeginHorizontal();
                    if (guiHelper.Button("Save 2", ControlVariant.Default, ControlSize.Small))
                        SavePosition(2);
                    if (guiHelper.Button("Load 2", ControlVariant.Secondary, ControlSize.Small))
                        LoadPosition(2);
                    GUILayout.EndHorizontal();

                    guiHelper.AddSpace(4);

                    GUILayout.BeginHorizontal();
                    if (guiHelper.Button("Save 3", ControlVariant.Default, ControlSize.Small))
                        SavePosition(3);
                    if (guiHelper.Button("Load 3", ControlVariant.Secondary, ControlSize.Small))
                        LoadPosition(3);
                    GUILayout.EndHorizontal();
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Appearance");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton(
                        "Custom Scale",
                        x =>
                        {
                            state.CustomScale = x;
                            ApplyPlayerScale();
                        },
                        () => state.CustomScale
                    );

                    if (state.CustomScale)
                    {
                        guiHelper.AddSpace(8);
                        DrawFloatSlider(
                            "Scale",
                            x =>
                            {
                                state.PlayerScale = x;
                                ApplyPlayerScale();
                            },
                            () => state.PlayerScale,
                            0.1f,
                            5f,
                            "x"
                        );
                    }
                });
                guiHelper.EndCard();

                guiHelper.EndVerticalGroup();
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
                guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Damage");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("Damage Multiplier", x => state.DamageMultiplier = x, () => state.DamageMultiplier);

                    if (state.DamageMultiplier)
                    {
                        guiHelper.AddSpace(8);
                        DrawFloatSlider("Multiplier", x => state.DamageMultiplierValue = x, () => state.DamageMultiplierValue, 1f, 100f, "x");
                        guiHelper.MutedLabel($"Current: {state.DamageMultiplierValue:F1}x damage");
                    }
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Bulk Actions");
                guiHelper.CardContent(() =>
                {
                    if (guiHelper.Button("Kill All Players", ControlVariant.Destructive, ControlSize.Default))
                        KillAllActors(ActorType.Player);

                    guiHelper.AddSpace(10);

                    if (guiHelper.Button("Kill All Monsters", ControlVariant.Destructive, ControlSize.Default))
                        KillAllActors(ActorType.Monster);

                    guiHelper.AddSpace(10);

                    if (guiHelper.Button("Clear All Monsters", ControlVariant.Destructive, ControlSize.Default))
                        ClearAllMonsters();
                });
                guiHelper.EndCard();

                guiHelper.EndVerticalGroup();
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
                guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Item Collection");
                guiHelper.CardContent(() =>
                {
                    bool isActive = pickupManager.isActive;
                    string buttonText = isActive ? "Stop Picking Up" : "Pickup All Items";
                    ControlVariant variant = isActive ? ControlVariant.Destructive : ControlVariant.Default;

                    if (guiHelper.Button(buttonText, variant, ControlSize.Default))
                    {
                        if (isActive)
                            pickupManager.Stop();
                        else
                            pickupManager.StartPickupAll();
                    }

                    guiHelper.MutedLabel(isActive ? "Actively picking up items..." : "Click to start pickup");
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Auto Loot");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("Auto Loot Enabled", x => state.AutoLoot = x, () => state.AutoLoot);

                    if (state.AutoLoot)
                    {
                        guiHelper.AddSpace(8);
                        DrawFloatSlider("Detection Range", x => state.AutoLootDistance = x, () => state.AutoLootDistance, 10f, 200f, "m");
                        autoLootManager.SetDistance(state.AutoLootDistance);
                        guiHelper.MutedLabel($"Current: {state.AutoLootDistance:F1}m");
                    }
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Equipment");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("Infinite Durability", x => state.InfiniteDurability = x, () => state.InfiniteDurability);
                    guiHelper.AddSpace(8);
                    DrawToggleButton("Infinite Gauge", x => state.InfiniteGauge = x, () => state.InfiniteGauge);
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Commerce");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("Infinite Currency", x => state.InfiniteCurrency = x, () => state.InfiniteCurrency);
                    guiHelper.AddSpace(8);
                    DrawToggleButton("Infinite Price", x => state.InfinitePrice = x, () => state.InfinitePrice);
                    guiHelper?.AddSpace(8);
                    if (guiHelper?.Button("Add 10,000 Currency", ControlVariant.Default, ControlSize.Default) ?? false)
                    {
                        AddCurrency(10000);
                    }
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Shop Actions");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("Force Buy", x => state.ForceBuy = x, () => state.ForceBuy);
                    guiHelper.AddSpace(8);
                    DrawToggleButton("Force Repair", x => state.ForceRepair = x, () => state.ForceRepair);
                });
                guiHelper.EndCard();

                guiHelper?.AddSpace(12);

                guiHelper?.BeginCard(width: -1, height: -1);
                guiHelper?.CardTitle("Item Spawner");
                guiHelper?.CardContent(() =>
                {
                    GUILayout.BeginHorizontal();
                    guiHelper?.Label("Item ID:");
                    itemSpawnIDInput = GUILayout.TextField(itemSpawnIDInput, GUILayout.Width(100));
                    GUILayout.EndHorizontal();

                    guiHelper?.AddSpace(4);

                    if (guiHelper?.Button("Spawn Item", ControlVariant.Default, ControlSize.Default) ?? false)
                    {
                        if (int.TryParse(itemSpawnIDInput, out int itemId))
                        {
                            ItemSpawnerPatches.SetItemToSpawn(itemId, 1);
                        }
                        else
                        {
                            MelonLogger.Warning("[ItemSpawner] Invalid item ID");
                        }
                    }

                    guiHelper?.AddSpace(12);
                    GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
                    guiHelper?.AddSpace(8);

                    GUILayout.BeginHorizontal();
                    guiHelper?.Label("Item Browser");
                    if (guiHelper?.Button(itemListLoaded ? "Refresh List" : "Load Items", ControlVariant.Secondary, ControlSize.Small) ?? false)
                    {
                        LoadItemList();
                    }
                    GUILayout.EndHorizontal();

                    if (itemListLoaded && cachedItemList.Count > 0)
                    {
                        guiHelper?.AddSpace(4);

                        GUILayout.BeginHorizontal();
                        guiHelper?.Label("Search:");
                        itemSearchFilter = GUILayout.TextField(itemSearchFilter, GUILayout.Width(200));
                        GUILayout.EndHorizontal();

                        guiHelper?.AddSpace(4);
                        guiHelper?.MutedLabel($"Found {cachedItemList.Count} items");

                        itemListScrollPosition = GUILayout.BeginScrollView(itemListScrollPosition, GUILayout.Height(200));

                        var filteredItems = string.IsNullOrEmpty(itemSearchFilter) ? cachedItemList.Take(50).ToList() : cachedItemList.Where(x => x.name.ToLower().Contains(itemSearchFilter.ToLower()) || x.id.ToString().Contains(itemSearchFilter)).Take(50).ToList();

                        foreach (var item in filteredItems)
                        {
                            if (guiHelper?.Button($"[{item.id}] {item.name}", ControlVariant.Secondary, ControlSize.Small) ?? false)
                            {
                                itemSpawnIDInput = item.id.ToString();
                                MelonLogger.Msg($"[ItemSpawner] Selected: {item.name} (ID: {item.id})");
                            }
                        }

                        GUILayout.EndScrollView();
                    }
                    else if (!itemListLoaded)
                    {
                        guiHelper?.MutedLabel("Click 'Load Items' to browse available items");
                    }
                    else
                    {
                        guiHelper?.MutedLabel("No items found");
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

        void DrawVisualTab()
        {
            try
            {
                guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("ESP Settings");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton("Enable ESP", x => state.ESP = x, () => state.ESP);

                    if (state.ESP)
                    {
                        guiHelper.AddSpace(10);
                        DrawFloatSlider("Distance", x => state.ESPDistance = x, () => state.ESPDistance, 50f, 500f, "m");
                    }
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("ESP Visibility");
                guiHelper.CardContent(() =>
                {
                    guiHelper.BeginHorizontalGroup();

                    guiHelper.BeginVerticalGroup(GUILayout.Width(150));
                    DrawToggleButton("Players", x => state.ESPShowPlayers = x, () => state.ESPShowPlayers);
                    DrawToggleButton("Monsters", x => state.ESPShowMonsters = x, () => state.ESPShowMonsters);
                    DrawToggleButton("Loot", x => state.ESPShowLoot = x, () => state.ESPShowLoot);
                    guiHelper.EndVerticalGroup();

                    guiHelper.BeginVerticalGroup(GUILayout.Width(150));
                    DrawToggleButton("Interactors", x => state.ESPShowInteractors = x, () => state.ESPShowInteractors);
                    DrawToggleButton("NPCs", x => state.ESPShowNPCs = x, () => state.ESPShowNPCs);
                    DrawToggleButton("Field Skills", x => state.ESPShowFieldSkills = x, () => state.ESPShowFieldSkills);
                    guiHelper.EndVerticalGroup();

                    guiHelper.BeginVerticalGroup(GUILayout.Width(150));
                    DrawToggleButton("Projectiles", x => state.ESPShowProjectiles = x, () => state.ESPShowProjectiles);
                    DrawToggleButton("Aura Skills", x => state.ESPShowAuraSkills = x, () => state.ESPShowAuraSkills);
                    guiHelper.EndVerticalGroup();

                    guiHelper.EndHorizontalGroup();
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Lighting");
                guiHelper.CardContent(() =>
                {
                    DrawToggleButton(
                        "Fullbright",
                        x =>
                        {
                            state.Fullbright = x;
                            fullbrightManager.SetEnabled(state.Fullbright);
                        },
                        () => state.Fullbright
                    );
                });
                guiHelper.EndCard();

                guiHelper.EndVerticalGroup();
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
                guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                UpdatePlayerCache();

                guiHelper.BeginHorizontalGroup();

                guiHelper.BeginVerticalGroup(GUILayout.Width(300));
                guiHelper.BeginCard(width: 280, height: 600);
                guiHelper.CardTitle("Entity List");

                int totalCount = cachedPlayers.Length;
                guiHelper.CardDescription($"Total: {totalCount}");

                guiHelper.CardContent(() =>
                {
                    ProtoActor[] displayPlayers = cachedPlayers;

                    if (displayPlayers.Length == 0)
                    {
                        guiHelper.MutedLabel("No entities found");
                    }
                    else
                    {
                        int maxDisplay = Mathf.Min(displayPlayers.Length, 15);
                        for (int i = 0; i < maxDisplay; i++)
                            DrawActorListItem(displayPlayers[i]);

                        if (displayPlayers.Length > maxDisplay)
                            guiHelper.MutedLabel($"...and {displayPlayers.Length - maxDisplay} more");
                    }
                });
                guiHelper.EndCard();
                guiHelper.EndVerticalGroup();

                guiHelper.AddSpace(12);

                guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));
                DrawEntityActionsPanel();
                guiHelper.EndVerticalGroup();

                guiHelper.EndHorizontalGroup();
                guiHelper.EndVerticalGroup();
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

                guiHelper.BeginCard(width: -1, height: 600);
                guiHelper.CardTitle("Entity Actions");

                if (selectedPlayer != null)
                {
                    string actorType = selectedPlayer.ActorType == ActorType.Player ? "Player" : "Monster";
                    guiHelper.CardDescription($"Target: {selectedPlayer.nickName} ({actorType})");
                }
                else
                {
                    guiHelper.CardDescription("Select an entity to perform actions");
                }

                guiHelper.CardContent(() =>
                {
                    if (selectedPlayer == null)
                    {
                        guiHelper.MutedLabel("No entity selected");
                    }
                    else
                    {
                        DrawActorInfo(selectedPlayer, localPlayer);
                        guiHelper.AddSpace(14);
                        guiHelper.LabeledSeparator("Actions");
                        guiHelper.AddSpace(8);

                        if (localPlayer != null)
                        {
                            if (guiHelper.Button("Teleport To Target", ControlVariant.Default, ControlSize.Default))
                                movementManager.TeleportToPlayer(selectedPlayer);

                            if (selectedPlayer.ActorID != localPlayer.ActorID)
                            {
                                guiHelper.AddSpace(8);
                                if (guiHelper.Button("Teleport Target To Me", ControlVariant.Default, ControlSize.Default))
                                    movementManager.TeleportPlayerToSelf(selectedPlayer);

                                guiHelper.AddSpace(8);
                                if (selectedPlayer.ActorType == ActorType.Player)
                                {
                                    if (guiHelper.Button("Kill Player", ControlVariant.Destructive, ControlSize.Default))
                                        KillActor(selectedPlayer);
                                }
                                else if (selectedPlayer.ActorType == ActorType.Monster)
                                {
                                    if (guiHelper.Button("Kill Monster", ControlVariant.Destructive, ControlSize.Default))
                                        KillActor(selectedPlayer);
                                }
                            }
                        }
                    }
                });
                guiHelper.EndCard();
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
                guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Hotkey Configuration");
                guiHelper.CardDescription("Click any hotkey to edit");
                guiHelper.CardContent(() =>
                {
                    var hotkeys = configManager.GetAllHotkeys();

                    foreach (var kvp in hotkeys.OrderBy(x => x.Key))
                    {
                        string displayName = System.Text.RegularExpressions.Regex.Replace(kvp.Key, "([a-z])([A-Z])", "$1 $2");

                        guiHelper.BeginHorizontalGroup();
                        guiHelper.Label($"{displayName}:", ControlVariant.Default);

                        if (isListeningForHotkey && editingHotkey == kvp.Key)
                        {
                            guiHelper.DestructiveLabel("Press any key (ESC to cancel)");
                        }
                        else
                        {
                            var currentHotkey = configManager.GetHotkey(kvp.Key);
                            if (guiHelper.Button(currentHotkey.ToString() ?? "None", ControlVariant.Secondary, ControlSize.Small))
                            {
                                editingHotkey = kvp.Key;
                                isListeningForHotkey = true;
                                pendingKey = KeyCode.None;
                            }
                        }
                        guiHelper.EndHorizontalGroup();
                        guiHelper.AddSpace(4);
                    }

                    if (pendingKey != KeyCode.None && !isListeningForHotkey && !string.IsNullOrEmpty(editingHotkey))
                    {
                        var newHotkey = new HotkeyConfig(pendingKey, pendingShift, pendingCtrl, pendingAlt);
                        configManager.SetHotkey(editingHotkey, newHotkey);
                        pendingKey = KeyCode.None;
                        editingHotkey = "";
                    }
                });
                guiHelper.EndCard();

                guiHelper.AddSpace(12);

                guiHelper.BeginCard(width: -1, height: -1);
                guiHelper.CardTitle("Configuration");
                guiHelper.CardContent(() =>
                {
                    if (guiHelper.Button("Save Configuration", ControlVariant.Default, ControlSize.Default))
                        MelonLogger.Msg("Configuration saved");

                    guiHelper.AddSpace(6);

                    if (guiHelper.Button("Reload Configuration", ControlVariant.Default, ControlSize.Default))
                    {
                        configManager.LoadAllConfigs();
                        MelonLogger.Msg("Configuration reloaded");
                    }
                });
                guiHelper.EndCard();

                guiHelper.EndVerticalGroup();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DrawSettingsTab error: {ex.Message}");
            }
        }

        private void DrawToggleButton(string label, Action<bool> setter, Func<bool> getter)
        {
            bool isEnabled = getter();

            if (guiHelper.Toggle(label, isEnabled, ControlVariant.Default, ControlSize.Default, (newValue) => setter(newValue), false))
            {
                // it will do it!
            }
        }

        private void DrawFloatSlider(string label, Action<float> setter, Func<float> getter, float min, float max, string suffix)
        {
            float oldValue = getter();
            guiHelper.BeginHorizontalGroup();
            guiHelper.Label($"{label}: {oldValue:F1}{suffix}", ControlVariant.Default);
            float newValue = GUILayout.HorizontalSlider(oldValue, min, max, GUILayout.ExpandWidth(true));
            guiHelper.EndHorizontalGroup();

            if (newValue != oldValue)
            {
                setter(newValue);
            }
        }

        private void DrawTeleportButton(string label, float distance)
        {
            if (guiHelper.Button(label, ControlVariant.Default, ControlSize.Default))
                movementManager.TeleportForward(distance);
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
                ControlVariant variant = isSelected ? ControlVariant.Secondary : ControlVariant.Ghost;

                if (guiHelper.Button(label, variant, ControlSize.Small))
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

                guiHelper.Label($"Name: {selectedTarget.nickName}", ControlVariant.Default);
                guiHelper.Label($"Type: {(selectedTarget.ActorType == ActorType.Player ? "Player" : "Monster")}", ControlVariant.Default);
                guiHelper.Label($"Actor ID: {selectedTarget.ActorID}", ControlVariant.Default);

                if (localPlayer != null && selectedTarget.ActorID != localPlayer.ActorID)
                {
                    float distance = Vector3.Distance(selectedTarget.transform.position, localPlayer.transform.position);
                    guiHelper.Label($"Distance: {distance:F1}m", ControlVariant.Default);
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

        private void AddCurrency(int amount)
        {
            try
            {
                var hub = UnityEngine.Object.FindObjectOfType<Hub>();
                if (hub != null)
                {
                    var vworld = ReflectionHelper.GetFieldValue(hub, "vworld") ?? ReflectionHelper.GetPropertyValue(hub, "vworld");
                    if (vworld != null)
                    {
                        var roomManager = ReflectionHelper.GetFieldValue(vworld, "_vRoomManager") ?? ReflectionHelper.GetPropertyValue(vworld, "VRoomManager");
                        if (roomManager != null)
                        {
                            var vrooms = ReflectionHelper.GetFieldValue(roomManager, "_vrooms") as IDictionary;
                            if (vrooms != null)
                            {
                                foreach (DictionaryEntry entry in vrooms)
                                {
                                    var room = entry.Value;
                                    if (room == null)
                                        continue;

                                    if (room is MaintenanceRoom mRoom)
                                    {
                                        ReflectionHelper.InvokeMethod(mRoom, "AddCurrency", amount);
                                        MelonLogger.Msg($"[AddCurrency] Added {amount} currency successfully!");
                                        return;
                                    }

                                    var currentVal = ReflectionHelper.GetPropertyValue(room, "Currency");
                                    if (currentVal != null)
                                    {
                                        ReflectionHelper.SetPropertyValue(room, "Currency", (int)currentVal + amount);
                                        MelonLogger.Msg($"[AddCurrency] Added {amount} currency successfully!");
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AddCurrency] Error: {ex.Message}");
            }
        }

        private void LoadItemList()
        {
            try
            {
                cachedItemList.Clear();

                object excelDataManager = null;

                var dataManager = UnityEngine.Object.FindObjectOfType<DataManager>();
                if (dataManager != null)
                {
                    excelDataManager = ReflectionHelper.GetFieldValue(dataManager, "_excelDataManager");
                }

                if (excelDataManager == null)
                {
                    var hub = UnityEngine.Object.FindObjectOfType<Hub>();
                    if (hub != null)
                    {
                        excelDataManager = ReflectionHelper.GetFieldValue(hub, "_excelDataManager") ?? ReflectionHelper.GetPropertyValue(hub, "ExcelDataManager");
                    }
                }

                if (excelDataManager != null)
                {
                    var itemInfoDict = ReflectionHelper.GetPropertyValue(excelDataManager, "ItemInfoDict") as IDictionary;
                    var localizationDict = ReflectionHelper.GetPropertyValue(excelDataManager, "LocalizationDict") as IDictionary;

                    if (itemInfoDict != null)
                    {
                        foreach (DictionaryEntry entry in itemInfoDict)
                        {
                            int masterId = (int)entry.Key;
                            var itemInfo = entry.Value;

                            string displayName = "Unknown";
                            try
                            {
                                var nameKeyField = ReflectionHelper.GetFieldValue(itemInfo, "Name");
                                string nameKey = nameKeyField?.ToString() ?? "";

                                if (localizationDict != null && !string.IsNullOrEmpty(nameKey) && localizationDict.Contains(nameKey))
                                {
                                    var locData = localizationDict[nameKey];
                                    if (locData != null)
                                    {
                                        var koField = ReflectionHelper.GetFieldValue(locData, "ko");
                                        var enField = ReflectionHelper.GetFieldValue(locData, "en");

                                        string koText = koField?.ToString();
                                        string enText = enField?.ToString();

                                        if (!string.IsNullOrEmpty(koText))
                                            displayName = koText;
                                        else if (!string.IsNullOrEmpty(enText))
                                            displayName = enText;
                                        else
                                            displayName = nameKey;
                                    }
                                    else
                                    {
                                        displayName = nameKey;
                                    }
                                }
                                else
                                {
                                    displayName = nameKey;
                                }
                            }
                            catch { }

                            cachedItemList.Add((masterId, displayName));
                        }

                        cachedItemList = cachedItemList.OrderBy(x => x.id).ToList();
                        itemListLoaded = true;
                        MelonLogger.Msg($"[ItemSpawner] Loaded {cachedItemList.Count} items from game data");
                        return;
                    }
                }

                MelonLogger.Warning("[ItemSpawner] Could not load item list - DataManager not found");
                itemListLoaded = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ItemSpawner] LoadItemList error: {ex.Message}");
                itemListLoaded = true;
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

        private void ClearAllMonsters()
        {
            try
            {
                ProtoActor[] allActors = PlayerAPI.GetAllPlayers();
                int count = 0;
                foreach (ProtoActor actor in allActors)
                {
                    if (actor != null && actor.ActorType == ActorType.Monster)
                    {
                        KillActor(actor);
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"ClearAllMonsters error: {ex.Message}");
            }
        }

        private void EnableFly()
        {
            try
            {
                var localPlayer = PlayerAPI.GetLocalPlayer();
                if (localPlayer == null)
                    return;

                var cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = false;
                }

                var rb = localPlayer.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"EnableFly error: {ex.Message}");
            }
        }

        private void DisableFly()
        {
            try
            {
                var localPlayer = PlayerAPI.GetLocalPlayer();
                if (localPlayer == null)
                    return;

                var cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.enabled = true;
                }

                var rb = localPlayer.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"DisableFly error: {ex.Message}");
            }
        }

        private void UpdateFly()
        {
            if (!state.Fly)
                return;

            try
            {
                var localPlayer = PlayerAPI.GetLocalPlayer();
                if (localPlayer == null)
                    return;

                var cam = Camera.main;
                if (cam == null)
                    return;

                Vector3 moveDir = Vector3.zero;
                var keyboard = Keyboard.current;

                Vector3 camForward = cam.transform.forward;
                Vector3 camRight = cam.transform.right;

                if (keyboard.wKey.isPressed)
                    moveDir += camForward;
                if (keyboard.sKey.isPressed)
                    moveDir -= camForward;
                if (keyboard.aKey.isPressed)
                    moveDir -= camRight;
                if (keyboard.dKey.isPressed)
                    moveDir += camRight;
                if (keyboard.spaceKey.isPressed)
                    moveDir += Vector3.up;
                if (keyboard.leftCtrlKey.isPressed)
                    moveDir -= Vector3.up;

                if (moveDir != Vector3.zero)
                {
                    localPlayer.transform.position += moveDir.normalized * state.FlySpeed * Time.deltaTime;
                }
            }
            catch { }
        }

        private void SavePosition(int slot)
        {
            try
            {
                var localPlayer = PlayerAPI.GetLocalPlayer();
                if (localPlayer == null)
                    return;

                Vector3 pos = localPlayer.transform.position;
                switch (slot)
                {
                    case 1:
                        state.SavedPosition1 = pos;
                        break;
                    case 2:
                        state.SavedPosition2 = pos;
                        break;
                    case 3:
                        state.SavedPosition3 = pos;
                        break;
                }
                MelonLogger.Msg($"Saved position {slot}: {pos}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"SavePosition error: {ex.Message}");
            }
        }

        private void LoadPosition(int slot)
        {
            try
            {
                var localPlayer = PlayerAPI.GetLocalPlayer();
                if (localPlayer == null)
                    return;

                Vector3 pos = Vector3.zero;
                switch (slot)
                {
                    case 1:
                        pos = state.SavedPosition1;
                        break;
                    case 2:
                        pos = state.SavedPosition2;
                        break;
                    case 3:
                        pos = state.SavedPosition3;
                        break;
                }

                if (pos == Vector3.zero)
                {
                    MelonLogger.Warning($"Position {slot} not saved yet");
                    return;
                }

                var cc = localPlayer.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;
                localPlayer.transform.position = pos;
                if (cc != null)
                    cc.enabled = true;

            }
            catch (Exception ex)
            {
                MelonLogger.Error($"LoadPosition error: {ex.Message}");
            }
        }

        private void ApplyPlayerScale()
        {
            try
            {
                var localPlayer = PlayerAPI.GetLocalPlayer();
                if (localPlayer == null)
                    return;

                if (state.CustomScale)
                {
                    localPlayer.transform.localScale = Vector3.one * state.PlayerScale;
                }
                else
                {
                    localPlayer.transform.localScale = Vector3.one;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"ApplyPlayerScale error: {ex.Message}");
            }
        }
    }
}
