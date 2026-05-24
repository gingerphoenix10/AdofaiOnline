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
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PauseMenu.ShowPlayerSelect))]
    internal static bool ShowPlayerSelectPrefix()
    {
        return true;
        bool isHosting = Networking.listenSocket != null || Networking.connection != null;
        if (!isHosting)
            Networking.Host(7777);
        return isHosting;
    }
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PauseMenu.OpenDiscord))]
    internal static bool OpenDiscordPrefix()
    {
        Networking.Connect("127.0.0.1", 7777);
        return false;
    }

    public static bool remotePause = false;

    [HarmonyPostfix]
    [HarmonyPatch(nameof(PauseMenu.Show))]
    internal static void ShowPostfix()
    {
        if (!remotePause)
        {
            byte[] data = new byte[2] { (byte)PacketType.Pause, 0x01 };
            Networking.SendToHost(data);
        }
        remotePause = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(PauseMenu.Hide))]
    internal static void HidePostfix()
    {
        if (!remotePause)
        {
            byte[] data = new byte[2] { (byte)PacketType.Pause, 0x00 };
            Networking.SendToHost(data);
        }
        remotePause = false;
    }
}
