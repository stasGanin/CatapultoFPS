using UnityEngine;

/// <summary>
/// Сессия ручного управления башней на игроке: тело переносится к башне и отключается от физики
/// (с выключенным CharacterController по игроку не попасть), камера встаёт на «место», мышь крутит
/// ствол, ЛКМ стреляет ядрами из любых сундуков замка. E — выйти через люк.
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class TowerOperator : MonoBehaviour
{
    // Сколько урона получает игрок, если башню разнесли, пока он за пушкой.
    const float CollapseDamage = 50f;
    const float NoAmmoMessageCooldown = 2f;

    static TowerOperator _current;

    PlayerInputReader _input;
    PlayerInventory _inventory;
    PlayerHealth _health;
    PlayerMotor _motor;
    CharacterController _controller;
    Camera _camera;
    WeaponConfig _config;
    CastleTower _tower;
    int _enteredFrame;
    float _nextFireTime;
    float _nextNoAmmoMessageTime;

    public static bool IsOperating => _current != null && _current._tower != null;

    public static void Begin(PlayerInventory inventory, CastleTower tower)
    {
        var op = inventory.GetComponent<TowerOperator>();
        if (op == null)
            op = inventory.gameObject.AddComponent<TowerOperator>();
        op.Enter(tower);
    }

    public static void NotifyTowerGone(CastleTower tower, bool destroyedByDamage)
    {
        if (_current != null && _current._tower == tower)
            _current.Leave(destroyedByDamage);
    }

    void Enter(CastleTower tower)
    {
        _input = GetComponent<PlayerInputReader>();
        _inventory = GetComponent<PlayerInventory>();
        _health = GetComponent<PlayerHealth>();
        _motor = GetComponent<PlayerMotor>();
        _controller = GetComponent<CharacterController>();
        _camera = GetComponentInChildren<Camera>();
        if (_config == null)
            _config = Resources.Load<WeaponConfig>("Castle/Towers/CannonTowerConfig");
        if (_input == null || _camera == null || _config == null || CastleAmmoStorage.Cannonball == null)
        {
            Debug.LogError("TowerOperator: нет ввода, камеры, конфига башни или предмета-ядра.", this);
            return;
        }

        _tower = tower;
        _current = this;
        _enteredFrame = Time.frameCount;
        _nextFireTime = 0f;
        if (_controller != null)
            _controller.enabled = false;
        if (_motor != null)
            _motor.enabled = false;
        _inventory.NotifyGameplayBlockChanged();
        GameMessages.Post($"Tower: LMB fire · E leave · cannonballs in chests: {CastleAmmoStorage.Count(_tower.Castle)}");
    }

    void Update()
    {
        if (_tower == null)
        {
            Leave(false);
            return;
        }

        if (_health != null && _health.IsDead)
        {
            Leave(false);
            return;
        }

        // E на люке уже сработал в этом же кадре — иначе вход тут же превратился бы в выход.
        if (_input.InteractPressed && Time.frameCount != _enteredFrame)
        {
            Leave(false);
            return;
        }

        // Курсор свободен — открыто меню/пауза, стрелять нельзя.
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        bool wantsFire = _config.Automatic ? _input.AttackHeld : _input.AttackPressed;
        if (wantsFire && Time.time >= _nextFireTime)
            TryFire();
    }

    void LateUpdate()
    {
        if (_tower == null)
            return;

        // PlayerLook уже довернул камеру; сдвигаем корень так, чтобы камера оказалась на месте башни.
        transform.position += _tower.SeatWorld - _camera.transform.position;
        _tower.AimAt(_camera.transform.forward);
    }

    void TryFire()
    {
        if (!CastleAmmoStorage.TryConsume(_tower.Castle))
        {
            if (Time.time >= _nextNoAmmoMessageTime)
            {
                _nextNoAmmoMessageTime = Time.time + NoAmmoMessageCooldown;
                GameMessages.Post("No cannonballs in castle chests");
            }

            return;
        }

        _nextFireTime = Time.time + _config.FireInterval;
        Vector3 origin = _tower.Muzzle.position;
        Vector3 aimPoint = WeaponAim.GetPoint(_camera, gameObject, _config.AimRange);
        Vector3 direction = WeaponAim.GetDirection(origin, aimPoint, _camera.transform.forward);
        ProjectileVfx.SpawnMuzzleFlash(_config.MuzzleFlash, _tower.Muzzle, origin, direction);
        Cannonball.Launch(_config, origin, direction, _controller, _tower.GetComponent<Collider>());
    }

    /// <summary>Возвращает игрока в комнату под люком; при обрушении башни — ещё и с уроном.</summary>
    void Leave(bool tookDamage)
    {
        if (_current != this)
            return;

        Vector3 exit = _tower != null ? _tower.ExitPointWorld : transform.position;
        _tower = null;
        _current = null;
        transform.position = exit;
        if (_controller != null)
            _controller.enabled = true;
        if (_motor != null)
            _motor.enabled = true;
        _inventory.NotifyGameplayBlockChanged();

        if (tookDamage && _health != null)
            _health.ApplyDamage(CollapseDamage, new DamageInfo(exit, Vector3.up, Vector3.down, fromPlayer: false));
    }

    void OnDisable()
    {
        if (_current == this)
            Leave(false);
    }
}
