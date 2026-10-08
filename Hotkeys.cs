using BepInEx.Configuration;
using UnityEngine;

namespace Aooni2SpeedRunTool
{
    internal static class Hotkeys
    {
        public static ConfigEntry<KeyCode> NoClip;
        public static ConfigEntry<KeyCode> SaveState;
        public static ConfigEntry<KeyCode> LoadState;
        public static ConfigEntry<KeyCode> NoGameOver;
        public static ConfigEntry<KeyCode> EnemyPath;
        public static ConfigEntry<KeyCode> ResetKeyDisplay;
        public static ConfigEntry<KeyCode> ReloadSave;
        public static ConfigEntry<KeyCode> SpeedUp;
        public static ConfigEntry<KeyCode> SpeedDown;
        public static ConfigEntry<KeyCode> SpeedToggle;
        public static ConfigEntry<KeyCode> SpeedReset;

        public static void Init(ConfigFile cfg)
        {
            const string S = "Hotkeys";
            const string note = " Set to None to disable.";

            NoClip = cfg.Bind(S, "NoClip", KeyCode.F1, "Toggle NoClip." + note);
            SaveState = cfg.Bind(S, "SaveState", KeyCode.F2, "Save state." + note);
            LoadState = cfg.Bind(S, "LoadState", KeyCode.F3, "Load state." + note);
            NoGameOver = cfg.Bind(S, "NoGameOver", KeyCode.F4, "Toggle NoGameOver." + note);
            EnemyPath = cfg.Bind(S, "EnemyPath", KeyCode.F5, "Toggle enemy path display." + note);
            ResetKeyDisplay = cfg.Bind(S, "ResetKeyDisplay", KeyCode.F6, "Reset key display position." + note);
            ReloadSave = cfg.Bind(S, "ReloadSave", KeyCode.F12, "Reload current save slot." + note);
            SpeedUp = cfg.Bind(S, "SpeedUp", KeyCode.KeypadPlus, "Speed +1." + note);
            SpeedDown = cfg.Bind(S, "SpeedDown", KeyCode.KeypadMinus, "Speed -1." + note);
            SpeedToggle = cfg.Bind(S, "SpeedToggle", KeyCode.KeypadMultiply, "Switch custom/original speed." + note);
            SpeedReset = cfg.Bind(S, "SpeedReset", KeyCode.Keypad0, "Reset custom speed." + note);
        }

        /// <summary>這個熱鍵這一幀是否被按下（None 一律 false）。</summary>
        public static bool Down(ConfigEntry<KeyCode> e)
        {
            var k = e.Value;
            return k != KeyCode.None && Input.GetKeyDown(k);
        }

        public static string Name(ConfigEntry<KeyCode> e) => e.Value.ToString();
    }
}