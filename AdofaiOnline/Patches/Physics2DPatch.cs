using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(Physics2D))]
internal static class Physics2DPatch
{
    public static Collider2D[] forcedOutput = null;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(Physics2D.OverlapPointAll), new Type[] { typeof(Vector2), typeof(int) })]
    internal static bool OverlapPointAllPrefix(ref Collider2D[] __result)
    {
        if (forcedOutput != null)
        {
            __result = forcedOutput;
            forcedOutput = null;
            return false;
        }
        return true;
    }
}