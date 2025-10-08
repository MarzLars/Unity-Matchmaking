using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FishNet;
using FishNet.Transporting.UTP;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MatchmakingService {
    const int HeartbeatInterval = 15;
    const int LobbyRefreshRate = 5; // Increased from 2 to avoid rate limiting

    static UnityTransport _transport;

    static Lobby _currentLobby;
    static CancellationTokenSource _heartbeatSource, _updateLobbySource;
    
    static UnityTransport Transport{
        get => _transport ? _transport : _transport = Object.FindFirstObjectByType<UnityTransport>();
        set => _transport = value;
    }

    public static event Action<Lobby> CurrentLobbyRefreshed;

    public static void ResetStatics() {
        if (Transport) {
            if (InstanceFinder.NetworkManager) {
                InstanceFinder.ServerManager?.StopConnection(true);
                InstanceFinder.ClientManager?.StopConnection();
            }
            Transport = null;
        }

        _currentLobby = null;
    }

    public static async Task<List<Lobby>> GatherLobbies() {
        var options = new QueryLobbiesOptions {
            Count = 15,

            Filters = new List<QueryFilter> {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                new QueryFilter(QueryFilter.FieldOptions.IsLocked, "0", QueryFilter.OpOptions.EQ)
            }
        };

        var allLobbies = await LobbyService.Instance.QueryLobbiesAsync(options);
        return allLobbies.Results;
    }

    public static async Task CreateLobbyWithAllocation(LobbyData data) {
        // Create a relay allocation and generate a join code to share with the lobby
        var allocation = await RelayService.Instance.CreateAllocationAsync(data.MaxPlayers);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        // Create a lobby, adding the relay join code to the lobby data
        var options = new CreateLobbyOptions {
            Data = new Dictionary<string, DataObject> {
                { Constants.JoinKey, new DataObject(DataObject.VisibilityOptions.Member, joinCode) },
                { Constants.GameTypeKey, new DataObject(DataObject.VisibilityOptions.Public, data.Type.ToString(), DataObject.IndexOptions.N1) }, {
                    Constants.DifficultyKey,
                    new DataObject(DataObject.VisibilityOptions.Public, data.Difficulty.ToString(), DataObject.IndexOptions.N2)
                }
            }
        };

        _currentLobby = await LobbyService.Instance.CreateLobbyAsync(data.Name, data.MaxPlayers, options);

        SetHostRelayData(allocation);

        Heartbeat();
        PeriodicallyRefreshLobby();
    }

    static void SetHostRelayData(Allocation allocation) {
        if (!Transport) {
            Debug.LogError("UnityTransport not found!");
            return;
        }

        Transport.SetHostRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData
        );
    }

    public static async Task LockLobby() {
        try {
            await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, new UpdateLobbyOptions { IsLocked = true });
        }
        catch (Exception e) {
            Debug.Log($"Failed closing lobby: {e}");
        }
    }

    static async void Heartbeat() {
        _heartbeatSource = new CancellationTokenSource();
        while (!_heartbeatSource.IsCancellationRequested && _currentLobby != null) {
            await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
            await Task.Delay(HeartbeatInterval * 1000);
        }
    }

    public static async Task JoinLobbyWithAllocation(string lobbyId) {
        // First, ensure we're not in any other lobbies
        await EnsureNotInOtherLobbies(lobbyId);

        try {
            _currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
        }
        catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyConflict) {
            // Player is already in this lobby, just fetch the current state
            Debug.Log($"Player already in lobby {lobbyId}, fetching current state...");
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
        }

        var joinAllocation = await RelayService.Instance.JoinAllocationAsync(_currentLobby.Data[Constants.JoinKey].Value);

        SetClientRelayData(joinAllocation);

        PeriodicallyRefreshLobby();
    }

    /// <summary>
    /// Ensures the player is not in any other lobbies before joining a new one
    /// </summary>
    private static async Task EnsureNotInOtherLobbies(string targetLobbyId) {
        try {
            var joinedLobbies = await LobbyService.Instance.GetJoinedLobbiesAsync();
            
            foreach (var joinedLobbyId in joinedLobbies) {
                // Skip if it's the lobby we're trying to join
                if (joinedLobbyId == targetLobbyId) {
                    continue;
                }

                try {
                    await LobbyService.Instance.RemovePlayerAsync(joinedLobbyId, Authentication.PlayerId);
                    Debug.Log($"Left old lobby: {joinedLobbyId}");
                }
                catch (Exception e) {
                    Debug.LogWarning($"Failed to leave lobby {joinedLobbyId}: {e.Message}");
                }
            }
        }
        catch (Exception e) {
            Debug.LogWarning($"Failed to check joined lobbies: {e.Message}");
        }
    }

    static void SetClientRelayData(JoinAllocation allocation) {
        if (!Transport) {
            Debug.LogError("UnityTransport not found!");
            return;
        }

        Transport.SetClientRelayData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.HostConnectionData
        );
    }

    static async void PeriodicallyRefreshLobby() {
        _updateLobbySource = new CancellationTokenSource();
        await Task.Delay(LobbyRefreshRate * 1000);
        while (!_updateLobbySource.IsCancellationRequested && _currentLobby != null) {
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            CurrentLobbyRefreshed?.Invoke(_currentLobby);
            await Task.Delay(LobbyRefreshRate * 1000);
        }
    }

    public static async Task LeaveLobby() {
        _heartbeatSource?.Cancel();
        _updateLobbySource?.Cancel();

        if (_currentLobby != null)
            try {
                if (_currentLobby.HostId == Authentication.PlayerId) await LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
                else await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, Authentication.PlayerId);
                _currentLobby = null;
            }
            catch (Exception e) {
                Debug.Log(e);
            }
    }
}