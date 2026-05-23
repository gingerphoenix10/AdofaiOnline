using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(scrController))]
internal static class scrControllerPatch
{
    public static bool remoteGetReady = false;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrController.Start_Rewind))]
    internal static bool Start_RewindPrefix()
    {
        if ((Networking.connection == null && Networking.listenSocket == null) || !ADOBase.controller.gameworld)
            return true;

        if (remoteGetReady)
        {
            remoteGetReady = false;
            return true;
        }
        Networking.SendToHost(new byte[] { (byte)PacketType.GetReady });
        remoteGetReady = false;
        return true;
    }
}