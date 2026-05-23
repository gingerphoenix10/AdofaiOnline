using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(scrPlanet))]
internal static class scrPlanetPatch
{
    /*
    [HarmonyTranspiler]
    [HarmonyPatch(nameof(scrPlanet.Update_RefreshAngles))]
    internal static IEnumerable<CodeInstruction> Update_RefreshAnglesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var angleField = AccessTools.Field(typeof(scrPlanet), "angle");

        foreach (var code in instructions)
        {
            if (code.opcode == OpCodes.Stfld &&
                Equals(code.operand, angleField))
            {
                // stfld consumes:
                //   object ref
                //   value
                // replace with pops
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Pop);
                continue;
            }

            yield return code;
        }
    }
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlanet.Update_RefreshAngles))]
    internal static void Update_RefreshAnglesPrefix(scrPlanet __instance)
    {
        
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(scrPlanet.SwitchChosen))]
    internal static IEnumerable<CodeInstruction> SwitchChosenTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions;
    }

    */
    public static Vector3? forcedTilePos = null;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlanet.SnappedCardinalDirection))]
    internal static bool SnappedCardinalDirectionPrefix(scrPlanet __instance, ref Vector3 __result)
    {
        if (forcedTilePos != null)
        {
            __result = (Vector3)forcedTilePos;
            forcedTilePos = null;
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlanet.MoveToNextFloor))]
    internal static void MoveToNextFloorPrefix(scrPlanet __instance, scrFloor floor, float exitAngle, HitMargin hitMargin)
    {
        if (Networking.connection == null && Networking.listenSocket == null)
            return;

        if (__instance.player.playerID == Networking.localPlayer.PlayerID)
        {
            byte[] data = new byte[3 + sizeof(int) + sizeof(float)];
            data[0] = (byte)PacketType.Update;
            data[1] = 0x01;
            data[2] = (byte)hitMargin;
            Buffer.BlockCopy(BitConverter.GetBytes(floor.seqID), 0, data, 3, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(exitAngle), 0, data, 3 + sizeof(int), sizeof(float));
            Networking.SendToHost(data);
        }
    }
}
