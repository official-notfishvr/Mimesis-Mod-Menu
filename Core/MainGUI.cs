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
using shadcnui.GUIComponents.Core.Utils;
using shadcnui.GUIComponents.Layout;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mimesis_Mod_Menu.Core
{
    public class MainGUI : MonoBehaviour
    {
        private GUIHelper gui;
        private ConfigManager configManager;
        private Rect windowRect = new Rect(20, 20, 1000, 800);
        private Vector2 scrollPos;
        private int activeTab;

        private PickupManager pickupManager;
        private MovementManager movementManager;
        private AutoLootManager autoLootManager;
        private FullbrightManager fullbrightManager;
        private ItemSpawnerManager itemSpawnerManager;

        private bool listeningForHotkey;
        private string activeHotkeyId = "";

        private string itemSpawnIDInput = "1001";
        private string itemSearchFilter = "";
        private Vector2 itemListScroll;
        private List<(int id, string name)> cachedItems = new List<(int, string)>();
        private bool itemsLoaded;

        private bool showMenu = true;
        private float lastToggleTime;
        private const float TOGGLE_COOLDOWN = 0.2f;

        private ProtoActor[] cachedPlayers = new ProtoActor[0];
        private ProtoActor selectedActor;
        private float lastPlayerRefresh;
        private const float PLAYER_REFRESH_RATE = 1f;

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
            public bool InfiniteCurrency;
            public bool ForceBuy;
            public bool ForceRepair;
            public bool Fly;
            public float FlySpeed = 10f;
            public bool DamageMultiplier;
            public float DamageMultiplierValue = 10f;
            public bool CustomScale;
            public float PlayerScale = 1f;
            public Vector3 SavedPosition1;
            public Vector3 SavedPosition2;
            public Vector3 SavedPosition3;
        }

        private readonly FeatureState state = new FeatureState();

        void Start()
        {
            gui = new GUIHelper();
            configManager = new ConfigManager();
            autoLootManager = new AutoLootManager();
            fullbrightManager = new FullbrightManager();
            pickupManager = new PickupManager();
            movementManager = new MovementManager();
            itemSpawnerManager = new ItemSpawnerManager();

            if (configManager.GetHotkey("ToggleMenu").Key == KeyCode.None)
                configManager.SetHotkey("ToggleMenu", new HotkeyConfig(KeyCode.Insert, false, false, false));

            ESPManager.Initialize();
            Patches.ApplyPatches(configManager);

            if (!configManager.GetValue<bool>("Enabled", true))
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

            if (listeningForHotkey)
                return;

            if (configManager.GetHotkey("ToggleMenu").IsPressed() && Time.time - lastToggleTime > TOGGLE_COOLDOWN)
            {
                showMenu = !showMenu;
                lastToggleTime = Time.time;
            }

            if (!showMenu && !configManager.GetValue<bool>("BackgroundInput", true))
                return;

            if (configManager.GetHotkey("ToggleGodMode").IsPressed())
                state.GodMode = !state.GodMode;
            if (configManager.GetHotkey("ToggleInfiniteStamina").IsPressed())
                state.InfiniteStamina = !state.InfiniteStamina;
            if (configManager.GetHotkey("ToggleNoFallDamage").IsPressed())
                state.NoFallDamage = !state.NoFallDamage;
            if (configManager.GetHotkey("ToggleSpeedBoost").IsPressed())
                state.SpeedBoost = !state.SpeedBoost;
            if (configManager.GetHotkey("ToggleESP").IsPressed())
                state.ESP = !state.ESP;

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

            if (state.Fly)
                RunFly();
        }

        void OnGUI()
        {
            if (!configManager.GetValue<bool>("Enabled", true))
                return;

            GUI.skin.horizontalScrollbar = GUIStyle.none;
            GUI.skin.verticalScrollbar = GUIStyle.none;

            if (listeningForHotkey)
            {
                Event e = Event.current;
                if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None && e.keyCode != KeyCode.Escape)
                    configManager.SetHotkey(activeHotkeyId, new HotkeyConfig(e.keyCode, e.shift, e.control, e.alt));

                if (e.type == EventType.KeyDown)
                {
                    listeningForHotkey = false;
                    activeHotkeyId = "";
                }
            }

            if (showMenu)
                windowRect = GUI.Window(101, windowRect, RenderWindow, "Mimesis Mod Menu");

            ESPManager.UpdateESP();
            gui.DrawOverlay();
        }

        void RenderWindow(int id)
        {
            gui.UpdateGUI(showMenu);

            if (!gui.BeginGUI())
            {
                GUI.DragWindow();
                return;
            }

            string[] tabNames = { "Player", "Combat", "Loot", "Visual", "Entities", "Settings" };

            activeTab = gui.Tabs()
                .Items(tabNames)
                .SelectedIndex(activeTab)
                .Side(TabSide.Left)
                .MaxLines(1)
                .Content(() =>
                {
                    scrollPos = gui.ScrollView(
                        scrollPos,
                        () =>
                        {
                            gui.BeginVerticalGroup(GUILayout.ExpandHeight(true));

                            switch (activeTab)
                            {
                                case 0:
                                    RenderPlayerTab();
                                    break;
                                case 1:
                                    RenderCombatTab();
                                    break;
                                case 2:
                                    RenderLootTab();
                                    break;
                                case 3:
                                    RenderVisualTab();
                                    break;
                                case 4:
                                    RenderEntitiesTab();
                                    break;
                                case 5:
                                    RenderSettingsTab();
                                    break;
                            }

                            gui.EndVerticalGroup();
                        },
                        GUILayout.Height(700)
                    );
                })
                .Render();

            gui.EndGUI();
            GUI.DragWindow();
        }

        void RenderPlayerTab()
        {
            gui.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            gui.Card()
                .Title("Defense")
                .Header(() =>
                {
                    state.GodMode = gui.Switch("God Mode", state.GodMode).OnChange(v => state.GodMode = v).FullRowClick().Render();

                    gui.Space(8);

                    state.NoFallDamage = gui.Switch("No Fall Damage", state.NoFallDamage).OnChange(v => state.NoFallDamage = v).FullRowClick().Render();

                    gui.Space(8);

                    state.InfiniteStamina = gui.Switch("Infinite Stamina", state.InfiniteStamina).OnChange(v => state.InfiniteStamina = v).FullRowClick().Render();
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Movement")
                .Header(() =>
                {
                    state.SpeedBoost = gui.Switch("Speed Boost", state.SpeedBoost).OnChange(v => state.SpeedBoost = v).FullRowClick().Render();

                    if (state.SpeedBoost)
                    {
                        gui.Space(8);
                        gui.Label("Speed Multiplier").Muted().Render();
                        state.SpeedMultiplier = gui.Slider(state.SpeedMultiplier).Range(1f, 5f).ShowValue(true).Format("x{0:F1}").OnChange(v => state.SpeedMultiplier = v).Render();
                    }

                    gui.Space(10);

                    state.Fly = gui.Switch("Fly", state.Fly)
                        .OnChange(v =>
                        {
                            state.Fly = v;
                            if (v)
                                EnableFly();
                            else
                                DisableFly();
                        })
                        .FullRowClick()
                        .Render();

                    if (state.Fly)
                    {
                        gui.Space(8);
                        gui.Label("Fly Speed").Muted().Render();
                        state.FlySpeed = gui.Slider(state.FlySpeed).Range(5f, 50f).ShowValue(true).Format("{0:F1} m/s").OnChange(v => state.FlySpeed = v).Render();
                    }

                    gui.Space(10);
                    gui.Separator().Decorative().Render();
                    gui.Space(8);

                    gui.Row(() =>
                    {
                        if (gui.Button("Fwd 50m").Secondary().Small().Render())
                            movementManager.TeleportForward(50f);

                        gui.Space(6);

                        if (gui.Button("Fwd 100m").Secondary().Small().Render())
                            movementManager.TeleportForward(100f);
                    });

                    gui.Space(8);

                    for (int i = 0; i < 3; i++)
                    {
                        int slot = i + 1;
                        gui.Row(() =>
                        {
                            if (gui.Button($"Save Pos {slot}").Default().Small().Render())
                                SavePosition(slot);
                            gui.Space(6);
                            if (gui.Button($"Load Pos {slot}").Secondary().Small().Render())
                                LoadPosition(slot);
                        });
                        gui.Space(4);
                    }
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Appearance")
                .Header(() =>
                {
                    state.CustomScale = gui.Switch("Custom Scale", state.CustomScale)
                        .OnChange(v =>
                        {
                            state.CustomScale = v;
                            ApplyPlayerScale();
                        })
                        .FullRowClick()
                        .Render();

                    if (state.CustomScale)
                    {
                        gui.Space(8);
                        gui.Label("Player Scale").Muted().Render();
                        state.PlayerScale = gui.Slider(state.PlayerScale)
                            .Range(0.1f, 5f)
                            .ShowValue(true)
                            .Format("{0:F2}x")
                            .OnChange(v =>
                            {
                                state.PlayerScale = v;
                                ApplyPlayerScale();
                            })
                            .Render();
                    }
                })
                .Render();

            gui.EndVerticalGroup();
        }

        void RenderCombatTab()
        {
            gui.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            gui.Card()
                .Title("Damage")
                .Header(() =>
                {
                    state.DamageMultiplier = gui.Switch("Damage Multiplier", state.DamageMultiplier).OnChange(v => state.DamageMultiplier = v).FullRowClick().Render();

                    if (state.DamageMultiplier)
                    {
                        gui.Space(8);
                        gui.Label("Multiplier").Muted().Render();
                        state.DamageMultiplierValue = gui.Slider(state.DamageMultiplierValue).Range(1f, 100f).ShowValue(true).Format("{0:F0}x").OnChange(v => state.DamageMultiplierValue = v).Render();
                    }
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Bulk Actions")
                .Header(() =>
                {
                    if (gui.Button("Kill All Players").Destructive().Render())
                        KillAllActors(ActorType.Player);

                    gui.Space(10);

                    if (gui.Button("Kill All Monsters").Destructive().Render())
                        KillAllActors(ActorType.Monster);

                    gui.Space(10);

                    if (gui.Button("Clear All Monsters").Destructive().Render())
                        ClearAllMonsters();
                })
                .Render();

            gui.EndVerticalGroup();
        }

        void RenderLootTab()
        {
            gui.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            gui.Card()
                .Title("Item Collection")
                .Header(() =>
                {
                    bool active = pickupManager.isActive;
                    if (gui.Button(active ? "Stop Picking Up" : "Pickup All Items").Variant(active ? ControlVariant.Destructive : ControlVariant.Default).Render())
                    {
                        if (active)
                            pickupManager.Stop();
                        else
                            pickupManager.StartPickupAll();
                    }
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Auto Loot")
                .Header(() =>
                {
                    state.AutoLoot = gui.Switch("Auto Loot", state.AutoLoot)
                        .OnChange(v =>
                        {
                            state.AutoLoot = v;
                            autoLootManager.SetEnabled(v);
                        })
                        .FullRowClick()
                        .Render();

                    if (state.AutoLoot)
                    {
                        gui.Space(8);
                        gui.Label("Detection Range").Muted().Render();
                        state.AutoLootDistance = gui.Slider(state.AutoLootDistance)
                            .Range(10f, 200f)
                            .ShowValue(true)
                            .Format("{0:F0}m")
                            .OnChange(v =>
                            {
                                state.AutoLootDistance = v;
                                autoLootManager.SetDistance(v);
                            })
                            .Render();
                    }
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Equipment & Shop")
                .Header(() =>
                {
                    state.InfiniteDurability = gui.Switch("Infinite Durability", state.InfiniteDurability).OnChange(v => state.InfiniteDurability = v).FullRowClick().Render();

                    gui.Space(8);

                    state.InfinitePrice = gui.Switch("Infinite Price", state.InfinitePrice).OnChange(v => state.InfinitePrice = v).FullRowClick().Render();

                    gui.Space(8);

                    state.InfiniteGauge = gui.Switch("Infinite Gauge", state.InfiniteGauge).OnChange(v => state.InfiniteGauge = v).FullRowClick().Render();

                    gui.Space(8);

                    state.InfiniteCurrency = gui.Switch("Infinite Currency", state.InfiniteCurrency).OnChange(v => state.InfiniteCurrency = v).FullRowClick().Render();

                    gui.Space(8);

                    state.ForceBuy = gui.Switch("Force Buy", state.ForceBuy).OnChange(v => state.ForceBuy = v).FullRowClick().Render();

                    gui.Space(8);

                    state.ForceRepair = gui.Switch("Force Repair", state.ForceRepair).OnChange(v => state.ForceRepair = v).FullRowClick().Render();

                    gui.Space(8);

                    if (gui.Button("Add 10k Currency").Default().Small().Render())
                        AddCurrency(10000);
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Item Spawner")
                .Header(() =>
                {
                    gui.Row(() =>
                    {
                        gui.Label("Item ID:").Render();
                        gui.Space(6);
                        itemSpawnIDInput = gui.Input(itemSpawnIDInput).Placeholder("e.g. 1001").OnChange(v => itemSpawnIDInput = v).Render();
                        gui.Space(6);
                        if (gui.Button("Spawn").Default().Render())
                        {
                            if (int.TryParse(itemSpawnIDInput, out int spawnId))
                                ItemSpawnerPatches.SetItemToSpawn(spawnId, 1);
                        }
                    });

                    gui.Space(12);

                    if (gui.Button(itemsLoaded ? "Refresh List" : "Load Items").Secondary().Small().Render())
                        LoadItemList();

                    if (itemsLoaded)
                    {
                        gui.Space(6);
                        itemSearchFilter = gui.Input(itemSearchFilter).Placeholder("Search items...").OnChange(v => itemSearchFilter = v).Render();

                        gui.Space(4);

                        itemListScroll = gui.ScrollView(
                            itemListScroll,
                            () =>
                            {
                                var filtered = cachedItems.Where(x => string.IsNullOrEmpty(itemSearchFilter) || x.name.IndexOf(itemSearchFilter, StringComparison.OrdinalIgnoreCase) >= 0).Take(100);

                                foreach (var item in filtered)
                                {
                                    if (gui.Button($"{item.name} ({item.id})").Secondary().Small().Render())
                                        itemSpawnIDInput = item.id.ToString();
                                }
                            },
                            GUILayout.Height(200)
                        );
                    }
                })
                .Render();

            gui.EndVerticalGroup();
        }

        void RenderVisualTab()
        {
            gui.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            gui.Card()
                .Title("ESP Settings")
                .Header(() =>
                {
                    state.ESP = gui.Switch("Enable ESP", state.ESP).OnChange(v => state.ESP = v).FullRowClick().Render();

                    if (state.ESP)
                    {
                        gui.Space(10);
                        gui.Label("Detection Distance").Muted().Render();
                        state.ESPDistance = gui.Slider(state.ESPDistance).Range(50f, 500f).ShowValue(true).Format("{0:F0}m").OnChange(v => state.ESPDistance = v).Render();

                        gui.Space(12);
                        gui.Separator().Decorative().Render();
                        gui.Space(8);

                        gui.Row(() =>
                        {
                            gui.Column(
                                () =>
                                {
                                    state.ESPShowPlayers = gui.Checkbox("Players", state.ESPShowPlayers).OnChange(v => state.ESPShowPlayers = v).Render();
                                    gui.Space(4);
                                    state.ESPShowMonsters = gui.Checkbox("Monsters", state.ESPShowMonsters).OnChange(v => state.ESPShowMonsters = v).Render();
                                    gui.Space(4);
                                    state.ESPShowLoot = gui.Checkbox("Loot", state.ESPShowLoot).OnChange(v => state.ESPShowLoot = v).Render();
                                },
                                GUILayout.Width(150)
                            );

                            gui.Column(
                                () =>
                                {
                                    state.ESPShowInteractors = gui.Checkbox("Interactors", state.ESPShowInteractors).OnChange(v => state.ESPShowInteractors = v).Render();
                                    gui.Space(4);
                                    state.ESPShowNPCs = gui.Checkbox("NPCs", state.ESPShowNPCs).OnChange(v => state.ESPShowNPCs = v).Render();
                                    gui.Space(4);
                                    state.ESPShowFieldSkills = gui.Checkbox("Field Skills", state.ESPShowFieldSkills).OnChange(v => state.ESPShowFieldSkills = v).Render();
                                    gui.Space(4);
                                    state.ESPShowProjectiles = gui.Checkbox("Projectiles", state.ESPShowProjectiles).OnChange(v => state.ESPShowProjectiles = v).Render();
                                    gui.Space(4);
                                    state.ESPShowAuraSkills = gui.Checkbox("Aura Skills", state.ESPShowAuraSkills).OnChange(v => state.ESPShowAuraSkills = v).Render();
                                },
                                GUILayout.Width(150)
                            );
                        });
                    }
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Lighting")
                .Header(() =>
                {
                    state.Fullbright = gui.Switch("Fullbright", state.Fullbright)
                        .OnChange(v =>
                        {
                            state.Fullbright = v;
                            fullbrightManager.SetEnabled(v);
                        })
                        .FullRowClick()
                        .Render();
                })
                .Render();

            gui.EndVerticalGroup();
        }

        void RenderEntitiesTab()
        {
            gui.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            if (Time.time - lastPlayerRefresh >= PLAYER_REFRESH_RATE)
            {
                cachedPlayers = PlayerAPI.GetAllPlayers().Where(p => p != null && !string.IsNullOrEmpty(p.nickName) && !p.dead).OrderBy(p => p.ActorType).ThenBy(p => p.nickName).ToArray();
                lastPlayerRefresh = Time.time;
            }

            ProtoActor local = PlayerAPI.GetLocalPlayer();

            gui.Row(() =>
            {
                gui.Column(
                    () =>
                    {
                        gui.Card()
                            .Title("Entity List")
                            .Header(() =>
                            {
                                gui.Label($"Total: {cachedPlayers.Length}").Muted().Render();
                                gui.Space(8);

                                if (cachedPlayers.Length == 0)
                                {
                                    gui.Label("No entities found").Muted().Render();
                                }
                                else
                                {
                                    int shown = Mathf.Min(cachedPlayers.Length, 15);
                                    for (int i = 0; i < shown; i++)
                                    {
                                        ProtoActor actor = cachedPlayers[i];
                                        string prefix = actor.ActorType == ActorType.Player ? "[P]" : "[M]";
                                        string name = actor.nickName;
                                        if (local != null && actor.ActorID == local.ActorID)
                                            name += " [YOU]";

                                        bool isSelected = selectedActor != null && selectedActor.ActorID == actor.ActorID;

                                        if (gui.Button($"{prefix} {name}").Variant(isSelected ? ControlVariant.Secondary : ControlVariant.Ghost).Small().Render())
                                        {
                                            selectedActor = actor;
                                        }
                                    }

                                    if (cachedPlayers.Length > shown)
                                        gui.Label($"...and {cachedPlayers.Length - shown} more").Muted().Render();
                                }
                            })
                            .Render();
                    },
                    GUILayout.Width(300)
                );

                gui.Space(12);

                gui.Column(
                    () =>
                    {
                        gui.Card()
                            .Title("Entity Actions")
                            .Header(() =>
                            {
                                if (selectedActor == null)
                                {
                                    gui.Label("Select an entity to perform actions").Muted().Render();
                                }
                                else
                                {
                                    string actorKind = selectedActor.ActorType == ActorType.Player ? "Player" : "Monster";
                                    gui.Label($"Target: {selectedActor.nickName} ({actorKind})").Muted().Render();

                                    if (local != null && selectedActor.ActorID != local.ActorID)
                                    {
                                        float dist = Vector3.Distance(selectedActor.transform.position, local.transform.position);
                                        gui.Label($"Distance: {dist:F1}m").Muted().Render();
                                    }

                                    gui.Label($"Actor ID: {selectedActor.ActorID}").Muted().Render();
                                    gui.Space(14);

                                    if (local != null)
                                    {
                                        if (gui.Button("Teleport To Target").Default().Render())
                                            movementManager.TeleportToPlayer(selectedActor);

                                        if (selectedActor.ActorID != local.ActorID)
                                        {
                                            gui.Space(8);
                                            if (gui.Button("Teleport Target To Me").Default().Render())
                                                movementManager.TeleportPlayerToSelf(selectedActor);

                                            gui.Space(8);
                                            if (gui.Button(selectedActor.ActorType == ActorType.Player ? "Kill Player" : "Kill Monster").Destructive().Render())
                                            {
                                                KillActor(selectedActor);
                                            }
                                        }
                                    }
                                }
                            })
                            .Render();
                    },
                    GUILayout.ExpandWidth(true)
                );
            });

            gui.EndVerticalGroup();
        }

        void RenderSettingsTab()
        {
            gui.BeginVerticalGroup(GUILayout.ExpandWidth(true));

            gui.Card()
                .Title("Hotkeys")
                .Header(() =>
                {
                    if (listeningForHotkey)
                    {
                        gui.Label($"Press any key to bind '{activeHotkeyId}'...").Render();
                        gui.Space(8);
                        if (gui.Button("Cancel").Secondary().Small().Render())
                        {
                            listeningForHotkey = false;
                            activeHotkeyId = "";
                        }
                    }
                    else
                    {
                        string[] hotkeyIds = { "ToggleMenu", "ToggleGodMode", "ToggleESP", "ToggleAutoLoot", "ToggleFullbright", "ToggleSpeedBoost", "ToggleInfiniteStamina", "ToggleNoFallDamage" };

                        string[] hotkeyLabels = { "Toggle Menu", "God Mode", "ESP", "Auto Loot", "Fullbright", "Speed Boost", "Infinite Stamina", "No Fall Damage" };

                        for (int i = 0; i < hotkeyIds.Length; i++)
                        {
                            string hid = hotkeyIds[i];
                            string hlabel = hotkeyLabels[i];
                            string keyName = configManager.GetHotkey(hid).ToString();

                            gui.Row(() =>
                            {
                                gui.Label(hlabel).Render();
                                gui.Flex();
                                if (gui.Button(keyName).Secondary().Small().Render())
                                {
                                    activeHotkeyId = hid;
                                    listeningForHotkey = true;
                                }
                            });
                            gui.Space(4);
                        }
                    }
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Configuration")
                .Header(() =>
                {
                    if (gui.Button("Save Configuration").Default().Render())
                    {
                        MelonPreferences.Save();
                        MelonLogger.Msg("Configuration saved");
                    }

                    gui.Space(6);

                    if (gui.Button("Reload Configuration").Default().Render())
                    {
                        configManager.LoadAllConfigs();
                        MelonLogger.Msg("Configuration reloaded");
                    }
                })
                .Render();

            gui.Space(12);

            gui.Card()
                .Title("Appearance")
                .Header(() =>
                {
                    gui.Label("Theme").Muted().Render();
                    gui.Space(4);
                    gui.ThemeChanger().ShowPreview(true).Render();

                    gui.Space(10);

                    gui.Label("Font").Muted().Render();
                    gui.Space(4);
                    gui.FontChanger().ShowPreview(true).OnChange(f => gui.SetFont(f)).Render();
                })
                .Render();

            gui.EndVerticalGroup();
        }

        public FeatureState GetFeatureState() => state;

        void OnDestroy()
        {
            fullbrightManager?.Cleanup();
            ESPManager.Cleanup();
            pickupManager?.Stop();
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

        private void KillAllActors(ActorType type)
        {
            try
            {
                foreach (ProtoActor actor in PlayerAPI.GetAllPlayers())
                    if (actor != null && actor.ActorType == type && !actor.dead)
                        KillActor(actor);
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
                foreach (ProtoActor actor in PlayerAPI.GetAllPlayers())
                    if (actor != null && actor.ActorType == ActorType.Monster)
                        KillActor(actor);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"ClearAllMonsters error: {ex.Message}");
            }
        }

        private void AddCurrency(int amount)
        {
            try
            {
                var hub = UnityEngine.Object.FindObjectOfType<Hub>();
                if (hub == null)
                    return;
                var vworld = ReflectionHelper.GetFieldValue(hub, "vworld") ?? ReflectionHelper.GetPropertyValue(hub, "vworld");
                if (vworld == null)
                    return;
                var roomMgr = ReflectionHelper.GetFieldValue(vworld, "_vRoomManager") ?? ReflectionHelper.GetPropertyValue(vworld, "VRoomManager");
                if (roomMgr == null)
                    return;
                var vrooms = ReflectionHelper.GetFieldValue(roomMgr, "_vrooms") as IDictionary;
                if (vrooms == null)
                    return;
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
                    var cur = ReflectionHelper.GetPropertyValue(room, "Currency");
                    if (cur != null)
                    {
                        ReflectionHelper.SetPropertyValue(room, "Currency", (int)cur + amount);
                        MelonLogger.Msg($"[AddCurrency] Added {amount} currency successfully!");
                        return;
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
                cachedItems.Clear();
                object excelData = null;

                var dm = UnityEngine.Object.FindObjectOfType<DataManager>();
                if (dm != null)
                    excelData = ReflectionHelper.GetFieldValue(dm, "_excelDataManager");

                if (excelData == null)
                {
                    var hub = UnityEngine.Object.FindObjectOfType<Hub>();
                    if (hub != null)
                        excelData = ReflectionHelper.GetFieldValue(hub, "_excelDataManager") ?? ReflectionHelper.GetPropertyValue(hub, "ExcelDataManager");
                }

                if (excelData != null)
                {
                    var itemDict = ReflectionHelper.GetPropertyValue(excelData, "ItemInfoDict") as IDictionary;
                    var locDict = ReflectionHelper.GetPropertyValue(excelData, "LocalizationDict") as IDictionary;

                    if (itemDict != null)
                    {
                        foreach (DictionaryEntry entry in itemDict)
                        {
                            int masterId = (int)entry.Key;
                            var info = entry.Value;
                            string displayName = "Unknown";

                            try
                            {
                                string nameKey = ReflectionHelper.GetFieldValue(info, "Name")?.ToString() ?? "";
                                if (locDict != null && !string.IsNullOrEmpty(nameKey) && locDict.Contains(nameKey))
                                {
                                    var loc = locDict[nameKey];
                                    string en = ReflectionHelper.GetFieldValue(loc, "en")?.ToString();
                                    displayName = !string.IsNullOrEmpty(en) ? en : nameKey;
                                }
                                else
                                {
                                    displayName = nameKey;
                                }
                            }
                            catch { }

                            cachedItems.Add((masterId, displayName));
                        }

                        cachedItems = cachedItems.OrderBy(x => x.id).ToList();
                        itemsLoaded = true;
                        MelonLogger.Msg($"[ItemSpawner] Loaded {cachedItems.Count} items");
                        return;
                    }
                }

                MelonLogger.Warning("[ItemSpawner] Could not load item list");
                itemsLoaded = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ItemSpawner] Error: {ex.Message}");
                itemsLoaded = true;
            }
        }

        private void EnableFly()
        {
            try
            {
                var p = PlayerAPI.GetLocalPlayer();
                if (p == null)
                    return;
                var cc = p.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;
                var rb = p.GetComponent<Rigidbody>();
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
                var p = PlayerAPI.GetLocalPlayer();
                if (p == null)
                    return;
                var cc = p.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = true;
                var rb = p.GetComponent<Rigidbody>();
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

        private void RunFly()
        {
            if (!state.Fly)
                return;
            try
            {
                var p = PlayerAPI.GetLocalPlayer();
                if (p == null)
                    return;
                var cam = Camera.main;
                if (cam == null)
                    return;
                var kb = Keyboard.current;
                Vector3 dir = Vector3.zero;
                if (kb.wKey.isPressed)
                    dir += cam.transform.forward;
                if (kb.sKey.isPressed)
                    dir -= cam.transform.forward;
                if (kb.aKey.isPressed)
                    dir -= cam.transform.right;
                if (kb.dKey.isPressed)
                    dir += cam.transform.right;
                if (kb.spaceKey.isPressed)
                    dir += Vector3.up;
                if (kb.leftCtrlKey.isPressed)
                    dir -= Vector3.up;
                if (dir != Vector3.zero)
                    p.transform.position += dir.normalized * state.FlySpeed * Time.deltaTime;
            }
            catch { }
        }

        private void SavePosition(int slot)
        {
            try
            {
                var p = PlayerAPI.GetLocalPlayer();
                if (p == null)
                    return;
                Vector3 pos = p.transform.position;
                if (slot == 1)
                    state.SavedPosition1 = pos;
                else if (slot == 2)
                    state.SavedPosition2 = pos;
                else if (slot == 3)
                    state.SavedPosition3 = pos;
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
                var p = PlayerAPI.GetLocalPlayer();
                if (p == null)
                    return;
                Vector3 pos =
                    slot == 1 ? state.SavedPosition1
                    : slot == 2 ? state.SavedPosition2
                    : state.SavedPosition3;
                if (pos == Vector3.zero)
                {
                    MelonLogger.Warning($"Position {slot} not saved yet");
                    return;
                }
                var cc = p.GetComponent<CharacterController>();
                if (cc != null)
                    cc.enabled = false;
                p.transform.position = pos;
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
                var p = PlayerAPI.GetLocalPlayer();
                if (p == null)
                    return;
                p.transform.localScale = state.CustomScale ? Vector3.one * state.PlayerScale : Vector3.one;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"ApplyPlayerScale error: {ex.Message}");
            }
        }
    }
}
