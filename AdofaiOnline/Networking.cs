using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using AdofaiOnline.Patches;

namespace AdofaiOnline;

public static class Networking
{

    public static CSteamID LobbyID;
    public static Dictionary<HSteamNetConnection, PlayerInfo> clients = new();
    public static byte playerCount = 1;
    public static PlayerInfo localPlayer = new(0x00);
    public static bool isHost = true;
    public static HSteamNetPollGroup? pollGroup = null;

    // Host
    public static HSteamListenSocket? listenSocket = null;

    // Client
    public static HSteamNetConnection? connection = null;

    public static void Host(ushort port)
    {
        isHost = true;
        SteamNetworkingIPAddr addr = new SteamNetworkingIPAddr();
        addr.Clear();
        addr.m_port = port;

        listenSocket = SteamNetworkingSockets.CreateListenSocketIP(ref addr, 0, null);

        pollGroup = SteamNetworkingSockets.CreatePollGroup();

        Plugin.Logger.LogInfo($"Hosting on port {port}");
        ADOBase.controller.Restart();
    }

    public static void HostSteam(int virtualPort)
    {
        isHost = true;

        listenSocket = SteamNetworkingSockets.CreateListenSocketP2P(virtualPort, 0, null);

        pollGroup = SteamNetworkingSockets.CreatePollGroup();

        Plugin.Logger.LogInfo($"Hosting on virtual port {virtualPort}");

        SteamAPICall_t createLobbyCall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
        //ADOBase.controller.Restart();
    }

    public static void Connect(string ip, ushort port)
    {
        isHost = false;
        SteamNetworkingIPAddr addr = new SteamNetworkingIPAddr();

        addr.ParseString(ip);
        addr.m_port = port;

        connection = SteamNetworkingSockets.ConnectByIPAddress(ref addr, 0, null);
        pollGroup = SteamNetworkingSockets.CreatePollGroup();

        Plugin.Logger.LogInfo("Connecting...");
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
            Plugin.Logger.LogInfoError("Failed to create P2P connection");
            return;
        }

        Plugin.Logger.LogInfo($"Connecting to host: {hostSteamId}");
    }

    public static void SendToHost(byte[] data, int flags = Constants.k_nSteamNetworkingSend_Reliable)
    {
        if (isHost)
        {
            HandlePacket(data, new SteamNetworkingMessage_t { m_nFlags = flags });
        }
        else
        {
            SendToConnection(connection.Value, data, flags, out _);
        }
    }

    public static void HandlePacket(byte[] data, SteamNetworkingMessage_t msg)
    {
        if (data.Length == 0)
            return;

        Plugin.Logger.LogInfo(BitConverter.ToString(data));

        if (data[0] == (byte)PacketType.Welcome)
        {
            if (isHost)
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

            }
            else
            {
                byte playerNum = data[1];
                Plugin.Logger.LogInfo($"Joined! Player ID {playerNum}");
                localPlayer.PlayerID = playerNum;
                ChangePlayerCount(data[2]);
            }
            return;
        }

        if (isHost)
        {
            PlayerInfo plr = localPlayer;
            if (clients.TryGetValue(msg.m_conn, out PlayerInfo plrTemp))
                plr = plrTemp;

            byte[] newData = new byte[data.Length + 1];
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
        }
        else
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
                if (data[1] == localPlayer.PlayerID)
                    break;

                if (!ADOBase.controller.gameworld && data[2] == 0x00)
                {
                    Plugin.Logger.LogInfo($"Update from {data[1]}");

                    Vector3 pos;

                    float x = BitConverter.ToSingle(data, 3 + 0 * sizeof(float));
                    float y = BitConverter.ToSingle(data, 3 + 1 * sizeof(float));
                    float z = BitConverter.ToSingle(data, 3 + 2 * sizeof(float));

                    pos = new Vector3(x, y, z);
                    Plugin.Logger.LogInfo(pos.ToString());
                    PlanetarySystem planets = plr.planetarySystem;
                    scrPlanet planet = planets.chosenPlanet;
                    if (planet != null)
                    {
                        scrFloor flr = RDUtils.GetFloorAtPosition(pos);
                        if (planet.currfloor != flr && flr != null)
                        {
                            scrPlanetPatch.forcedTilePos = pos;
                            Plugin.Logger.LogInfo(flr.transform.GetComponent<Collider2D>().ToString());
                            Physics2DPatch.forcedOutput = new Collider2D[] { flr.transform.GetComponent<Collider2D>() };
                            plr.Hit();
                            //planet.transform.position = flr.transform.position;
                            planet.currfloor = flr;
                        }
                    }
                }
                else if (ADOBase.controller.gameworld && data[2] == 0x01)
                {
                    HitMargin margin = (HitMargin)data[3];
                    int seqId = BitConverter.ToInt32(data, 4);
                    float exitAngle = BitConverter.ToSingle(data, 4 + sizeof(int));
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
                scrControllerPatch.remoteGetReady = true;
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