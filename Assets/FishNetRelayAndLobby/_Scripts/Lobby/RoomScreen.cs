using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
///     NetworkBehaviours cannot easily be parented, so the network logic will take place
///     on the network scene object "NetworkLobby"
/// </summary>
public class RoomScreen : MonoBehaviour {
    [FormerlySerializedAs("_playerPanelPrefab")] [SerializeField] private LobbyPlayerPanel playerPanelPrefab;
    [FormerlySerializedAs("_playerPanelParent")] [SerializeField] private Transform playerPanelParent;
    [FormerlySerializedAs("_waitingText")] [SerializeField] private TMP_Text waitingText;
    [FormerlySerializedAs("_startButton")] [SerializeField] private GameObject startButton;
    [FormerlySerializedAs("_readyButton")] [SerializeField] private GameObject readyButton;

    private readonly List<LobbyPlayerPanel> _playerPanels = new();
    private bool _allReady;
    private bool _ready;

    public static event Action StartPressed; 

    private void OnEnable() {
        foreach (Transform child in playerPanelParent) Destroy(child.gameObject);
        _playerPanels.Clear();

        LobbyOrchestrator.LobbyPlayersUpdated += NetworkLobbyPlayersUpdated;
        MatchmakingService.CurrentLobbyRefreshed += OnCurrentLobbyRefreshed;
        
        _ready = false;
        
        // Show appropriate buttons based on role
        bool isHost = InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started;
        startButton.SetActive(false); // Will be enabled when all players are ready
        readyButton.SetActive(!isHost); // Non-host players can ready up immediately
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
                var panel = Instantiate(playerPanelPrefab, playerPanelParent);
                panel.Init(player.Key);
                _playerPanels.Add(panel);
            }
        }

        bool isHost = InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started;
        bool allPlayersReady = players.Count > 0 && players.All(p => p.Value);
        
        startButton.SetActive(isHost && allPlayersReady);
        readyButton.SetActive(!isHost && !_ready);
    }

    private void OnCurrentLobbyRefreshed(Lobby lobby) {
        waitingText.text = $"Waiting on players... {lobby.Players.Count}/{lobby.MaxPlayers}";
    }

    public void OnReadyClicked() {
        readyButton.SetActive(false);
        _ready = true;
    }

    public void OnStartClicked() {
        StartPressed?.Invoke();
    }
}