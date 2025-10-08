using System;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
public class LobbyRoomPanel : MonoBehaviour {
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text typeText;
    [SerializeField] TMP_Text playerCountText;

    public Lobby Lobby { get; private set; }

    public static event Action<Lobby> LobbySelected;

    public void Init(Lobby lobby) {
        UpdateDetails(lobby);
    }

    public void UpdateDetails(Lobby lobby) {
        Lobby = lobby;
        nameText.text = lobby.Name;
        typeText.text = Constants.GameTypes[GetValue(Constants.GameTypeKey)];
        
        playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
        return;

        int GetValue(string key) {
            return int.Parse(lobby.Data[key].Value);
        }
    }

    public void Clicked() {
        LobbySelected?.Invoke(Lobby);
    }
}