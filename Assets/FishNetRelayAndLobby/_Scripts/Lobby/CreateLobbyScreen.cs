using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class CreateLobbyScreen : MonoBehaviour {
    [FormerlySerializedAs("_nameInput")] [SerializeField] private TMP_InputField nameInput;
    [FormerlySerializedAs("_maxPlayersInput")] [SerializeField] private TMP_InputField maxPlayersInput;
    [FormerlySerializedAs("_typeDropdown")] [SerializeField] private TMP_Dropdown typeDropdown;
    [FormerlySerializedAs("_difficultyDropdown")] [SerializeField] private TMP_Dropdown difficultyDropdown;

    private void Start() {
        SetOptions(typeDropdown, Constants.GameTypes);
        SetOptions(difficultyDropdown, Constants.Difficulties);

        void SetOptions(TMP_Dropdown dropdown, IEnumerable<string> values) {
            dropdown.options = values.Select(type => new TMP_Dropdown.OptionData { text = type }).ToList();
        }
    }

    public static event Action<LobbyData> LobbyCreated;

    public void OnCreateClicked() {
        var lobbyData = new LobbyData {
            Name = nameInput.text,
            MaxPlayers = int.Parse(maxPlayersInput.text),
            Difficulty = difficultyDropdown.value,
            Type = typeDropdown.value
        };

        LobbyCreated?.Invoke(lobbyData);
    }
}

public struct LobbyData {
    public string Name;
    public int MaxPlayers;
    public int Difficulty;
    public int Type;
}