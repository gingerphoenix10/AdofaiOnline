using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(PauseMenu))]
internal static class PauseMenuPatch
{
    public static bool remotePause = false;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(PauseMenu.Show))]
    internal static void ShowPostfix()
    {
        if (remotePause || !Networking.IsConnected)
        {
            remotePause = false;
            return;
        }
        byte[] data = new byte[2] { (byte)PacketType.Pause, 0x01 };
        Networking.SendToHost(data);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(PauseMenu.Hide))]
    internal static void HidePostfix()
    {
        if (remotePause || !Networking.IsConnected)
        {
            remotePause = false;
            return;
        }
        byte[] data = new byte[2] { (byte)PacketType.Pause, 0x00 };
        Networking.SendToHost(data);
    }
}
