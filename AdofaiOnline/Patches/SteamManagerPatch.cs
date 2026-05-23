using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(SteamManager))]
internal static class SteamManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SteamManager.Update))]
    internal static void UpdatePostfix(SteamManager __instance)
    {
        if (!__instance.m_bInitialized || Networking.pollGroup == null)
            return;

        IntPtr[] messages = new IntPtr[32];
        int count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(
            Networking.pollGroup.Value,
            messages,
            messages.Length
        );

        for (int i = 0; i < count; i++)
        {
            SteamNetworkingMessage_t msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
            byte[] data = new byte[msg.m_cbSize];
            Marshal.Copy(msg.m_pData, data, 0, data.Length);
            Networking.HandlePacket(data, msg);
            //Marshal.Release(msg.m_pData);
        }
    }
}