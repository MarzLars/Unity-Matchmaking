using FishNet.Object;
using UnityEngine;

public class PlayerController : NetworkBehaviour {
    [SerializeField] private float _speed = 3;
    private Rigidbody _rb;

    private void Awake() {
        _rb = GetComponent<Rigidbody>();
    }

    private void Update() {
        if (!IsOwner) return;
        
        var dir = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
        _rb.linearVelocity = dir * _speed;
    }

    public override void OnStartClient() {
        base.OnStartClient();
        if (!IsOwner) {
            enabled = false;
        }
    }
}