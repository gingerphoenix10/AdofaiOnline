using DG.Tweening.Plugins.Core;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace AdofaiOnline;

public static class Callbacks
{
    public static Callback<SteamNetConnectionStatusChangedCallback_t> statusChanged;
    public static Callback<LobbyCreated_t> lobbyCreated;
    public static Callback<GameLobbyJoinRequested_t> joinRequested;
    public static Callback<LobbyEnter_t> lobbyEntered;
    public static void InitializeCallbacks()
    {
        statusChanged = Callback<SteamNetConnectionStatusChangedCallback_t>.Create(OnStatusChanged);
        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public static void ResetCallbacks()
    {
        statusChanged.Dispose();
        statusChanged = null;
        lobbyCreated.Dispose();
        lobbyCreated = null;
        joinRequested.Dispose();
        joinRequested = null;
        lobbyEntered.Dispose();
        lobbyEntered = null;
    }

    public static List<HSteamNetConnection> beenAccepted = new();
    private static async void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
    {
        switch (callback.m_info.m_eState)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                {
                    if (!Networking.isHost)
                    {
                        Plugin.Logger.LogInfo("Client connecting...");
                        break;
                    }
                    if (callback.m_info.m_hListenSocket == HSteamListenSocket.Invalid)
                    {
                        Plugin.Logger.LogInfo("Invalid Socket");
                        break;
                    }
                    if (!callback.m_info.m_identityRemote.GetSteamID().IsValid())
                    {
                        Plugin.Logger.LogInfo("Invalid steam ID");
                        break;
                    }
                    // idk why but that's breaking stuff

                    Plugin.Logger.LogInfo("Incoming connection");
                    Plugin.Logger.LogInfo($"{Networking.listenSocket == null}, {Networking.listenSocket}");
                    Plugin.Logger.LogInfo($"{Networking.pollGroup == null}, {Networking.pollGroup}");
                    Plugin.Logger.LogInfo($"{Networking.LobbyID == null}, {Networking.LobbyID}");

                    Plugin.Logger.LogInfo("Accepting connection");
                    EResult result = SteamNetworkingSockets.AcceptConnection(callback.m_hConn);
                    Plugin.Logger.LogInfo($"AcceptConnection result: {result}");

                    if (result == EResult.k_EResultOK)
                    {
                        Plugin.Logger.LogInfo("Accepted connection. Setting PollGroup");

                        SteamNetworkingSockets.SetConnectionPollGroup(
                            callback.m_hConn,
                            Networking.pollGroup.Value
                        );
                    }
                    Plugin.Logger.LogInfo("Poll group set");
                    Plugin.Logger.LogInfo(
                        $"STATE CHANGE: {callback.m_hConn} -> {callback.m_info.m_eState} " +
                        $"endReason={callback.m_info.m_eEndReason} " +
                        $"listen={callback.m_info.m_hListenSocket}"
                    );
                    break;
                }

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                {
                    Plugin.Logger.LogInfo("Connected!");
                    if (Networking.isHost)
                    {
                        Plugin.Logger.LogInfo("Only client can send welcome info");
                        break;
                    }

                    Networking.pollGroup = SteamNetworkingSockets.CreatePollGroup();
                    SteamNetworkingSockets.SetConnectionPollGroup(Networking.connection.Value, Networking.pollGroup.Value);

                    ADOBase.controller.Restart();
                    byte[] data = new byte[] {
                        (byte)PacketType.Welcome,
                        Convert.ToByte(ADOBase.controller.neoCosmosManager.installed),
                        Convert.ToByte(ADOBase.controller.vegaDLCManager.installed),
                        Convert.ToByte(ADOBase.controller.featuredDLCManager.installed)
                    };
                    Networking.SendToHost(data);
                    break;
                }

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                {
                    Plugin.Logger.LogInfo($"Disconnected: {callback.m_info.m_eEndReason}");
                    if (Networking.isHost)
                    {
                        Networking.clients.Remove(callback.m_hConn);
                        SteamNetworkingSockets.CloseConnection(
                            callback.m_hConn,
                            0,
                            "Closing",
                            false
                        );

                        byte[] sendDataExisting = new byte[3]
                        {
                            (byte)PacketType.CountChanged,
                            Networking.localPlayer.PlayerID,
                            (byte)(Networking.clients.Count+1)
                        };
                        Networking.PacketEvent(sendDataExisting);
                        Networking.SendToHost(sendDataExisting);
                    }
                    else
                    {
                        Disconnected();
                    }
                    break;
                }
        }
    }

    public static void Disconnected()
    {
        if (Networking.connection.HasValue)
            SteamNetworkingSockets.CloseConnection(
                Networking.connection.Value,
                0,
                "Disconnected",
                false
            );
        Networking.connection = null;

        if (Networking.listenSocket.HasValue)
        {
            foreach (HSteamNetConnection client in Networking.clients.Keys)
            {
                SteamNetworkingSockets.CloseConnection(
                    client,
                    0,
                    "Server shutting down",
                    false
                );
            }
            SteamNetworkingSockets.CloseListenSocket(Networking.listenSocket.Value);
        }
        Networking.listenSocket = null;

        if (Networking.pollGroup.HasValue)
            SteamNetworkingSockets.DestroyPollGroup(Networking.pollGroup.Value);
        Networking.pollGroup = null;

        Networking.localPlayer = new(
            0x00,
            ADOBase.controller.neoCosmosManager.installed,
            ADOBase.controller.vegaDLCManager.installed,
            ADOBase.controller.featuredDLCManager.installed
        );
        Networking.LobbyID = null;
        Networking.clients.Clear();
        Networking.playerCount = 1;
        Networking.ChangePlayerCount(1);
    }

    private static void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Plugin.Logger.LogError($"Lobby creation failed: {callback.m_eResult}");
            return;
        }

        Networking.LobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        Plugin.Logger.LogInfo($"Lobby created: {Networking.LobbyID}");
    }

    private static void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        Plugin.Logger.LogInfo("JOIN REQUESTED");
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private static async void OnLobbyEntered(LobbyEnter_t callback)
    {
        Plugin.Logger.LogInfo("LOBBY ENTERED");
        Networking.LobbyID = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.RequestLobbyData(Networking.LobbyID.Value);

        CSteamID hostId =
            SteamMatchmaking.GetLobbyOwner(Networking.LobbyID.Value);
        SteamFriends.RequestUserInformation(hostId, false);

        if (!Networking.isHost)
        {
            await Task.Yield();
            Networking.ConnectSteam(hostId);
            Plugin.Logger.LogInfo("CONNECT CALLED");
        }
    }
}
