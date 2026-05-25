using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(PlayerSelect))]
internal static class PlayerSelectPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PlayerSelect.Setup))]
    internal static void SetupPrefix(PlayerSelect __instance)
    {
        GameObject onlineButtons = new GameObject("onlineButtons");
        onlineButtons.transform.SetParent(__instance.transform);
        onlineButtons.transform.localScale = Vector3.one;
        onlineButtons.transform.localPosition = new Vector3(0, -65);

        GameObject hostButton = GameObject.Instantiate(__instance.buttons[0].gameObject, onlineButtons.transform);
        hostButton.transform.localPosition = __instance.buttons[0].transform.localPosition + (__instance.buttons[1].transform.localPosition - __instance.buttons[0].transform.localPosition) / 2;
        hostButton.transform.Find("fill/1player/sign").gameObject.SetActive(false);
        Transform hostLabel = hostButton.transform.Find("fill/1player/onePlayer");
        GameObject.DestroyImmediate(hostLabel.gameObject.GetComponent<Image>());
        TextMeshProUGUI hostText = hostLabel.gameObject.AddComponent<TextMeshProUGUI>();
        hostText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        hostText.verticalAlignment = VerticalAlignmentOptions.Middle;
        hostText.text = "Host";
        hostText.fontSize = 25;

        Button hostClick = hostButton.GetComponent<Button>();
        hostClick.onClick = new();

        if (Networking.IsConnected)
        {
            hostText.text = "Invite";
            hostClick.onClick.AddListener(() => SteamFriends.ActivateGameOverlayInviteDialog(Networking.LobbyID.Value)); // This will work great on LAN without a LobbyID!
        } else
        {
            hostClick.onClick.AddListener(() => Networking.Host(7777));
            //hostClick.onClick.AddListener(() => Networking.HostSteam());
        }

        GameObject joinButton = GameObject.Instantiate(__instance.buttons[0].gameObject, onlineButtons.transform);
        joinButton.transform.localPosition = __instance.buttons[2].transform.localPosition + (__instance.buttons[3].transform.localPosition - __instance.buttons[2].transform.localPosition) / 2;
        joinButton.transform.Find("fill/1player/sign").gameObject.SetActive(false);
        Transform joinLabel = joinButton.transform.Find("fill/1player/onePlayer");
        GameObject.DestroyImmediate(joinLabel.gameObject.GetComponent<Image>());
        TextMeshProUGUI joinText = joinLabel.gameObject.AddComponent<TextMeshProUGUI>();
        joinText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        joinText.verticalAlignment = VerticalAlignmentOptions.Middle;
        joinText.text = "Join";
        joinText.fontSize = 25;

        Button joinClick = joinButton.GetComponent<Button>();
        joinClick.onClick = new();

        if (Networking.IsConnected)
        {
            joinText.text = "Leave";
            joinClick.onClick.AddListener(() => Callbacks.Disconnected());
            //joinClick.onClick.AddListener(() => Networking.DisconnectSteam());
        } else
            joinClick.onClick.AddListener(() => Networking.Connect("192.168.1.222", 7777));
            //joinClick.onClick.AddListener(() => Networking.ConnectSteam(new CSteamID(76561198330113884)));

    }
}