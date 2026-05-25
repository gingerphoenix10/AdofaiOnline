using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AdofaiOnline.Patches;

[HarmonyPatch(typeof(scrPlayer))]
internal static class scrPlayerPatch
{
    public static bool forcedInput = false;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlayer.ValidInputWasTriggered))]
    internal static bool ValidInputWasTriggeredPrefix(scrPlayer __instance, ref bool __result)
    {
        if (forcedInput)
        {
            forcedInput = false;
            __result = true;
            return false;
        }
        if (__instance.playerID != Networking.localPlayer.PlayerID && ADOBase.controller.playerManager.players.Length == Networking.playerCount)
        {
            __result = false;
            return false;
        }

        if (ADOBase.controller.exitingToMainMenu)
        {
            __result = false;
            return false;
        }
        if (ADOBase.controller.paused)
        {
            __result = false;
            return false;
        }
        bool flag = false;
        if (__instance.touchEnabled)
        {
            Touch[] touches = Input.touches;
            for (int i = 0; i < touches.Length; i++)
            {
                Touch touch = touches[i];
                if (touch.phase == TouchPhase.Began && !ADOBase.controller.IsScreenPointInsideUIElements(touch.position))
                {
                    flag = true;
                    break;
                }
            }
        }
        bool flag2 = Input.GetKeyDown(KeyCode.Mouse0) || Input.GetKeyDown(KeyCode.Mouse1);
        bool flag3 = false;
        /*if ((ADOBase.isSwitch && !Application.isEditor) || scrController.coopMode)
        {
            if (RDInput.playerInputs != null && RDInput.playerInputs.Count > __instance.playerID && RDInput.playerInputs[__instance.playerID].Count > 0)
                flag3 = ((!RDC.force4PlayerCoop) ? RDInput.playerInputs[__instance.playerID].Any((RDInputType input) => input.mainPress) : __instance.KeyWasPressedForDebugCoop());
        }
        else*/
        if (ADOBase.isMobile)
        {
            flag3 = (Input.anyKeyDown && !flag2) || flag;
        }
        else
        {
            bool num = Input.anyKeyDown || RDInput.GetMain(ButtonState.IsDown) > 0;
            bool flag4 = flag2 && EventSystem.current.IsPointerOverGameObject();
            if (ADOBase.controller.isCutscene)
            {
                flag4 = false;
            }
            flag3 = num && !flag4;
        }
        if (!flag3)
        {
            __result = false;
            return false;
        }
        __result = __instance.CountValidKeysPressed() > 0;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlayer.CountValidKeysPressed))]
    internal static bool CountValidKeysPressedPrefix(scrPlayer __instance, ref int __result)
    {
        int num = 0;
        __instance.keyLimiterOverCounter = 0;
        if (__instance.touchEnabled)
        {
            Touch[] touches = Input.touches;
            for (int i = 0; i < touches.Length; i++)
            {
                Touch touch = touches[i];
                if (touch.phase == TouchPhase.Began && !ADOBase.controller.IsScreenPointInsideUIElements(touch.position))
                {
                    num++;
                }
            }
        }
        if ((ADOBase.isSwitch && !Application.isEditor) || scrController.coopMode && false)
        {
            if (RDC.force4PlayerCoop)
            {
                num += (__instance.KeyWasPressedForDebugCoop() ? 1 : 0);
            }
            else
            {
                HashSet<RDInputType> source = RDInput.playerInputs[__instance.playerID];
                num += source.Count((RDInputType input) => input.mainPress);
            }
        }
        else
        {
            num += RDInput.mainPressCount;
        }
        if ((States)(object)ADOBase.controller.stateMachine.GetState() == States.PlayerControl)
        {
            int num2 = (ADOBase.controller.unlockKeyLimiter ? 16 : 1000);
            for (int num3 = __instance.downKeysDuration.Count - 1; num3 >= 0; num3--)
            {
                KeyValuePair<AnyKeyCode, float> keyValuePair = __instance.downKeysDuration.ElementAt(num3);
                if (Time.time - keyValuePair.Value >= 0.5f)
                {
                    __instance.downKeysDuration.Remove(keyValuePair.Key);
                }
            }
            foreach (AnyKeyCode mainPressKey in RDInput.GetMainPressKeys())
            {
                bool flag = false;
                if (!__instance.downKeysDuration.ContainsKey(mainPressKey) && __instance.downKeysDuration.Count >= num2)
                {
                    __instance.keyLimiterOverCounter++;
                    flag = true;
                }
                if (!flag)
                {
                    __instance.downKeysDuration[mainPressKey] = Time.time;
                }
                if (mainPressKey.value is KeyCode keyCode)
                {
                    __instance.keyFrequency[keyCode] = (__instance.keyFrequency.ContainsKey(keyCode) ? (__instance.keyFrequency[keyCode] + 1) : 0);
                    __instance.keyTotal++;
                }
                if (mainPressKey.value is AsyncKeyCode asyncKeyCode)
                {
                    __instance.keyFrequency[asyncKeyCode] = (__instance.keyFrequency.ContainsKey(asyncKeyCode) ? (__instance.keyFrequency[asyncKeyCode] + 1) : 0);
                    __instance.keyTotal++;
                }
            }
            ADOBase.controller.maximumUsedKeys = Math.Max(ADOBase.controller.maximumUsedKeys, __instance.downKeysDuration.Count);
        }
        else
        {
            __instance.downKeysDuration.Clear();
            ADOBase.controller.maximumUsedKeys = 0;
        }
        __result = Math.Max(0, num);
        return false;
    }

    public static bool remoteDeath = false;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlayer.Die))]
    internal static bool DiePrefix(scrPlayer __instance, bool overload, bool multipress, string failMessage, bool hitbox)
    {
        if (Networking.connection == null && Networking.listenSocket == null)
            return true;

        if (__instance.playerID == Networking.localPlayer.PlayerID && !__instance.auto)
        {
            byte[] data = new byte[4 + failMessage.Length];
            data[0] = (byte)PacketType.Die;
            data[1] = Convert.ToByte(overload);
            data[2] = Convert.ToByte(multipress);
            data[3] = Convert.ToByte(hitbox);
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(failMessage), 0, data, 4, failMessage.Length);
            Plugin.Logger.LogInfo($"Sending Death: {overload}, {multipress}, {hitbox}, {failMessage}, {__instance.auto}");
            Networking.SendToHost(data);
            return true;
        }
        else if (remoteDeath)
        {
            RDC.auto = false;
            __instance.invincibilityTimer = 0f;
            remoteDeath = false;
            return true;
        }
        return false;
    }

    static int i = 0;
    [HarmonyPostfix]
    [HarmonyPatch(nameof(scrPlayer.Hit))]
    internal static async void HitPostfix(scrPlayer __instance)
    {
        //if (i < 200)
        //{
        //i++;
        //await Task.Delay(10);
        //__instance.planetarySystem.chosenPlanet = __instance.chosenPlanet.SwitchChosen();
        //}
        //else
        //i = 0;
        //__instance.chosenPlanet.SwitchChosen();
    }

    public static scrFloor lastFloor = null;
    [HarmonyPostfix]
    [HarmonyPatch(nameof(scrPlayer.Update))]
    internal static void UpdatePostfix(scrPlayer __instance)
    {
        if (!Networking.IsConnected)
            return;

        if (ADOBase.playerManager.players.Length != Networking.playerCount && !ADOBase.loader.isWipingToBlack
#if EXPERIMENT_CUSTOMS
            && ADOBase.controller.gameworld
#endif
            )
            Networking.ChangePlayerCount(Networking.playerCount);

        if (ADOBase.playerManager.allPlayers[Networking.localPlayer.PlayerID] != __instance)
            return;

        if (!ADOBase.controller.gameworld)
        {
            if (__instance.currFloor != null && __instance.currFloor != lastFloor)
            {
                lastFloor = __instance.currFloor;
                byte[] data = new byte[2 + sizeof(float) * 4 + sizeof(double) * 1]; // 3 floats, plus one byte for packet type, one for level type, and one for current angle

                data[0] = (byte)PacketType.Update;
                data[1] = 0x00;
                Buffer.BlockCopy(BitConverter.GetBytes(__instance.currFloor.transform.position.x), 0, data, 0 * sizeof(float) + 2, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(__instance.currFloor.transform.position.y), 0, data, 1 * sizeof(float) + 2, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(__instance.currFloor.transform.position.z), 0, data, 2 * sizeof(float) + 2, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(__instance.planetarySystem.chosenPlanet.angle), 0, data, 3 * sizeof(float) + 2, sizeof(double));
                Networking.SendToHost(data, Constants.k_nSteamNetworkingSend_Unreliable);
            }
        }
    }

    public static bool remoteRevive = false;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlayer.Revive))]
    internal static bool RevivePrefix(scrPlayer __instance, int floorID, scrPlayer helperPlayer)
    {
        if (!Networking.IsConnected)
            return true;

        if (helperPlayer.playerID == Networking.localPlayer.PlayerID || (helperPlayer == null && Networking.isHost))
        {
            byte[] data = new byte[sizeof(int) + 3];
            data[0] = (byte)PacketType.Revive;
            data[1] = (byte)__instance.playerID;
            Buffer.BlockCopy(BitConverter.GetBytes(floorID), 0, data, 2, sizeof(int));
            Networking.SendToHost(data, Constants.k_nSteamNetworkingSend_Unreliable);
            return true;
        }
        else if (remoteRevive)
        {
            remoteRevive = false;
            return true;
        }
        return false;
    }

    public static bool remoteDamage = false;
    [HarmonyPostfix]
    [HarmonyPatch(nameof(scrPlayer.OnDamage))]
    internal static void OnDamage(scrPlayer __instance, bool multipress = false, bool applyMultipressDamage = false, bool skipDamage = false, HitMargin hitMargin = HitMargin.TooEarly)
    {
        if (!Networking.IsConnected || __instance.playerID != Networking.localPlayer.PlayerID)
            return;

        byte[] data = new byte[5 + 3 * sizeof(float)];
        data[0] = (byte)PacketType.Damage;
        data[1] = Convert.ToByte(multipress);
        data[2] = Convert.ToByte(applyMultipressDamage);
        data[3] = Convert.ToByte(skipDamage);
        data[4] = (byte)hitMargin;
        Buffer.BlockCopy(BitConverter.GetBytes(scrPlanetPatch.lastMiss.x), 0, data, 0 * sizeof(float) + 5, sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(scrPlanetPatch.lastMiss.y), 0, data, 1 * sizeof(float) + 5, sizeof(float));
        Buffer.BlockCopy(BitConverter.GetBytes(scrPlanetPatch.lastMiss.z), 0, data, 2 * sizeof(float) + 5, sizeof(float));

        Networking.SendToHost(data);
    }

}