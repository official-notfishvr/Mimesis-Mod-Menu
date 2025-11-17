using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mimesis_Mod_Menu.Core.Config
{
    public class ConfigManager
    {
        private const string MAIN_CATEGORY = "Mimesis Mod Menu";
        private const string HOTKEYS_CATEGORY = "Mimesis Mod Menu Hotkeys";

        private MelonPreferences_Category mainCategory;
        private MelonPreferences_Category hotkeysCategory;

        private Dictionary<string, MelonPreferences_Entry<string>> stringEntries = new Dictionary<string, MelonPreferences_Entry<string>>();
        private Dictionary<string, MelonPreferences_Entry<bool>> boolEntries = new Dictionary<string, MelonPreferences_Entry<bool>>();
        private Dictionary<string, MelonPreferences_Entry<float>> floatEntries = new Dictionary<string, MelonPreferences_Entry<float>>();
        private Dictionary<string, MelonPreferences_Entry<string>> hotkeyEntries = new Dictionary<string, MelonPreferences_Entry<string>>();

        public ConfigManager()
        {
            mainCategory = MelonPreferences.CreateCategory(MAIN_CATEGORY, "Mimesis Mod Menu Configuration");
            hotkeysCategory = MelonPreferences.CreateCategory(HOTKEYS_CATEGORY, "Mimesis Mod Menu Hotkey Configuration");
        }

        public void LoadAllConfigs() { }

        public T GetValue<T>(string key, T defaultValue, string description = "")
        {
            try
            {
                if (typeof(T) == typeof(string))
                {
                    if (!stringEntries.TryGetValue(key, out var entry))
                    {
                        entry = mainCategory.CreateEntry(key, (string)(object)defaultValue, key, description);
                        stringEntries[key] = entry;
                    }
                    return (T)(object)entry.Value;
                }
                else if (typeof(T) == typeof(bool))
                {
                    if (!boolEntries.TryGetValue(key, out var entry))
                    {
                        entry = mainCategory.CreateEntry(key, (bool)(object)defaultValue, key, description);
                        boolEntries[key] = entry;
                    }
                    return (T)(object)entry.Value;
                }
                else if (typeof(T) == typeof(float))
                {
                    if (!floatEntries.TryGetValue(key, out var entry))
                    {
                        entry = mainCategory.CreateEntry(key, (float)(object)defaultValue, key, description);
                        floatEntries[key] = entry;
                    }
                    return (T)(object)entry.Value;
                }
                else
                {
                    MelonLogger.Warning($"Unsupported type {typeof(T).Name} for key {key}");
                    return defaultValue;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting {typeof(T).Name} {key}: {ex.Message}");
                return defaultValue;
            }
        }

        public void SetValue<T>(string key, T value, string description = "")
        {
            try
            {
                if (typeof(T) == typeof(string))
                {
                    if (!stringEntries.TryGetValue(key, out var entry))
                    {
                        entry = mainCategory.CreateEntry(key, (string)(object)value, key, description);
                        stringEntries[key] = entry;
                    }
                    else
                    {
                        entry.Value = (string)(object)value;
                    }
                }
                else if (typeof(T) == typeof(bool))
                {
                    if (!boolEntries.TryGetValue(key, out var entry))
                    {
                        entry = mainCategory.CreateEntry(key, (bool)(object)value, key, description);
                        boolEntries[key] = entry;
                    }
                    else
                    {
                        entry.Value = (bool)(object)value;
                    }
                }
                else if (typeof(T) == typeof(float))
                {
                    if (!floatEntries.TryGetValue(key, out var entry))
                    {
                        entry = mainCategory.CreateEntry(key, (float)(object)value, key, description);
                        floatEntries[key] = entry;
                    }
                    else
                    {
                        entry.Value = (float)(object)value;
                    }
                }
                else
                {
                    MelonLogger.Warning($"Unsupported type {typeof(T).Name} for key {key}");
                    return;
                }

                MelonPreferences.Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting {typeof(T).Name} {key}: {ex.Message}");
            }
        }

        public HotkeyConfig GetHotkey(string feature)
        {
            try
            {
                if (!hotkeyEntries.TryGetValue(feature, out var entry))
                {
                    entry = hotkeysCategory.CreateEntry(feature, "None", feature, "");
                    hotkeyEntries[feature] = entry;
                }

                if (entry != null)
                {
                    return HotkeyConfig.Parse(entry.Value);
                }

                return new HotkeyConfig();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting hotkey {feature}: {ex.Message}");
                return new HotkeyConfig();
            }
        }

        public void SetHotkey(string feature, HotkeyConfig hotkey)
        {
            try
            {
                if (!hotkeyEntries.TryGetValue(feature, out var entry))
                {
                    entry = hotkeysCategory.CreateEntry(feature, hotkey.ToString(), feature, "");
                    hotkeyEntries[feature] = entry;
                }
                else
                {
                    entry.Value = hotkey.ToString();
                }
                MelonPreferences.Save();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error setting hotkey {feature}: {ex.Message}");
            }
        }

        public bool IsHotkeyPressed(string feature)
        {
            return GetHotkey(feature).IsPressed();
        }

        public Dictionary<string, HotkeyConfig> GetAllHotkeys()
        {
            var result = new Dictionary<string, HotkeyConfig>();
            try
            {
                foreach (var entry in hotkeysCategory.Entries)
                {
                    if (entry is MelonPreferences_Entry<string> stringEntry)
                    {
                        result[entry.Identifier] = HotkeyConfig.Parse(stringEntry.Value);
                        hotkeyEntries[entry.Identifier] = stringEntry;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Error getting all hotkeys: {ex.Message}");
            }
            return result;
        }
    }
}
