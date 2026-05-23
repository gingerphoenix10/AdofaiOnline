using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(scnSplash))]
internal static class scnSplashPatch
{
#if DEBUG
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scnSplash.Start))]
    internal static bool StartPrefix(scnSplash __instance)
    {
        __instance.GoToMenu();
        return false;
    }
#endif
}