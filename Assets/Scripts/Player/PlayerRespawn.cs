using UnityEngine;

/// <summary>
/// On death: freezes the player for a short delay, then respawns them next to their own mage.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerRespawn : MonoBehaviour
{
    // Смещение от мага, чтобы не возродиться внутри его коллайдера.
    static readonly Vector3 MageOffset = new Vector3(3f, 0.2f, 0f);

    PlayerHealth _health;
    PlayerMotor _motor;
    CharacterController _controller;
    PlayerCombatConfig _config;
    float _respawnAt = -1f;

    public bool IsWaiting => _respawnAt >= 0f;
    public float SecondsLeft => IsWaiting ? Mathf.Max(0f, _respawnAt - Time.time) : 0f;

    void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _motor = GetComponent<PlayerMotor>();
        _controller = GetComponent<CharacterController>();
        _config = PlayerCombatConfig.Load();
    }

    void OnEnable() => _health.Died += OnDied;
    void OnDisable() => _health.Died -= OnDied;

    void OnDied()
    {
        _respawnAt = Time.time + _config.RespawnDelay;
        if (_motor != null)
            _motor.enabled = false;
    }

    void Update()
    {
        if (!IsWaiting || Time.time < _respawnAt)
            return;

        _respawnAt = -1f;
        TeleportHome();
        _health.ResetFull();
        if (_motor != null)
            _motor.enabled = true;
    }

    void TeleportHome()
    {
        SquareCastle castle = SquareCastle.FindPlayerOwned();
        if (castle == null)
            return;

        Vector3 local = CarcassMetrics.CellCenterLocal(0, 0, 0) + MageOffset;
        Vector3 target = castle.transform.TransformPoint(local);
        // CharacterController перетирает transform.position, пока включён.
        if (_controller != null)
            _controller.enabled = false;
        transform.position = target;
        if (_controller != null)
            _controller.enabled = true;
    }
}
