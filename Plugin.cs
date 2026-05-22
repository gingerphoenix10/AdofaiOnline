using DG.Tweening;
using HarmonyLib;
using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static NewsSign;
using static UnityEngine.Analytics.IAnalytic;

namespace AdofaiOnline;

public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;
    private static readonly Harmony Patcher = new(MyPluginInfo.PLUGIN_GUID);
    public Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;
    public Callback<LobbyCreated_t> lobbyCreated;
    public Callback<GameLobbyJoinRequested_t> joinRequested;
    public Callback<LobbyEnter_t> lobbyEntered;
    public static CSteamID LobbyID;
    public static Dictionary<HSteamNetConnection, PlayerInfo> clients = new();
    public static byte playerCount = 1;
    public static PlayerInfo localPlayer = new(0x00);
    public static bool isHost = true;
    private async void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        Patcher.PatchAll();
        DontDestroyOnLoad(new GameObject("steam").AddComponent<SteamManager>());
        statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    private void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
    {
        switch (callback.m_info.m_eState)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                {
                    Debug.Log("Incoming connection");

                    SteamNetworkingSockets.AcceptConnection(callback.m_hConn);

                    SteamNetworkingSockets.SetConnectionPollGroup(
                        callback.m_hConn,
                        (HSteamNetPollGroup)pollGroup
                    );

                    break;
                }

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                {
                    Debug.Log("Connected!");
                    if (isHost)
                        return;
                    ADOBase.controller.Restart();
                    byte[] data = new byte[1] { (byte)PacketType.Welcome };
                    SendToHost(data);
                    break;
                }

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                {
                    Debug.Log("Disconnected");
                    if (isHost)
                    {
                        clients.Remove(callback.m_hConn);
                        SteamNetworkingSockets.CloseConnection(
                            callback.m_hConn,
                            0,
                            "Closing",
                            false
                        );

                        byte[] sendDataExisting = new byte[3]
                        {
                            (byte)PacketType.CountChanged,
                            localPlayer.PlayerID,
                            (byte)(clients.Count+1)
                        };
                        PacketEvent(sendDataExisting);
                        SendToHost(sendDataExisting);
                    }
                    else
                    {
                        connection = null;
                        pollGroup = null;
                        ChangePlayerCount(1);
                        localPlayer = new(0x00);
                    }
                    ADOBase.controller.Restart();
                    break;
                }
        }
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"Lobby creation failed: {callback.m_eResult}");
            return;
        }

        LobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        Plugin.Logger.LogInfo($"Lobby created: {LobbyID}");

        SteamFriends.ActivateGameOverlayInviteDialog(LobbyID);
    }

    private static void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private static void OnLobbyEntered(LobbyEnter_t callback)
    {
        CSteamID lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        CSteamID hostId =
            SteamMatchmaking.GetLobbyOwner(lobbyId);

        if (!isHost)
            ConnectSteam(hostId);
    }

    // host stuff
    public static HSteamListenSocket? listenSocket = null;
    public static HSteamNetPollGroup? pollGroup = null;

    public static void Host(ushort port)
    {
        isHost = true;
        SteamNetworkingIPAddr addr = new SteamNetworkingIPAddr();
        addr.Clear();
        addr.m_port = port;

        listenSocket = SteamNetworkingSockets.CreateListenSocketIP(ref addr, 0, null);

        pollGroup = SteamNetworkingSockets.CreatePollGroup();

        Debug.Log($"Hosting on port {port}");
        ADOBase.controller.Restart();
    }

    public static void HostSteam(int virtualPort)
    {
        isHost = true;

        listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);

        pollGroup = SteamNetworkingSockets.CreatePollGroup();

        Debug.Log($"Hosting on virtual port {virtualPort}");

        SteamAPICall_t createLobbyCall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
        //ADOBase.controller.Restart();
    }

    public static HSteamNetConnection? connection = null;

    public static void Connect(string ip, ushort port)
    {
        isHost = false;
        SteamNetworkingIPAddr addr = new SteamNetworkingIPAddr();

        addr.ParseString(ip);
        addr.m_port = port;

        connection = SteamNetworkingSockets.ConnectByIPAddress(ref addr, 0, null);
        pollGroup = SteamNetworkingSockets.CreatePollGroup();

        Debug.Log("Connecting...");
    }

    public static void ConnectSteam(CSteamID hostSteamId)
    {
        isHost = false;

        SteamNetworkingIdentity identity = new SteamNetworkingIdentity();
        identity.SetSteamID(hostSteamId);

        connection = SteamNetworkingSockets.ConnectP2P(ref identity, 0, 0, null);
        pollGroup = SteamNetworkingSockets.CreatePollGroup();

        if (connection == HSteamNetConnection.Invalid)
        {
            Debug.LogError("Failed to create P2P connection");
            return;
        }

        Debug.Log($"Connecting to host: {hostSteamId}");
    }

    public static void SendToHost(byte[] data, int flags = Constants.k_nSteamNetworkingSend_Reliable)
    {
        if (isHost)
        {
            HandlePacket(data, new SteamNetworkingMessage_t { m_nFlags = flags });
        } else
        {
            SendToConnection((HSteamNetConnection)Plugin.connection, data, flags, out _);
        }
    }

    public static void HandlePacket(byte[] data, SteamNetworkingMessage_t msg)
    {
        if (data.Length == 0)
            return;

        Plugin.Logger.LogInfo(BitConverter.ToString(data));

        if (data[0] == (byte)PacketType.Welcome)
        {
            if (Plugin.isHost)
            {
                int playerId = -1;
                for (int i = 1; i < 4; i++)
                {
                    Plugin.Logger.LogInfo($"Checking {i}");
                    bool available = true;
                    foreach (PlayerInfo player in clients.Values)
                    {
                        if (player.PlayerID == i)
                        {
                            available = false;
                            break;
                        }
                    }
                    if (available)
                    {
                        playerId = i;
                        break;
                    }
                }
                if (playerId == -1)
                {
                    Plugin.Logger.LogInfo("No space. Kicking");
                    SteamNetworkingSockets.CloseConnection(msg.m_conn, 1, "Lobby full", false);
                    return;
                }
                clients.Add(msg.m_conn, new PlayerInfo((byte)playerId));

                byte[] sendDataNew = new byte[3]
                {
                        (byte)PacketType.Welcome,
                        clients[msg.m_conn].PlayerID,
                        (byte)(clients.Count+1)
                };
                SendToConnection(msg.m_conn, sendDataNew, Constants.k_nSteamNetworkingSend_Reliable, out _);

                byte[] sendDataExisting = new byte[3]
                {
                        (byte)PacketType.CountChanged,
                        localPlayer.PlayerID,
                        (byte)(clients.Count+1)
                };

                PacketEvent(sendDataExisting);
                foreach (HSteamNetConnection client in clients.Keys)
                {
                    if (client == msg.m_conn)
                        continue;
                    SendToConnection(client, sendDataNew, msg.m_nFlags, out _);
                }

            } else
            {
                byte playerNum = data[1];
                Logger.LogInfo($"Joined! Player ID {playerNum}");
                localPlayer.PlayerID = playerNum;
                ChangePlayerCount(data[2]);
            }
            return;
        }

        if (isHost)
        {
            PlayerInfo plr = localPlayer;
            if (Plugin.clients.TryGetValue(msg.m_conn, out PlayerInfo plrTemp))
                plr = plrTemp;

            byte[] newData = new byte[data.Length+1];
            newData[0] = data[0];
            newData[1] = plr.PlayerID;
            Buffer.BlockCopy(data, 1, newData, 2, data.Length - 1);
            if (plr.PlayerID != localPlayer.PlayerID/* || newData[0] == (byte)PacketType.GetReady*/)
                PacketEvent(newData);
            foreach (HSteamNetConnection client in clients.Keys)
            {
                if (client == msg.m_conn/* && newData[0] != (byte)PacketType.GetReady*/)
                    continue;
                SendToConnection(client, newData, msg.m_nFlags, out _);
            }
        } else
        {
            PacketEvent(data);
        }
    }

    public static void PacketEvent(byte[] data)
    {
        scrPlayer plr = ADOBase.playerManager.allPlayers[(int)data[1]];
        switch (data[0])
        {
            case (byte)PacketType.Update:
                if (data[1] == Plugin.localPlayer.PlayerID)
                    break;

                if (!ADOBase.controller.gameworld && data[2] == 0x00)
                {
                    Plugin.Logger.LogInfo($"Update from {data[1]}");

                    Vector3 pos;

                    float x = BitConverter.ToSingle(data, 3 + 0 * sizeof(float));
                    float y = BitConverter.ToSingle(data, 3 + 1 * sizeof(float));
                    float z = BitConverter.ToSingle(data, 3 + 2 * sizeof(float));

                    pos = new Vector3(x, y, z);
                    Plugin.Logger.LogInfo(pos);
                    PlanetarySystem planets = plr.planetarySystem;
                    scrPlanet planet = planets.chosenPlanet;
                    if (planet != null)
                    {
                        scrFloor flr = RDUtils.GetFloorAtPosition(pos);
                        if (planet.currfloor != flr && flr != null)
                        {
                            scrPlanetPatch.forcedTilePos = pos;
                            Plugin.Logger.LogInfo(flr.transform.GetComponent<Collider2D>());
                            Physics2DPatch.forcedOutput = new Collider2D[] { flr.transform.GetComponent<Collider2D>() };
                            plr.Hit();
                            //planet.transform.position = flr.transform.position;
                            planet.currfloor = flr;
                        }
                    }
                } else if (ADOBase.controller.gameworld && data[2] == 0x01)
                {
                    HitMargin margin = (HitMargin)data[3];
                    int seqId = BitConverter.ToInt32(data, 4);
                    float exitAngle = BitConverter.ToSingle(data, 4+sizeof(int));
                    Plugin.Logger.LogInfo($"Floor {seqId}, exitAngle {exitAngle}");

                    scrFloor floor = ADOBase.lm.listFloors[seqId];
                    //plr.planetarySystem.chosenPlanet.MoveToNextFloor(floor, exitAngle, margin);
                    plr.planetarySystem.chosenPlanet.currfloor = floor.prevfloor;
                    bool prevInfMargin = ADOBase.controller.noFailInfiniteMargin;
                    ADOBase.controller.noFailInfiniteMargin = true;
                    plr.Hit();
                    ADOBase.controller.noFailInfiniteMargin = prevInfMargin;
                }
                break;
            case (byte)PacketType.CountChanged:
                if (data[1] != 0x00)
                    break;
                ChangePlayerCount(data[2]);
                break;
            case (byte)PacketType.Die:
                scrPlayerPatch.remoteDeath = true;
                plr.Die();
                break;
            case (byte)PacketType.ChangeScene:
                SceneManagerPatch.remote = true;
                LoadSceneMode mode = (LoadSceneMode)data[2];
                byte[] sceneNameArray = new byte[data.Length - 3];
                Buffer.BlockCopy(data, 3, sceneNameArray, 0, sceneNameArray.Length);
                SceneManager.LoadScene(Encoding.UTF8.GetString(sceneNameArray), mode);
                break;
            case (byte)PacketType.Revive:
                scrPlayer revivingPlayer = ADOBase.playerManager.allPlayers[(int)data[2]];

                int revivingTile = BitConverter.ToInt32(data, 3);

                scrPlayerPatch.remoteRevive = true;
                revivingPlayer.Revive(revivingTile, plr);
                break;
            case (byte)PacketType.GetReady:
                //ControllerPatch.StartGame();
                //!levelWasSkipped && (!playerManager.AnyValidInputWasTriggered() || isCutscene)
                ControllerPatch.remoteGetReady = true;
                ADOBase.controller.levelWasSkipped = true;
                break;
            case (byte)PacketType.SetLevel:
                byte[] levelNameChars = new byte[data.Length - 2];
                Buffer.BlockCopy(data, 2, levelNameChars, 0, data.Length - 2);
                GCS.internalLevelName = Encoding.UTF8.GetString(levelNameChars);
                break;
        }
    }

    public static void SendToConnection(HSteamNetConnection hConn, byte[] data, int nSendFlags, out long pOutMessageNumber)
    {
        IntPtr dataPointer = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, dataPointer, data.Length);
        SteamNetworkingSockets.SendMessageToConnection(hConn, dataPointer, (uint)data.Length, nSendFlags, out pOutMessageNumber);
        Marshal.FreeHGlobal(dataPointer);
    }

    public static void ChangePlayerCount(byte count)
    {
        playerCount = count;
        scrPlayerManager.SetPlayerCount(count);
        Plugin.Logger.LogInfo("Changed");
        //ADOBase.controller.Restart();
        SceneManager.LoadScene("scnLevelSelect");
    }
}

public enum PacketType : byte
{
    Welcome = 0x00,
    Update = 0x01,
    CountChanged = 0x02,
    Die = 0x03,
    ChangeScene = 0x04,
    Revive = 0x05,
    GetReady = 0x06,
    SetLevel = 0x07
}

public class PlayerInfo
{
    public byte PlayerID;
    public PlayerInfo(byte PlayerID)
    {
        this.PlayerID = PlayerID;
    }
}

[HarmonyPatch(typeof(SteamManager))]
internal static class SteamManagerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SteamManager.Update))]
    internal static void UpdatePostfix(SteamManager __instance)
    {
        if (!__instance.m_bInitialized || Plugin.pollGroup == null)
            return;

        IntPtr[] messages = new IntPtr[32];
        int count = SteamNetworkingSockets.ReceiveMessagesOnPollGroup(
            (HSteamNetPollGroup)Plugin.pollGroup,
            messages,
            messages.Length
        );

        for (int i = 0; i < count; i++)
        {
            SteamNetworkingMessage_t msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(messages[i]);
            byte[] data = new byte[msg.m_cbSize];
            Marshal.Copy(msg.m_pData, data, 0, data.Length);
            Plugin.HandlePacket(data, msg);
            //Marshal.Release(msg.m_pData);
        }
    }
}

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
        if (__instance.playerID != Plugin.localPlayer.PlayerID)
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
        else*/ if (ADOBase.isMobile)
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
    internal static bool DiePrefix(scrPlayer __instance)
    {
        if (Plugin.connection == null && Plugin.listenSocket == null)
            return true;

        if (__instance.playerID == Plugin.localPlayer.PlayerID)
        {
            byte[] data = new byte[1] { (byte)PacketType.Die };
            Plugin.SendToHost(data);
            return true;
        } else if (remoteDeath)
        {
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
        if (ADOBase.playerManager.allPlayers[Plugin.localPlayer.PlayerID] != __instance)
            return;
        
        if (ADOBase.playerManager.players.Length != Plugin.playerCount && !ADOBase.loader.isWipingToBlack)
            Plugin.ChangePlayerCount(Plugin.playerCount);

        if (!ADOBase.controller.gameworld)
        {
            if (__instance.currFloor != null && __instance.currFloor != lastFloor)
            {
                lastFloor = __instance.currFloor;
                byte[] data = new byte[sizeof(float) * 3 + 2]; // 3 floats, plus one byte for packet type and one for level type

                data[0] = (byte)PacketType.Update;
                data[1] = 0x00;
                Buffer.BlockCopy(BitConverter.GetBytes(__instance.currFloor.transform.position.x), 0, data, 0 * sizeof(float) + 2, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(__instance.currFloor.transform.position.y), 0, data, 1 * sizeof(float) + 2, sizeof(float));
                Buffer.BlockCopy(BitConverter.GetBytes(__instance.currFloor.transform.position.z), 0, data, 2 * sizeof(float) + 2, sizeof(float));
                Plugin.SendToHost(data, Constants.k_nSteamNetworkingSend_Unreliable);
            }
        }
    }

    public static bool remoteRevive = false;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrPlayer.Revive))]
    internal static bool RevivePrefix(scrPlayer __instance, int floorID, scrPlayer helperPlayer)
    {
        if (Plugin.connection == null && Plugin.listenSocket == null)
        {
            Plugin.Logger.LogInfo("Not connected");
            return true;
        }

        if (helperPlayer.playerID == Plugin.localPlayer.PlayerID || (helperPlayer == null && Plugin.isHost))
        {
            byte[] data = new byte[sizeof(int) + 3];
            data[0] = (byte)PacketType.Revive;
            data[1] = (byte)__instance.playerID;
            Buffer.BlockCopy(BitConverter.GetBytes(floorID), 0, data, 2, sizeof(int));
            Plugin.SendToHost(data, Constants.k_nSteamNetworkingSend_Unreliable);
            Plugin.Logger.LogInfo("Sending");
            return true;
        }
        else if (remoteRevive)
        {
            Plugin.Logger.LogInfo("Reviving" + ((byte)__instance.playerID == Plugin.localPlayer.PlayerID?" self":""));
            remoteRevive = false;
            return true;
        }
        Plugin.Logger.LogInfo("noneoftheabove");
        return false;
    }

}

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
        if (Plugin.connection == null && Plugin.listenSocket == null)
            return;

        if (__instance.player.playerID == Plugin.localPlayer.PlayerID)
        {
            byte[] data = new byte[3 + sizeof(int) + sizeof(float)];
            data[0] = (byte)PacketType.Update;
            data[1] = 0x01;
            data[2] = (byte)hitMargin;
            Buffer.BlockCopy(BitConverter.GetBytes(floor.seqID), 0, data, 3, sizeof(int));
            Buffer.BlockCopy(BitConverter.GetBytes(exitAngle), 0, data, 3 + sizeof(int), sizeof(float));
            Plugin.SendToHost(data);
        }
    }
}

[HarmonyPatch(typeof(PauseMenu))]
internal static class PauseMenuPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PauseMenu.ShowPlayerSelect))]
    internal static bool ShowPlayerSelectPrefix()
    {
        return true;
        bool isHosting = Plugin.listenSocket != null || Plugin.connection != null;
        if (!isHosting)
            Plugin.Host(7777);
        return isHosting;
    }
    [HarmonyPrefix]
    [HarmonyPatch(nameof(PauseMenu.OpenDiscord))]
    internal static bool OpenDiscordPrefix()
    {
        Plugin.Connect("127.0.0.1", 7777);
        return false;
    }
}

[HarmonyPatch(typeof(scnSplash))]
internal static class SplashPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scnSplash.Start))]
    internal static bool StartPrefix(scnSplash __instance)
    {
        __instance.GoToMenu();
        return false;
    }
}

[HarmonyPatch(typeof(SceneManager))]
internal static class SceneManagerPatch
{
    public static bool remote = false;
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SceneManager.LoadScene), new Type[] { typeof(string), typeof(LoadSceneMode) })]
    internal static void LoadScene1Postfix(string sceneName, LoadSceneMode mode)
    {
        if (!remote)
        {
            byte[] levelData = new byte[1 + GCS.internalLevelName.Length];
            levelData[0] = (byte)PacketType.SetLevel;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(GCS.internalLevelName), 0, levelData, 1, GCS.internalLevelName.Length);
            Plugin.SendToHost(levelData);

            byte[] data = new byte[2 + sceneName.Length];
            data[0] = (byte)PacketType.ChangeScene;
            data[1] = (byte)mode;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(sceneName), 0, data, 2, sceneName.Length);
            Plugin.SendToHost(data);
        }
        remote = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(SceneManager.LoadScene), new Type[] { typeof(string) })]
    internal static void LoadScene2Postfix(string sceneName)
    {
        if (!remote)
        {
            byte[] levelData = new byte[1 + GCS.internalLevelName.Length];
            levelData[0] = (byte)PacketType.SetLevel;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(GCS.internalLevelName), 0, levelData, 1, GCS.internalLevelName.Length);
            Plugin.SendToHost(levelData);

            byte[] data = new byte[2 + sceneName.Length];
            data[0] = (byte)PacketType.ChangeScene;
            data[1] = (byte)LoadSceneMode.Single;
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(sceneName), 0, data, 2, sceneName.Length);
            Plugin.SendToHost(data);
        }
        remote = false;
    }
}

[HarmonyPatch(typeof(scrController))]
internal static class ControllerPatch
{
    public static bool remoteGetReady = false;
    [HarmonyPrefix]
    [HarmonyPatch(nameof(scrController.Start_Rewind))]
    internal static bool Start_RewindPrefix()
    {
        if ((Plugin.connection == null && Plugin.listenSocket == null) || !ADOBase.controller.gameworld)
            return true;

        if (remoteGetReady)
        {
            remoteGetReady = false;
            return true;
        }
        Plugin.SendToHost(new byte[] { (byte)PacketType.GetReady });
        remoteGetReady = false;
        return true;
    }
}

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
        hostClick.onClick.AddListener(() => Plugin.Host(7777));

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
        joinClick.onClick.AddListener(() => Plugin.Connect("127.0.0.1", 7777));
    }
}