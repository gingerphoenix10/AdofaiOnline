#if BEPIN
using BepInEx;
using BepInEx.Logging;
#else
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;
#endif
using HarmonyLib;
using UnityEngine;
using System.Reflection;

namespace AdofaiOnline;

#if BEPIN
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private static readonly Harmony Patcher = new(MyPluginInfo.PLUGIN_GUID);
    public static SteamManager callbackHandler;
    public void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Patcher.PatchAll();
        GameObject.DontDestroyOnLoad(callbackHandler = new GameObject().AddComponent<SteamManager>());
        Callbacks.InitializeCallbacks();
    }
}
#else
public static class Plugin
{
    public static bool IsEnabled { get; private set; }
    public static BepInLogger Logger { get; private set; }
    private static Harmony harmony;

    public static SteamManager callbackHandler;

    internal static void Setup(ModEntry modEntry)
    {
        Logger = new(modEntry.Logger);
        modEntry.OnToggle = OnToggle;

        harmony = new Harmony(modEntry.Info.Id);
    }

    private static bool OnToggle(ModEntry modEntry, bool value) {
        IsEnabled = value;
        if (value) {
            StartMod(modEntry);
        } else {
            StopMod(modEntry);
        }
        return true;
    }

    private static void StartMod(ModEntry modEntry) {
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        GameObject.DontDestroyOnLoad(callbackHandler = new GameObject().AddComponent<SteamManager>());
        Callbacks.InitializeCallbacks();
    }
    private static void StopMod(ModEntry modEntry) {
        harmony.UnpatchAll(modEntry.Info.Id);
        if (callbackHandler != null)
        { 
            GameObject.Destroy(callbackHandler.transform);
            callbackHandler = null;
        }
        Callbacks.ResetCallbacks();
    }
}

public class BepInLogger
{
    public ModEntry.ModLogger logger;
    public BepInLogger(ModEntry.ModLogger logger)
    {
        this.logger = logger;
    }

    public void LogInfo(string message) => logger.Log(message);
    public void LogError(string message) => logger.Error(message);
}
#endif