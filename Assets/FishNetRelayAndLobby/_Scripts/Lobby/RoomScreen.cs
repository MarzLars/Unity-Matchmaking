using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;

/// <summary>
///     NetworkBehaviours cannot easily be parented, so the network logic will take place
///     on the network scene object "NetworkLobby"
/// </summary>
public class RoomScreen : MonoBehaviour {
    [SerializeField] private LobbyPlayerPanel _playerPanelPrefab;
    [SerializeField] private Transform _playerPanelParent;
    [SerializeField] private TMP_Text _waitingText;
    [SerializeField] private GameObject _startButton, _readyButton;

    private readonly List<LobbyPlayerPanel> _playerPanels = new();
    private bool _allReady;
    private bool _ready;

    public static event Action StartPressed; 

    private void OnEnable() {
        foreach (Transform child in _playerPanelParent) Destroy(child.gameObject);
        _playerPanels.Clear();

        LobbyOrchestrator.LobbyPlayersUpdated += NetworkLobbyPlayersUpdated;
        MatchmakingService.CurrentLobbyRefreshed += OnCurrentLobbyRefreshed;
        _startButton.SetActive(false);
        _readyButton.SetActive(false);

        _ready = false;
    }

    private void OnDisable() {
        LobbyOrchestrator.LobbyPlayersUpdated -= NetworkLobbyPlayersUpdated;
        MatchmakingService.CurrentLobbyRefreshed -= OnCurrentLobbyRefreshed;
    }

    public static event Action LobbyLeft;

    public void OnLeaveLobby() {
        LobbyLeft?.Invoke();
    }

    private void NetworkLobbyPlayersUpdated(Dictionary<int, bool> players) {
        var allActivePlayerIds = players.Keys;

        // Remove all inactive panels
        var toDestroy = _playerPanels.Where(p => !allActivePlayerIds.Contains(p.PlayerId)).ToList();
        foreach (var panel in toDestroy) {
            _playerPanels.Remove(panel);
            Destroy(panel.gameObject);
        }

        foreach (var player in players) {
            var currentPanel = _playerPanels.FirstOrDefault(p => p.PlayerId == player.Key);
            if (currentPanel != null) {
                if (player.Value) currentPanel.SetReady();
            }
            else {
                var panel = Instantiate(_playerPanelPrefab, _playerPanelParent);
                panel.Init(player.Key);
                _playerPanels.Add(panel);
            }
        }

        bool isHost = InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started;
        _startButton.SetActive(isHost && players.All(p => p.Value));
        _readyButton.SetActive(!_ready);
    }

    private void OnCurrentLobbyRefreshed(Lobby lobby) {
        _waitingText.text = $"Waiting on players... {lobby.Players.Count}/{lobby.MaxPlayers}";
    }

    public void OnReadyClicked() {
        _readyButton.SetActive(false);
        _ready = true;
    }

    public void OnStartClicked() {
        StartPressed?.Invoke();
    }
}