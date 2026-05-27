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

        if (!Networking.IsConnected)
        {
            // Host
            GameObject hostButton = GameObject.Instantiate(__instance.buttons[0].gameObject, onlineButtons.transform);
#if LAN_BUTTONS
            hostButton.transform.localPosition = __instance.buttons[0].transform.localPosition + (__instance.buttons[1].transform.localPosition - __instance.buttons[0].transform.localPosition) / 2;
#else
            hostButton.transform.localPosition = __instance.buttons[1].transform.localPosition + (__instance.buttons[2].transform.localPosition - __instance.buttons[1].transform.localPosition) / 2;
#endif
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
#if LAN_BUTTONS
            hostClick.onClick.AddListener(() => Networking.Host(7777));
#else
            hostClick.onClick.AddListener(() => Networking.HostSteam());
#endif

#if LAN_BUTTONS
            // Join
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

            joinClick.onClick.AddListener(() => Networking.Connect("127.0.0.1", 7777));
#endif
        }
        else
        {
            // Invite
            GameObject inviteButton = GameObject.Instantiate(__instance.buttons[0].gameObject, onlineButtons.transform);
            inviteButton.transform.localPosition = __instance.buttons[0].transform.localPosition + (__instance.buttons[1].transform.localPosition - __instance.buttons[0].transform.localPosition) / 2;
            inviteButton.transform.Find("fill/1player/sign").gameObject.SetActive(false);
            Transform inviteLabel = inviteButton.transform.Find("fill/1player/onePlayer");
            GameObject.DestroyImmediate(inviteLabel.gameObject.GetComponent<Image>());
            TextMeshProUGUI inviteText = inviteLabel.gameObject.AddComponent<TextMeshProUGUI>();
            inviteText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            inviteText.verticalAlignment = VerticalAlignmentOptions.Middle;
            inviteText.text = "Invite";
            inviteText.fontSize = 25;

            Button inviteClick = inviteButton.GetComponent<Button>();
            inviteClick.onClick = new();
            inviteClick.onClick.AddListener(() => {
                // My game for some reason keeps randomly giving me an invite dialogue when launching the game so idk, add some stuff here for that
                Plugin.Logger.LogInfo("????");
                SteamFriends.ActivateGameOverlayInviteDialog(Networking.LobbyID.Value); // This will work great on LAN without a LobbyID!
            });

            // Leave
            GameObject leaveButton = GameObject.Instantiate(__instance.buttons[0].gameObject, onlineButtons.transform);
            leaveButton.transform.localPosition = __instance.buttons[2].transform.localPosition + (__instance.buttons[3].transform.localPosition - __instance.buttons[2].transform.localPosition) / 2;
            leaveButton.transform.Find("fill/1player/sign").gameObject.SetActive(false);
            Transform leaveLabel = leaveButton.transform.Find("fill/1player/onePlayer");
            GameObject.DestroyImmediate(leaveLabel.gameObject.GetComponent<Image>());
            TextMeshProUGUI leaveText = leaveLabel.gameObject.AddComponent<TextMeshProUGUI>();
            leaveText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            leaveText.verticalAlignment = VerticalAlignmentOptions.Middle;
            leaveText.text = "Leave";
            leaveText.fontSize = 25;

            Button leaveClick = leaveButton.GetComponent<Button>();
            leaveClick.onClick = new();
#if LAN_BUTTONS
            leaveClick.onClick.AddListener(() => Callbacks.Disconnected());
#else
            leaveClick.onClick.AddListener(() => Networking.DisconnectSteam());
#endif
        }

    }
}