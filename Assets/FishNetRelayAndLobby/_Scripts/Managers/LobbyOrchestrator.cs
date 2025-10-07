using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using Unity.Services.Lobbies.Models;
using UnityEngine;

#pragma warning disable CS4014

/// <summary>
///     Lobby orchestrator. I put as much UI logic within the three sub screens,
///     but the transport and RPC logic remains here. It's possible we could pull
/// </summary>
public class LobbyOrchestrator : NetworkBehaviour {
    [SerializeField] private MainLobbyScreen _mainLobbyScreen;
    [SerializeField] private CreateLobbyScreen _createScreen;
    [SerializeField] private RoomScreen _roomScreen;

    private void Start() {
        _mainLobbyScreen.gameObject.SetActive(true);
        _createScreen.gameObject.SetActive(false);
        _roomScreen.gameObject.SetActive(false);

        CreateLobbyScreen.LobbyCreated += CreateLobby;
        LobbyRoomPanel.LobbySelected += OnLobbySelected;
        RoomScreen.LobbyLeft += OnLobbyLeft;
        RoomScreen.StartPressed += OnGameStart;
    }

    #region Main Lobby

    private async void OnLobbySelected(Lobby lobby) {
        using (new Load("Joining Lobby...")) {
            try {
                await MatchmakingService.JoinLobbyWithAllocation(lobby.Id);

                _mainLobbyScreen.gameObject.SetActive(false);
                _roomScreen.gameObject.SetActive(true);

                InstanceFinder.ClientManager.StartConnection();
            }
            catch (Exception e) {
                Debug.LogError(e);
                CanvasUtilities.Instance.ShowError("Failed joining lobby");
            }
        }
    }

    #endregion

    #region Create

    private async void CreateLobby(LobbyData data) {
        using (new Load("Creating Lobby...")) {
            try {
                await MatchmakingService.CreateLobbyWithAllocation(data);

                _createScreen.gameObject.SetActive(false);
                _roomScreen.gameObject.SetActive(true);

                // Starting the host immediately will keep the relay server alive
                InstanceFinder.ServerManager.StartConnection();
                InstanceFinder.ClientManager.StartConnection();
            }
            catch (Exception e) {
                Debug.LogError(e);
                CanvasUtilities.Instance.ShowError("Failed creating lobby");
            }
        }
    }

    #endregion

    #region Room

    private readonly Dictionary<int, bool> _playersInLobby = new();
    public static event Action<Dictionary<int, bool>> LobbyPlayersUpdated;

    public override void OnStartNetwork() {
        base.OnStartNetwork();
        if (IsServerInitialized) {
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnServerConnectionState;
            _playersInLobby.Add(LocalConnection.ClientId, false);
            UpdateInterface();
        }

        // Client uses this in case host destroys the lobby
        InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
    }

    private void OnServerConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args) {
        if (!IsServerInitialized) return;

        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started) {
            // Add locally
            if (!_playersInLobby.ContainsKey(conn.ClientId)) _playersInLobby.Add(conn.ClientId, false);

            PropagateToClients();

            UpdateInterface();
        }
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped) {
            // Handle locally
            if (_playersInLobby.ContainsKey(conn.ClientId)) _playersInLobby.Remove(conn.ClientId);

            // Propagate all clients
            RemovePlayerClientRpc(conn.ClientId);

            UpdateInterface();
        }
    }

    private void OnClientConnectionState(FishNet.Transporting.ClientConnectionStateArgs args) {
        if (args.ConnectionState == FishNet.Transporting.LocalConnectionState.Stopped && !IsServerInitialized) {
            // This happens when the host disconnects the lobby
            _roomScreen.gameObject.SetActive(false);
            _mainLobbyScreen.gameObject.SetActive(true);
            OnLobbyLeft();
        }
    }

    private void PropagateToClients() {
        foreach (var player in _playersInLobby) UpdatePlayerClientRpc(player.Key, player.Value);
    }

    [ObserversRpc]
    private void UpdatePlayerClientRpc(int clientId, bool isReady) {
        if (IsServerInitialized) return;

        if (!_playersInLobby.ContainsKey(clientId)) _playersInLobby.Add(clientId, isReady);
        else _playersInLobby[clientId] = isReady;
        UpdateInterface();
    }

    [ObserversRpc]
    private void RemovePlayerClientRpc(int clientId) {
        if (IsServerInitialized) return;

        if (_playersInLobby.ContainsKey(clientId)) _playersInLobby.Remove(clientId);
        UpdateInterface();
    }

    public void OnReadyClicked() {
        SetReadyServerRpc(LocalConnection.ClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetReadyServerRpc(int playerId) {
        _playersInLobby[playerId] = true;
        PropagateToClients();
        UpdateInterface();
    }

    private void UpdateInterface() {
        LobbyPlayersUpdated?.Invoke(_playersInLobby);
    }

    private async void OnLobbyLeft() {
        using (new Load("Leaving Lobby...")) {
            _playersInLobby.Clear();
            if (InstanceFinder.NetworkManager != null) {
                InstanceFinder.ServerManager?.StopConnection(true);
                InstanceFinder.ClientManager?.StopConnection();
            }
            await MatchmakingService.LeaveLobby();
        }
    }
    
    public override void OnStopNetwork() {
        base.OnStopNetwork();
        CreateLobbyScreen.LobbyCreated -= CreateLobby;
        LobbyRoomPanel.LobbySelected -= OnLobbySelected;
        RoomScreen.LobbyLeft -= OnLobbyLeft;
        RoomScreen.StartPressed -= OnGameStart;
        
        // We only care about this during lobby
        if (InstanceFinder.NetworkManager != null) {
            if (InstanceFinder.ServerManager != null)
                InstanceFinder.ServerManager.OnRemoteConnectionState -= OnServerConnectionState;
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
    }
    
    private async void OnGameStart() {
        using (new Load("Starting the game...")) {
            await MatchmakingService.LockLobby();
            InstanceFinder.SceneManager.LoadGlobalScenes(new SceneLoadData("Game"));
        }
    }

    #endregion
}
