using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(scnLevelSelect))]
internal static class scnLevelSelectPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scnLevelSelect.Start))]
    internal static void StartPrefix()
    {
        if (!Networking.IsConnected)
            return;
        GCS.worldEntrance = null;
    }
}
