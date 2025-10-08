using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class LobbyPlayerPanel : MonoBehaviour {
    [FormerlySerializedAs("_nameText")] [SerializeField] private TMP_Text nameText;
    [FormerlySerializedAs("_statusText")] [SerializeField] private TMP_Text statusText;

    public int PlayerId { get; private set; }

    public void Init(int playerId) {
        PlayerId = playerId;
        nameText.text = $"Player {playerId}";
    }

    public void SetReady() {
        statusText.text = "Ready";
        statusText.color = Color.green;
    }
}