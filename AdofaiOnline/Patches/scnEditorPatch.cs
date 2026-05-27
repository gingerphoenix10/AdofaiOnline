using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace AdofaiOnline.Patches;

#if EXPERIMENT_CUSTOMS
[HarmonyPatch(typeof(scnEditor))]
internal static class scnEditorPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scnEditor.Awake))]
    internal static bool AwakePrefix(scnEditor __instance)
    {
        if (!Networking.IsConnected)
            return true;
        RDString.LoadLevelEditorFonts();
        scnEditor.instance = __instance;
        __instance.LoadGameScene();
        return false;
    }
}
#endif