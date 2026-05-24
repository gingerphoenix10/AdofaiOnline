using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(scrMisc))]
internal class scrMiscPatch
{
    public static HitMargin? forcedMargin;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrMisc.GetHitMargin))]
    internal static bool GetHitMarginPatch(ref HitMargin __result)
    {
        if (forcedMargin.HasValue)
        {
            __result = forcedMargin.Value;
            forcedMargin = null;
            return false;
        }
        return true;
    }
}
