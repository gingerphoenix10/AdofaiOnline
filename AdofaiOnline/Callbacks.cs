using DG.Tweening.Plugins.Core;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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

    private static void OnStatusChanged(SteamNetConnectionStatusChangedCallback_t callback)
    {
        switch (callback.m_info.m_eState)
        {
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting:
                {
                    //if (callback.m_info.m_hListenSocket == HSteamListenSocket.Invalid)
                    //    break;
                    // idk why but that's breaking stuff

                    Plugin.Logger.LogInfo("Incoming connection");

                    SteamNetworkingSockets.AcceptConnection(callback.m_hConn);

                    SteamNetworkingSockets.SetConnectionPollGroup(
                        callback.m_hConn,
                        Networking.pollGroup.Value
                    );

                    break;
                }

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected:
                {
                    Plugin.Logger.LogInfo("Connected!");
                    if (Networking.isHost)
                        return;
                    ADOBase.controller.Restart();
                    byte[] data = new byte[1] { (byte)PacketType.Welcome };
                    Networking.SendToHost(data);
                    break;
                }

            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer:
            case ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally:
                {
                    Plugin.Logger.LogInfo("Disconnected");
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

        Networking.localPlayer = new(0x00);
        Networking.LobbyID = null;
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
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private static void OnLobbyEntered(LobbyEnter_t callback)
    {
        Networking.LobbyID = new CSteamID(callback.m_ulSteamIDLobby);

        CSteamID hostId =
            SteamMatchmaking.GetLobbyOwner(Networking.LobbyID.Value);

        if (!Networking.isHost)
            Networking.ConnectSteam(hostId);
    }
}
