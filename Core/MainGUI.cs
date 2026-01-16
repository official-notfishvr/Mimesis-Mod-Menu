using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Mimesis_Mod_Menu.Core.Config;
using Mimesis_Mod_Menu.Core.Features;
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
        private int currentTab;
        private TabConfig[] tabs;

        private PickupManager pickupManager;
        private MovementManager movementManager;
        private AutoLootManager autoLootManager;
        private FullbrightManager fullbrightManager;
        private ItemSpawnerManager itemSpawnerManager;

        private bool isListeningForHotkey = false;
        private string activeHotkeyId = "";

        private string itemSpawnIDInput = "1001";
        private string itemSearchFilter = "";
        private Vector2 itemListScrollPosition;
        private List<(int id, string name)> cachedItemList = new List<(int, string)>();
        private bool itemListLoaded = false;

        private bool showMenu = true;
        private float lastMenuToggleTime;
        private const float HOTKEY_COOLDOWN = 0.2f;

        private ProtoActor[] cachedPlayers = new ProtoActor[0];
        private ProtoActor selectedPlayer;
        private float lastPlayerCacheTime;
        private const float PLAYER_CACHE_INTERVAL = 1f;

        public class FeatureState
        {
            public bool GodMode;
            public bool InfiniteStamina;
            public bool NoFallDamage;
            public bool SpeedBoost;
            public float SpeedMultiplier = 2f;
            public bool ESP;
            public float ESPDistance = 150f;
            public bool ESPShowLoot;
            public bool ESPShowPlayers;
            public bool ESPShowMonsters;
            public bool ESPShowInteractors;
            public bool ESPShowNPCs;
            public bool ESPShowFieldSkills;
            public bool ESPShowProjectiles;
            public bool ESPShowAuraSkills;
            public bool AutoLoot;
            public float AutoLootDistance = 50f;
            public bool Fullbright;
            public bool InfiniteDurability;
            public bool InfinitePrice;
            public bool InfiniteGauge;
            public bool ForceBuy;
            public bool ForceRepair;
            public bool InfiniteCurrency;
            public bool Fly;
            public float FlySpeed = 10f;
            public bool DamageMultiplier;
            public float DamageMultiplierValue = 10f;
            public bool CustomScale;
            public float PlayerScale = 1f;
            public Vector3[] SavedPositions = new Vector3[3];
            public Vector3 SavedPosition1;
            public Vector3 SavedPosition2;
            public Vector3 SavedPosition3;
        }

        private readonly FeatureState state = new FeatureState();

        void Start()
        {
            guiHelper = new GUIHelper();
            configManager = new ConfigManager();

            autoLootManager = new AutoLootManager();
            fullbrightManager = new FullbrightManager();
            pickupManager = new PickupManager();
            movementManager = new MovementManager();
            itemSpawnerManager = new ItemSpawnerManager();

            tabs = new TabConfig[] { new TabConfig("Player", DrawPlayerTab), new TabConfig("Combat", DrawCombatTab), new TabConfig("Loot", DrawLootTab), new TabConfig("Visual", DrawVisualTab), new TabConfig("Entities", DrawEntitiesTab), new TabConfig("Settings", DrawSettingsTab) };

            if (configManager.GetHotkey("ToggleMenu").Key == KeyCode.None)
            {
                configManager.SetHotkey("ToggleMenu", new HotkeyConfig(KeyCode.Insert, false, false, false));
            }

            ESPManager.Initialize();
            Patches.ApplyPatches(configManager);

            bool enabled = configManager.GetValue<bool>("Enabled", true);
            if (!enabled)
                showMenu = false;

            MelonLogger.Msg("Mimesis Mod Menu initialized");
        }

        void Update()
        {
            if (configManager == null)
                return;

            autoLootManager?.Update();
            fullbrightManager?.Update();
            pickupManager?.Update();
            movementManager?.Update();
            itemSpawnerManager?.Update();
            if (!configManager.GetValue<bool>("Enabled", true))
                return;

            HandleInput();
        }

        private void HandleInput()
        {
            if (isListeningForHotkey)
                return;

            if (CheckHotkey("ToggleMenu") && Time.time - lastMenuToggleTime > HOTKEY_COOLDOWN)
            {
                showMenu = !showMenu;
                lastMenuToggleTime = Time.time;
            }

            if (!showMenu && !configManager.GetValue<bool>("BackgroundInput", true))
                return;

            if (CheckHotkey("ToggleGodMode"))
                ToggleBool(v => state.GodMode = v, state.GodMode);
            if (CheckHotkey("ToggleInfiniteStamina"))
                ToggleBool(v => state.InfiniteStamina = v, state.InfiniteStamina);
            if (CheckHotkey("ToggleNoFallDamage"))
                ToggleBool(v => state.NoFallDamage = v, state.NoFallDamage);
            if (CheckHotkey("ToggleSpeedBoost"))
                ToggleBool(v => state.SpeedBoost = v, state.SpeedBoost);
            if (CheckHotkey("ToggleESP"))
                ToggleBool(v => state.ESP = v, state.ESP);

            if (CheckHotkey("ToggleAutoLoot"))
            {
                state.AutoLoot = !state.AutoLoot;
                autoLootManager.SetEnabled(state.AutoLoot);
            }

            if (CheckHotkey("ToggleFullbright"))
            {
                state.Fullbright = !state.Fullbright;
                fullbrightManager.SetEnabled(state.Fullbright);
            }

            if (state.Fly)
                UpdateFly();
        }

        private bool CheckHotkey(string name)
        {
            return configManager.GetHotkey(name).IsPressed();
        }

        private void ToggleBool(Action<bool> setter, bool currentValue)
        {
            setter(!currentValue);
        }

        void OnGUI()
        {
            if (!configManager.GetValue<bool>("Enabled", true))
                return;

            GUI.skin.horizontalScrollbar = GUIStyle.none;
            GUI.skin.verticalScrollbar = GUIStyle.none;

            if (isListeningForHotkey)
            {
                HandleHotkeyBindingEvent();
            }

            if (showMenu)
            {
                windowRect = GUI.Window(101, windowRect, DrawWindow, "Mimesis Mod Menu");
            }

            ESPManager.UpdateESP();
            guiHelper.DrawOverlay();
        }

        private void HandleHotkeyBindingEvent()
        {
            Event e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode != KeyCode.None && e.keyCode != KeyCode.Escape)
                {
                    configManager.SetHotkey(activeHotkeyId, new HotkeyConfig(e.keyCode, e.shift, e.control, e.alt));
                }
                isListeningForHotkey = false;
                activeHotkeyId = "";
            }
        }

        void DrawWindow(int windowID)
        {
            guiHelper.UpdateGUI(showMenu);

            if (!guiHelper.BeginGUI())
            {
                GUI.DragWindow();
                return;
            }

            currentTab = guiHelper.VerticalTabs(tabs.Select(t => t.Name).ToArray(), currentTab, DrawActiveTab, maxLines: 1);

            guiHelper.EndGUI();
            GUI.DragWindow();
        }

        void DrawActiveTab()
        {
            scrollPosition = guiHelper.ScrollView(
                scrollPosition,
                () =>
                {
                    guiHelper.BeginVerticalGroup(GUILayout.ExpandHeight(true));
                    tabs[currentTab].Content.Invoke();
                    guiHelper.EndVerticalGroup();
                },
                GUILayout.Height(700)
            );
        }

        void DrawPlayerTab()
        {
            guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            DrawCard(
                "Defense",
                () =>
                {
                    DrawCheckbox("God Mode", v => state.GodMode = v, state.GodMode, "ToggleGodMode");
                    guiHelper.AddSpace(8);
                    DrawCheckbox("No Fall Damage", v => state.NoFallDamage = v, state.NoFallDamage, "ToggleNoFallDamage");
                    guiHelper.AddSpace(8);
                    DrawCheckbox("Infinite Stamina", v => state.InfiniteStamina = v, state.InfiniteStamina, "ToggleInfiniteStamina");
                }
            );

            guiHelper.AddSpace(12);

            DrawCard(
                "Movement",
                () =>
                {
                    DrawCheckbox("Speed Boost", v => state.SpeedBoost = v, state.SpeedBoost, "ToggleSpeedBoost");
                    if (state.SpeedBoost)
                    {
                        guiHelper.AddSpace(8);
                        DrawSlider("Multiplier", v => state.SpeedMultiplier = v, state.SpeedMultiplier, 1f, 5f, "x");
                    }

                    guiHelper.AddSpace(10);

                    DrawCheckbox(
                        "Fly",
                        v =>
                        {
                            state.Fly = v;
                            if (v)
                                EnableFly();
                            else
                                DisableFly();
                        },
                        state.Fly
                    );

                    if (state.Fly)
                    {
                        guiHelper.AddSpace(8);
                        DrawSlider("Fly Speed", v => state.FlySpeed = v, state.FlySpeed, 5f, 50f, "m/s");
                    }

                    guiHelper.AddSpace(10);
                    DrawTeleportControls();
                }
            );

            guiHelper.AddSpace(12);

            DrawCard(
                "Appearance",
                () =>
                {
                    DrawCheckbox(
                        "Custom Scale",
                        v =>
                        {
                            state.CustomScale = v;
                            ApplyPlayerScale();
                        },
                        state.CustomScale
                    );
                    if (state.CustomScale)
                    {
                        guiHelper.AddSpace(8);
                        DrawSlider(
                            "Scale",
                            v =>
                            {
                                state.PlayerScale = v;
                                ApplyPlayerScale();
                            },
                            state.PlayerScale,
                            0.1f,
                            5f,
                            "x"
                        );
                    }
                }
            );

            guiHelper.EndVerticalGroup();
        }

        void DrawCombatTab()
        {
            guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            DrawCard(
                "Damage",
                () =>
                {
                    DrawCheckbox("Damage Multiplier", v => state.DamageMultiplier = v, state.DamageMultiplier);
                    if (state.DamageMultiplier)
                    {
                        guiHelper.AddSpace(8);
                        DrawSlider("Multiplier", v => state.DamageMultiplierValue = v, state.DamageMultiplierValue, 1f, 100f, "x");
                    }
                }
            );

            guiHelper.AddSpace(12);

            DrawCard(
                "Bulk Actions",
                () =>
                {
                    if (guiHelper.Button("Kill All Players", ControlVariant.Destructive, ControlSize.Default))
                        KillAllActors(ActorType.Player);

                    guiHelper.AddSpace(10);

                    if (guiHelper.Button("Kill All Monsters", ControlVariant.Destructive, ControlSize.Default))
                        KillAllActors(ActorType.Monster);

                    guiHelper.AddSpace(10);

                    if (guiHelper.Button("Clear All Monsters", ControlVariant.Destructive, ControlSize.Default))
                        ClearAllMonsters();
                }
            );

            guiHelper.EndVerticalGroup();
        }

        void DrawLootTab()
        {
            guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            DrawCard(
                "Item Collection",
                () =>
                {
                    bool isActive = pickupManager.isActive;
                    if (guiHelper.Button(isActive ? "Stop Picking Up" : "Pickup All Items", isActive ? ControlVariant.Destructive : ControlVariant.Default, ControlSize.Default))
                    {
                        if (isActive)
                            pickupManager.Stop();
                        else
                            pickupManager.StartPickupAll();
                    }
                }
            );

            guiHelper.AddSpace(12);

            DrawCard(
                "Auto Loot",
                () =>
                {
                    DrawCheckbox(
                        "Auto Loot Enabled",
                        v =>
                        {
                            state.AutoLoot = v;
                            autoLootManager.SetEnabled(v);
                        },
                        state.AutoLoot,
                        "ToggleAutoLoot"
                    );

                    if (state.AutoLoot)
                    {
                        guiHelper.AddSpace(8);
                        DrawSlider(
                            "Detection Range",
                            v =>
                            {
                                state.AutoLootDistance = v;
                                autoLootManager.SetDistance(v);
                            },
                            state.AutoLootDistance,
                            10f,
                            200f,
                            "m"
                        );
                    }
                }
            );

            guiHelper.AddSpace(12);

            DrawCard(
                "Equipment & Shop",
                () =>
                {
                    DrawCheckbox("Infinite Durability", v => state.InfiniteDurability = v, state.InfiniteDurability);
                    guiHelper.AddSpace(8);
                    DrawCheckbox("Infinite Gauge", v => state.InfiniteGauge = v, state.InfiniteGauge);
                    guiHelper.AddSpace(8);
                    DrawCheckbox("Infinite Currency", v => state.InfiniteCurrency = v, state.InfiniteCurrency);
                    guiHelper.AddSpace(8);
                    if (guiHelper.Button("Add 10k Currency", ControlVariant.Default, ControlSize.Small))
                        AddCurrency(10000);
                }
            );

            guiHelper.AddSpace(12);
            DrawItemSpawner();
            guiHelper.EndVerticalGroup();
        }

        void DrawVisualTab()
        {
            guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            DrawCard(
                "ESP Settings",
                () =>
                {
                    DrawCheckbox("Enable ESP", v => state.ESP = v, state.ESP, "ToggleESP");
                    if (state.ESP)
                    {
                        guiHelper.AddSpace(10);
                        DrawSlider("Distance", v => state.ESPDistance = v, state.ESPDistance, 50f, 500f, "m");
                        guiHelper.AddSpace(12);

                        guiHelper.BeginHorizontalGroup();

                        guiHelper.BeginVerticalGroup(GUILayout.Width(150));
                        DrawCheckbox("Players", v => state.ESPShowPlayers = v, state.ESPShowPlayers);
                        DrawCheckbox("Monsters", v => state.ESPShowMonsters = v, state.ESPShowMonsters);
                        DrawCheckbox("Loot", v => state.ESPShowLoot = v, state.ESPShowLoot);
                        guiHelper.EndVerticalGroup();

                        guiHelper.BeginVerticalGroup(GUILayout.Width(150));
                        DrawCheckbox("Interactors", v => state.ESPShowInteractors = v, state.ESPShowInteractors);
                        DrawCheckbox("NPCs", v => state.ESPShowNPCs = v, state.ESPShowNPCs);
                        DrawCheckbox("Field Skills", v => state.ESPShowFieldSkills = v, state.ESPShowFieldSkills);
                        guiHelper.EndVerticalGroup();

                        guiHelper.EndHorizontalGroup();
                    }
                }
            );

            guiHelper.AddSpace(12);

            DrawCard(
                "Lighting",
                () =>
                {
                    DrawCheckbox(
                        "Fullbright",
                        v =>
                        {
                            state.Fullbright = v;
                            fullbrightManager.SetEnabled(v);
                        },
                        state.Fullbright,
                        "ToggleFullbright"
                    );
                }
            );

            guiHelper.EndVerticalGroup();
        }

        private void DrawCard(string title, Action content)
        {
            guiHelper.BeginCard(width: -1, height: -1);
            guiHelper.CardTitle(title);
            guiHelper.CardContent(content);
            guiHelper.EndCard();
        }

        private void DrawCheckbox(string label, Action<bool> onChange, bool value, string hotkeyId = null)
        {
            GUILayout.BeginHorizontal();
            bool newValue = guiHelper.Toggle(label, value, ControlVariant.Default, ControlSize.Default, (v) => onChange(v), false);
            if (!string.IsNullOrEmpty(hotkeyId))
            {
                GUILayout.FlexibleSpace();
                guiHelper.MutedLabel($"[{configManager.GetHotkey(hotkeyId)}]");
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSlider(string label, Action<float> onChange, float value, float min, float max, string suffix)
        {
            guiHelper.Label(label);
            float newValue = guiHelper.Slider(value, min, max);
            if (newValue != value)
            {
                onChange(newValue);
            }
            guiHelper.MutedLabel($"{newValue:F1}{suffix}");
        }

        private void DrawTeleportControls()
        {
            GUILayout.BeginHorizontal();
            if (guiHelper.Button("Fwd 50m", ControlVariant.Secondary, ControlSize.Small))
                movementManager.TeleportForward(50f);
            if (guiHelper.Button("Fwd 100m", ControlVariant.Secondary, ControlSize.Small))
                movementManager.TeleportForward(100f);
            GUILayout.EndHorizontal();

            guiHelper.AddSpace(8);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                GUILayout.BeginHorizontal();
                if (guiHelper.Button($"Save Pos {idx + 1}", ControlVariant.Default, ControlSize.Small))
                    SavePosition(idx + 1);
                if (guiHelper.Button($"Load Pos {idx + 1}", ControlVariant.Secondary, ControlSize.Small))
                    LoadPosition(idx + 1);
                GUILayout.EndHorizontal();
                guiHelper.AddSpace(4);
            }
        }

        private void DrawItemSpawner()
        {
            DrawCard(
                "Item Spawner",
                () =>
                {
                    GUILayout.BeginHorizontal();
                    guiHelper.Label("ID:");
                    guiHelper.Label(itemSpawnIDInput);
                    if (guiHelper.Button("Spawn", ControlVariant.Default, ControlSize.Default))
                    {
                        if (int.TryParse(itemSpawnIDInput, out int id))
                            ItemSpawnerPatches.SetItemToSpawn(id, 1);
                    }
                    GUILayout.EndHorizontal();

                    guiHelper.AddSpace(12);

                    if (guiHelper.Button(itemListLoaded ? "Refresh List" : "Load Items", ControlVariant.Secondary, ControlSize.Small))
                    {
                        LoadItemList();
                    }

                    if (itemListLoaded)
                    {
                        guiHelper.AddSpace(4);
                        guiHelper.Label(itemSearchFilter);

                        itemListScrollPosition = GUILayout.BeginScrollView(itemListScrollPosition, GUILayout.Height(200));
                        var filtered = cachedItemList.Where(x => string.IsNullOrEmpty(itemSearchFilter) || x.name.IndexOf(itemSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0).Take(100);

                        foreach (var item in filtered)
                        {
                            if (guiHelper.Button($"{item.name} ({item.id})", ControlVariant.Secondary, ControlSize.Small))
                            {
                                itemSpawnIDInput = item.id.ToString();
                            }
                        }
                        GUILayout.EndScrollView();
                    }
                }
            );
        }

        private void DrawHotkeyButton(string label, string id)
        {
            GUILayout.BeginHorizontal();
            guiHelper.Label(label);
            GUILayout.FlexibleSpace();
            string keyName = configManager.GetHotkey(id).ToString();
            if (guiHelper.Button(keyName, ControlVariant.Secondary, ControlSize.Small))
            {
                activeHotkeyId = id;
                isListeningForHotkey = true;
            }
            GUILayout.EndHorizontal();
            guiHelper.AddSpace(4);
        }

        public FeatureState GetFeatureState() => state;

        void OnDestroy()
        {
            fullbrightManager?.Cleanup();
            ESPManager.Cleanup();
            pickupManager?.Stop();
        }

        void DrawEntitiesTab()
        {
            guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            UpdatePlayerCache();

            guiHelper.BeginHorizontalGroup();

            guiHelper.BeginVerticalGroup(GUILayout.Width(300));
            DrawCard(
                "Entity List",
                () =>
                {
                    guiHelper.MutedLabel($"Total: {cachedPlayers.Length}");
                    guiHelper.AddSpace(8);

                    if (cachedPlayers.Length == 0)
                    {
                        guiHelper.MutedLabel("No entities found");
                    }
                    else
                    {
                        int maxDisplay = Mathf.Min(cachedPlayers.Length, 15);
                        for (int i = 0; i < maxDisplay; i++)
                            DrawActorListItem(cachedPlayers[i]);

                        if (cachedPlayers.Length > maxDisplay)
                            guiHelper.MutedLabel($"...and {cachedPlayers.Length - maxDisplay} more");
                    }
                }
            );
            guiHelper.EndVerticalGroup();

            guiHelper.AddSpace(12);

            guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));
            DrawEntityActionsPanel();
            guiHelper.EndVerticalGroup();

            guiHelper.EndHorizontalGroup();
            guiHelper.EndVerticalGroup();
        }

        private void DrawEntityActionsPanel()
        {
            ProtoActor localPlayer = PlayerAPI.GetLocalPlayer();

            DrawCard(
                "Entity Actions",
                () =>
                {
                    if (selectedPlayer != null)
                    {
                        string actorType = selectedPlayer.ActorType == ActorType.Player ? "Player" : "Monster";
                        guiHelper.MutedLabel($"Target: {selectedPlayer.nickName} ({actorType})");
                    }
                    else
                    {
                        guiHelper.MutedLabel("Select an entity to perform actions");
                    }

                    guiHelper.AddSpace(8);

                    if (selectedPlayer == null)
                    {
                        guiHelper.MutedLabel("No entity selected");
                    }
                    else
                    {
                        DrawActorInfo(selectedPlayer, localPlayer);
                        guiHelper.AddSpace(14);

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
                }
            );
        }

        void DrawSettingsTab()
        {
            guiHelper.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            DrawCard(
                "Hotkeys",
                () =>
                {
                    if (isListeningForHotkey)
                    {
                        guiHelper.Label($"Press any key to bind {activeHotkeyId}...");
                        if (guiHelper.Button("Cancel", ControlVariant.Secondary, ControlSize.Small))
                        {
                            isListeningForHotkey = false;
                            activeHotkeyId = "";
                        }
                    }
                    else
                    {
                        DrawHotkeyButton("Toggle Menu", "ToggleMenu");
                        DrawHotkeyButton("God Mode", "ToggleGodMode");
                        DrawHotkeyButton("ESP", "ToggleESP");
                        DrawHotkeyButton("Auto Loot", "ToggleAutoLoot");
                        DrawHotkeyButton("Fullbright", "ToggleFullbright");
                        DrawHotkeyButton("Speed Boost", "ToggleSpeedBoost");
                        DrawHotkeyButton("Infinite Stamina", "ToggleInfiniteStamina");
                        DrawHotkeyButton("No Fall Damage", "ToggleNoFallDamage");
                    }
                }
            );

            guiHelper.AddSpace(12);

            DrawCard(
                "Configuration",
                () =>
                {
                    if (guiHelper.Button("Save Configuration", ControlVariant.Default, ControlSize.Default))
                    {
                        MelonPreferences.Save();
                        MelonLogger.Msg("Configuration saved");
                    }

                    guiHelper.AddSpace(6);

                    if (guiHelper.Button("Reload Configuration", ControlVariant.Default, ControlSize.Default))
                    {
                        configManager.LoadAllConfigs();
                        MelonLogger.Msg("Configuration reloaded");
                    }
                }
            );

            guiHelper.EndVerticalGroup();
        }

        private void UpdatePlayerCache()
        {
            if (Time.time - lastPlayerCacheTime < PLAYER_CACHE_INTERVAL)
                return;

            ProtoActor[] allActors = PlayerAPI.GetAllPlayers();
            cachedPlayers = allActors.Where(p => p != null && !string.IsNullOrEmpty(p.nickName) && !p.dead).OrderBy(p => p.ActorType).ThenBy(p => p.nickName).ToArray();
            lastPlayerCacheTime = Time.time;
        }

        private void DrawActorListItem(ProtoActor actor)
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

        private void DrawActorInfo(ProtoActor selectedTarget, ProtoActor localPlayer)
        {
            if (selectedTarget == null)
                return;

            guiHelper.MutedLabel($"Name: {selectedTarget.nickName}");
            guiHelper.MutedLabel($"Type: {(selectedTarget.ActorType == ActorType.Player ? "Player" : "Monster")}");
            guiHelper.MutedLabel($"Actor ID: {selectedTarget.ActorID}");

            if (localPlayer != null && selectedTarget.ActorID != localPlayer.ActorID)
            {
                float distance = Vector3.Distance(selectedTarget.transform.position, localPlayer.transform.position);
                guiHelper.MutedLabel($"Distance: {distance:F1}m");
            }
        }

        private void KillActor(ProtoActor target)
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
                                        var enField = ReflectionHelper.GetFieldValue(locData, "en");
                                        string enText = enField?.ToString();

                                        if (!string.IsNullOrEmpty(enText))
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
