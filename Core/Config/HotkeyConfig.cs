using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Mimesis_Mod_Menu.Core.Config
{
    public class HotkeyConfig
    {
        public KeyCode Key { get; set; }
        public bool RequireShift { get; set; }
        public bool RequireCtrl { get; set; }
        public bool RequireAlt { get; set; }

        public HotkeyConfig(KeyCode key = KeyCode.None, bool shift = false, bool ctrl = false, bool alt = false)
        {
            Key = key;
            RequireShift = shift;
            RequireCtrl = ctrl;
            RequireAlt = alt;
        }

        public bool IsPressed()
        {
            try
            {
                if (Key == KeyCode.None)
                    return false;

                var keyboard = Keyboard.current;
                if (keyboard == null)
                    return false;

                string keyName = Key.ToString();
                var targetKey = keyboard.FindKeyOnCurrentKeyboardLayout(keyName);

                if (targetKey == null)
                    return false;

                if (!targetKey.wasPressedThisFrame)
                    return false;

                bool hasShift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                bool hasCtrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
                bool hasAlt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

                bool shiftMatch = RequireShift == hasShift;
                bool ctrlMatch = RequireCtrl == hasCtrl;
                bool altMatch = RequireAlt == hasAlt;

                return shiftMatch && ctrlMatch && altMatch;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"IsPressed error: {ex.Message}");
                return false;
            }
        }

        public override string ToString()
        {
            try
            {
                string result = Key.ToString();
                if (RequireCtrl)
                    result = "Ctrl+" + result;
                if (RequireShift)
                    result = "Shift+" + result;
                if (RequireAlt)
                    result = "Alt+" + result;
                return result;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"ToString error: {ex.Message}");
                return "None";
            }
        }

        public static HotkeyConfig Parse(string str)
        {
            try
            {
                if (string.IsNullOrEmpty(str) || str.Equals("None", StringComparison.OrdinalIgnoreCase))
                    return new HotkeyConfig();

                bool ctrl = str.Contains("Ctrl+");
                bool shift = str.Contains("Shift+");
                bool alt = str.Contains("Alt+");

                string keyPart = str.Replace("Ctrl+", "").Replace("Shift+", "").Replace("Alt+", "").Trim();

                if (Enum.TryParse<KeyCode>(keyPart, true, out var key))
                    return new HotkeyConfig(key, shift, ctrl, alt);

                MelonLogger.Warning($"Failed to parse hotkey: {str} - key part: {keyPart}");
                return new HotkeyConfig();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Parse error: {ex.Message}");
                return new HotkeyConfig();
            }
        }
    }
}
