using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class GameManager : NetworkBehaviour {
    [SerializeField] private GameObject _playerPrefab;

    public override void OnStartClient() {
        base.OnStartClient();
        SpawnPlayerServerRpc(LocalConnection);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SpawnPlayerServerRpc(NetworkConnection conn) {
        var spawn = Instantiate(_playerPrefab);
        ServerManager.Spawn(spawn, conn);
    }

    public override void OnStopClient() {
        base.OnStopClient();
        HandleLeaveLobby();
    }

    private async void HandleLeaveLobby() {
        await MatchmakingService.LeaveLobby();
    }

    private void OnDestroy() {
        if (InstanceFinder.NetworkManager != null) {
            InstanceFinder.ServerManager?.StopConnection(true);
            InstanceFinder.ClientManager?.StopConnection();
        }
    }
}