using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

/// <summary>
///     Lobby orchestrator. I put as much UI logic within the three sub screens,
///     but the transport and RPC logic remains here. It's possible we could pull
/// </summary>
public class LobbyOrchestrator : NetworkBehaviour {
    [SerializeField] MainLobbyScreen mainLobbyScreen;
    [SerializeField] CreateLobbyScreen createScreen;
    [SerializeField] RoomScreen roomScreen;

    void Awake() {
        mainLobbyScreen.gameObject.SetActive(true);
        createScreen.gameObject.SetActive(false);
        roomScreen.gameObject.SetActive(false);
    }

    void Start() {
        CreateLobbyScreen.LobbyCreated += CreateLobby;
        LobbyRoomPanel.LobbySelected += OnLobbySelected;
        RoomScreen.LobbyLeft += OnLobbyLeft;
        RoomScreen.StartPressed += OnGameStart;
        this.gameObject.SetActive(true);
    }

    void OnDestroy() {
        CreateLobbyScreen.LobbyCreated -= CreateLobby;
        LobbyRoomPanel.LobbySelected -= OnLobbySelected;
        RoomScreen.LobbyLeft -= OnLobbyLeft;
        RoomScreen.StartPressed -= OnGameStart;
    }

    #region Main Lobby
    async void OnLobbySelected(Lobby lobby) {
        using (new Load("Joining Lobby...")) {
            try {
                await MatchmakingService.JoinLobbyWithAllocation(lobby.Id);

                if (mainLobbyScreen == null || roomScreen == null) return;

                mainLobbyScreen.gameObject.SetActive(false);
                roomScreen.gameObject.SetActive(true);

                InstanceFinder.ClientManager.StartConnection();
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyNotFound) {
                Debug.LogError($"Lobby no longer exists: {e}");
                if (CanvasUtilities.Instance != null) {
                    CanvasUtilities.Instance.ShowError("This lobby no longer exists");
                }
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyFull) {
                Debug.LogError($"Lobby is full: {e}");
                if (CanvasUtilities.Instance != null) {
                    CanvasUtilities.Instance.ShowError("This lobby is full");
                }
            }
            catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyConflict) {
                Debug.LogWarning($"Player already in lobby, recovered: {e}");
                // The service should have handled this, but if we still get here, proceed
                if (mainLobbyScreen != null && roomScreen != null) {
                    mainLobbyScreen.gameObject.SetActive(false);
                    roomScreen.gameObject.SetActive(true);
                    InstanceFinder.ClientManager.StartConnection();
                }
            }
            catch (Exception e) {
                Debug.LogError($"Failed joining lobby: {e}");
                if (CanvasUtilities.Instance != null) {
                    CanvasUtilities.Instance.ShowError("Failed joining lobby");
                }
            }
        }
    }

    #endregion

    #region Create
    async void CreateLobby(LobbyData data) {
        using (new Load("Creating Lobby...")) {
            try {
                await MatchmakingService.CreateLobbyWithAllocation(data);

                if (createScreen == null || roomScreen == null) return;

                createScreen.gameObject.SetActive(false);
                roomScreen.gameObject.SetActive(true);

                // Starting the host immediately will keep the relay server alive
                InstanceFinder.ServerManager.StartConnection();
                InstanceFinder.ClientManager.StartConnection();
            }
            catch (Exception e) {
                Debug.LogError(e);
                if (CanvasUtilities.Instance != null) {
                    CanvasUtilities.Instance.ShowError("Failed creating lobby");
                }
            }
        }
    }

    #endregion

    #region Room
    readonly Dictionary<int, bool> _playersInLobby = new Dictionary<int, bool>();
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

    void OnServerConnectionState(NetworkConnection conn, FishNet.Transporting.RemoteConnectionStateArgs args) {
        if (!IsServerInitialized) return;

        if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Started) {
            // Add locally
            _playersInLobby.TryAdd(conn.ClientId, false);

            PropagateToClients();

            UpdateInterface();
        }
        else if (args.ConnectionState == FishNet.Transporting.RemoteConnectionState.Stopped) {
            // Handle locally
            _playersInLobby.Remove(conn.ClientId);

            // Propagate all clients
            RemovePlayerClientRpc(conn.ClientId);

            UpdateInterface();
        }
    }

    void OnClientConnectionState(FishNet.Transporting.ClientConnectionStateArgs args)
    {
        if (args.ConnectionState != FishNet.Transporting.LocalConnectionState.Stopped || IsServerInitialized)
            return;
        // This happens when the host disconnects the lobby
        if (roomScreen != null && mainLobbyScreen != null) {
            roomScreen.gameObject.SetActive(false);
            mainLobbyScreen.gameObject.SetActive(true);
        }
        OnLobbyLeft();
    }

    void PropagateToClients() {
        foreach (var player in _playersInLobby) UpdatePlayerClientRpc(player.Key, player.Value);
    }

    [ObserversRpc]
    void UpdatePlayerClientRpc(int clientId, bool isReady) {
        if (IsServerInitialized) return;

        _playersInLobby[clientId] = isReady;
        UpdateInterface();
    }

    [ObserversRpc]
    void RemovePlayerClientRpc(int clientId) {
        if (IsServerInitialized) return;

        _playersInLobby.Remove(clientId);
        UpdateInterface();
    }

    public void OnReadyClicked() {
        SetReadyServerRpc(LocalConnection.ClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    void SetReadyServerRpc(int playerId) {
        _playersInLobby[playerId] = true;
        PropagateToClients();
        UpdateInterface();
    }

    void UpdateInterface() {
        LobbyPlayersUpdated?.Invoke(_playersInLobby);
    }

    async void OnLobbyLeft() {
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
        
        // We only care about this during lobby
        if (!InstanceFinder.NetworkManager)
            return;
        if (InstanceFinder.ServerManager)
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnServerConnectionState;
        if (InstanceFinder.ClientManager)
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
    }

    async void OnGameStart() {
        using (new Load("Starting the game...")) {
            await MatchmakingService.LockLobby();
            InstanceFinder.SceneManager.LoadGlobalScenes(new SceneLoadData("Game"));
        }
    }

    #endregion
}
