using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using HarmonyLib.Tools;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace Aooni2SpeedRunTool;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log;
    internal static Harmony harmony = new Harmony("SpeedRunTool");

    public override void Load()
    {
        // Plugin startup logic
        Log = base.Log;

        ClassInjector.RegisterTypeInIl2Cpp<SpeedRunTool>();

        var host = new GameObject(nameof(SpeedRunTool))
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        Object.DontDestroyOnLoad(host);
        host.AddComponent<SpeedRunTool>();

        HarmonyFileLog.Enabled = true;
        harmony.PatchAll(typeof(Patch));

        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }
}
