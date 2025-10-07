# Unity Netcode to FishNet Migration Guide

## Migration Summary

This project has been successfully migrated from Unity Netcode for GameObjects to FishNet Networking. Below are the key changes and important notes.

## Files Modified

### 1. **PlayerController.cs**
- Changed base class from `Unity.Netcode.NetworkBehaviour` to `FishNet.Object.NetworkBehaviour`
- Replaced `OnNetworkSpawn()` with `OnStartClient()`
- Changed `IsOwner` check location to disable non-owned instances

### 2. **GameManager.cs**
- Updated to use FishNet's spawning system: `ServerManager.Spawn()`
- Changed `_playerPrefab` from `PlayerController` to `GameObject` type
- Replaced `NetworkManager.Singleton` with `InstanceFinder` API
- Updated connection management to use `ServerManager` and `ClientManager`

### 3. **LobbyOrchestrator.cs**
- Migrated RPC system from Unity Netcode to FishNet:
  - `[ClientRpc]` → `[ObserversRpc]`
  - `[ServerRpc]` remains the same
- Changed client ID type from `ulong` to `int` (FishNet uses int for ClientId)
- Updated connection callbacks:
  - `OnClientConnectedCallback` → `OnRemoteConnectionState`
  - `OnClientDisconnectCallback` → `OnClientConnectionState`
- Replaced `NetworkManager.Singleton` with `InstanceFinder` API
- Updated scene loading: `SceneManager.LoadScene()` → `InstanceFinder.SceneManager.LoadGlobalScenes()`
- Changed lifecycle methods: `OnNetworkSpawn()` → `OnStartNetwork()`, `OnDestroy()` → `OnStopNetwork()`

### 4. **MatchmakingService.cs**
- **Critical Change**: Switched from Unity Transport (UTP) to FishNet's Tugboat transport
- Updated relay setup methods:
  - `SetHostRelayData()` → Uses `Tugboat.SetServerRelayInformation()`
  - `SetClientRelayData()` → Uses `Tugboat.SetClientRelayInformation()`
- Replaced `UnityTransport` with `Tugboat` transport component
- Updated connection management to use FishNet's `ServerManager` and `ClientManager`

### 5. **RoomScreen.cs**
- Updated to handle `Dictionary<int, bool>` instead of `Dictionary<ulong, bool>`
- Replaced `NetworkManager.Singleton.IsHost` with FishNet's `InstanceFinder.ServerManager.Started`

### 6. **LobbyPlayerPanel.cs**
- Changed `PlayerId` type from `ulong` to `int`

### 7. **Bootstrapper.cs**
- Removed Unity Netcode namespace reference (no functional changes needed)

## Key Differences Between Unity Netcode and FishNet

### API Changes
| Unity Netcode | FishNet |
|--------------|---------|
| `NetworkManager.Singleton` | `InstanceFinder.NetworkManager` |
| `IsServer` / `IsClient` / `IsHost` | `IsServerInitialized` / `IsClientStarted` |
| `NetworkObject.Spawn()` | `ServerManager.Spawn()` |
| `ulong ClientId` | `int ClientId` |
| `[ClientRpc]` | `[ObserversRpc]` or `[TargetRpc]` |
| `OnNetworkSpawn()` | `OnStartNetwork()` or `OnStartClient()` |
| `UnityTransport` | `Tugboat` (for Unity Relay) |

### Connection Management
- **Unity Netcode**: `NetworkManager.Singleton.StartHost()` / `StartClient()` / `Shutdown()`
- **FishNet**: 
  - `InstanceFinder.ServerManager.StartConnection()`
  - `InstanceFinder.ClientManager.StartConnection()`
  - `InstanceFinder.ServerManager.StopConnection()`

### Scene Management
- **Unity Netcode**: `NetworkManager.SceneManager.LoadScene()`
- **FishNet**: `InstanceFinder.SceneManager.LoadGlobalScenes(new SceneLoadData(sceneName))`

## Important Setup Requirements

### 1. Transport Layer
You **MUST** have a Tugboat transport component in your scene instead of Unity Transport:
- Remove any `UnityTransport` components from your NetworkManager
- Add the `Tugboat` transport component to handle Unity Relay connections
- Tugboat is FishNet's transport that supports Unity Relay services

### 2. NetworkObject Component
- Ensure all networked GameObjects have FishNet's `NetworkObject` component
- Update your player prefab to use FishNet's NetworkObject instead of Unity Netcode's

### 3. NetworkManager
- Replace Unity's NetworkManager with FishNet's NetworkManager in your scenes
- Configure the NetworkManager to use the Tugboat transport

## Testing Checklist

- [ ] Verify Tugboat transport is properly configured in NetworkManager
- [ ] Test lobby creation and joining
- [ ] Test player spawning in game scene
- [ ] Test player ready states in lobby
- [ ] Test host starting the game
- [ ] Test disconnection handling
- [ ] Test with ParrelSync clones (if using)

## Additional Notes

### Unity Relay Compatibility
FishNet's Tugboat transport fully supports Unity Relay, so your existing matchmaking infrastructure with Unity Gaming Services (Lobby + Relay) remains functional.

### Performance
FishNet generally offers better performance than Unity Netcode, especially with:
- Lower bandwidth usage
- More efficient serialization
- Better support for large player counts

### Known Warnings
Some code style warnings remain (e.g., underscore-prefixed field names). These are cosmetic and don't affect functionality.

## Resources

- [FishNet Documentation](https://fish-networking.gitbook.io/docs/)
- [FishNet Discord](https://discord.gg/Ta9HgDh4Hj)
- [Tugboat Transport Documentation](https://fish-networking.gitbook.io/docs/manual/guides/transports/tugboat)

## Migration Date
Completed: 2025-10-07

