using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class MainLobbyScreen : MonoBehaviour {
    [SerializeField] LobbyRoomPanel lobbyPanelPrefab;
    [SerializeField] Transform lobbyParent;
    [SerializeField] GameObject noLobbiesText;
    [SerializeField] float lobbyRefreshRate = 5; // Increased from 2 to avoid rate limiting

    readonly List<LobbyRoomPanel> _currentLobbySpawns = new();
    float _nextRefreshTime;
    bool _isFetching; // Guard against multiple simultaneous fetches
    CancellationTokenSource _cancellationTokenSource;

    void Update() {
        if (Time.time >= _nextRefreshTime && !_isFetching) {
            FetchLobbies();
        }
    }

    void OnEnable() {
        foreach (Transform child in lobbyParent) Destroy(child.gameObject);
        _currentLobbySpawns.Clear();
        _isFetching = false;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    void OnDisable() {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _isFetching = false;
    }

    async void FetchLobbies() {
        if (_isFetching) return; // Additional guard

        _isFetching = true;
        
        try {
            // Grab all current lobbies
            var allLobbies = await MatchmakingService.GatherLobbies();

            // Check if we've been disabled/cancelled while waiting
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested) {
                return;
            }

            // Destroy all the current lobby panels which don't exist anymore.
            // Exclude our own homes as it'll show for a brief moment after closing the room
            var lobbyIds = allLobbies.Where(l => l.HostId != Authentication.PlayerId).Select(l => l.Id);
            var notActive = _currentLobbySpawns.Where(l => !lobbyIds.Contains(l.Lobby.Id)).ToList();

            foreach (var panel in notActive) {
                Destroy(panel.gameObject);
                _currentLobbySpawns.Remove(panel);
            }

            // Update or spawn the remaining active lobbies
            foreach (var lobby in allLobbies) {
                // Skip our own lobby
                if (lobby.HostId == Authentication.PlayerId) continue;

                var current = _currentLobbySpawns.FirstOrDefault(p => p.Lobby.Id == lobby.Id);
                if (current != null) {
                    current.UpdateDetails(lobby);
                }
                else {
                    var panel = Instantiate(lobbyPanelPrefab, lobbyParent);
                    panel.Init(lobby);
                    _currentLobbySpawns.Add(panel);
                }
            }

            noLobbiesText.SetActive(!_currentLobbySpawns.Any());
        }
        catch (Exception e) {
            Debug.LogError($"Error fetching lobbies: {e}");
        }
        finally {
            _isFetching = false;
            _nextRefreshTime = Time.time + lobbyRefreshRate; // Set AFTER completion
        }
    }
}