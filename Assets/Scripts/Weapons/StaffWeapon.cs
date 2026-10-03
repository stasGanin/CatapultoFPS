using UnityEngine;

/// <summary>
/// Hold attack to raise the staff, then drip homing missiles at the nearest hostile
/// while mana lasts. No target = raised pose, no spend.
/// </summary>
public sealed class StaffWeapon : MonoBehaviour
{
    public const string ConfigResource = "Weapons/StaffConfig";
    public const string ViewName = "StaffView";

    [SerializeField] StaffConfig _config;
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] PlayerMana _mana;
    [SerializeField] Camera _camera;
    [SerializeField] Transform _muzzle;
    [SerializeField] Transform _viewModel;

    float _raise;
    float _nextFireTime;
    Vector3 _restPos;
    Quaternion _restRot;
    bool _restCaptured;

    static readonly Collider[] SeekScratch = new Collider[64];

    public GameObject ViewModelObject => _viewModel != null ? _viewModel.gameObject : null;

    public void SetConfig(StaffConfig config) => _config = config;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_mana == null)
            _mana = GetComponent<PlayerMana>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        if (_config == null)
            _config = Resources.Load<StaffConfig>(ConfigResource);
        TryAttachArt();
        BindMuzzle();
        CaptureRestPose();
    }

    void OnDisable()
    {
        _raise = 0f;
        ApplyRaisePose();
    }

    void Update()
    {
        if (_config == null || _input == null || _camera == null)
            return;
        if (_inventory != null && _inventory.BlocksGameplayInput)
        {
            TickRaise(false);
            return;
        }

        bool holding = _input.AttackHeld;
        bool hasTarget = TryFindTarget(out Transform target, out Vector3 aim);
        bool canAfford = _mana == null || _mana.CanAfford(_config.ManaCostPerMissile);
        bool wantRaise = holding && (canAfford || !hasTarget);

        TickRaise(wantRaise);

        if (_raise < 0.99f || !holding)
            return;
        if (!hasTarget)
            return;
        if (!canAfford)
            return;
        if (Time.time < _nextFireTime)
            return;
        if (_mana != null && !_mana.TrySpend(_config.ManaCostPerMissile))
            return;

        Fire(target, aim);
    }

    void TickRaise(bool wantRaise)
    {
        float dur = wantRaise ? _config.RaiseDuration : _config.LowerDuration;
        float step = dur > 0.01f ? Time.deltaTime / dur : 1f;
        _raise = Mathf.Clamp01(_raise + (wantRaise ? step : -step));
        if (_raise < 0.5f)
            _nextFireTime = 0f;
        ApplyRaisePose();
    }

    void Fire(Transform target, Vector3 aim)
    {
        _nextFireTime = Time.time + _config.FireInterval;

        Vector3 origin = _muzzle != null
            ? _muzzle.position
            : _camera.transform.position + _camera.transform.up * 0.35f + _camera.transform.forward * 0.2f;
        Vector3 dir = aim - origin;
        if (dir.sqrMagnitude < 0.01f)
            dir = _camera.transform.forward;
        dir.Normalize();

        ProjectileVfx.SpawnMuzzleFlash(_config.MuzzleFlash, _muzzle, origin, dir);
        HomingMagicMissile.Spawn(
            origin,
            dir,
            target,
            _config.MissileSpeed,
            _config.MissileRadius,
            _config.MissileDamage,
            _config.MissileLifetime,
            gameObject,
            _config.ProjectileVisual,
            _config.ImpactVisual);
    }

    bool TryFindTarget(out Transform target, out Vector3 aim)
    {
        target = null;
        aim = Vector3.zero;
        Vector3 origin = transform.position + Vector3.up * 1.1f;
        float radius = _config != null ? _config.SearchRadius : 32f;
        int count = Physics.OverlapSphereNonAlloc(origin, radius, SeekScratch, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = SeekScratch[i];
            if (col == null || !IsHostileTarget(col))
                continue;
            Vector3 point = col.bounds.center;
            float d = (point - origin).sqrMagnitude;
            if (d >= best)
                continue;
            best = d;
            target = col.transform;
            aim = point;
        }

        return target != null;
    }

    public static bool IsHostileTarget(Collider col)
    {
        if (col == null)
            return false;
        if (col.GetComponentInParent<PlayerHealth>() != null)
            return false;
        if (col.GetComponentInParent<HomingMagicMissile>() != null)
            return false;

        if (col.GetComponentInParent<EnemyUnit>() != null)
            return true;
        if (col.GetComponentInParent<EnemyRoller>() != null)
            return true;
        if (col.GetComponentInParent<EnemyBrute>() != null)
            return true;
        if (col.GetComponentInParent<EnemyBomber>() != null)
            return true;
        if (col.GetComponentInParent<EnemyCastleMage>() != null)
            return true;
        if (col.GetComponentInParent<EnemyCastleSpawnModule>() != null)
            return true;
        if (col.GetComponentInParent<EnemySpawnerModule>() != null)
            return true;

        var chunk = col.GetComponentInParent<CastleWallChunk>();
        if (chunk != null)
            return !chunk.IsDetached && !chunk.BelongsToPlayerCastle;

        var module = col.GetComponentInParent<CastleModuleBreakable>();
        if (module != null)
        {
            var castle = module.GetComponentInParent<SquareCastle>();
            return castle == null || !castle.IsPlayerOwned;
        }

        var segment = col.GetComponentInParent<Catapulto.Battle.Castle.CastleSegmentView>();
        if (segment != null && !segment.IsDestroyed)
        {
            var castle = segment.GetComponentInParent<SquareCastle>();
            return castle == null || !castle.IsPlayerOwned;
        }

        return false;
    }

    void TryAttachArt()
    {
        if (_camera == null)
            return;

        Transform existing = _camera.transform.Find(ViewName);
        if (existing != null)
        {
            _viewModel = existing;
            return;
        }

        Transform art = WeaponViewModelFactory.AttachStaff(_camera.transform);
        if (art == null)
            return;
        art.name = ViewName;
        _viewModel = art;
    }

    void BindMuzzle()
    {
        if (_viewModel == null)
            return;
        var marker = _viewModel.GetComponentInChildren<WeaponMuzzle>(true);
        if (marker != null)
        {
            _muzzle = marker.transform;
            return;
        }

        Transform named = _viewModel.Find("Muzzle");
        if (named != null)
            _muzzle = named;
    }

    void CaptureRestPose()
    {
        if (_viewModel == null || _restCaptured)
            return;
        _restPos = _viewModel.localPosition;
        _restRot = _viewModel.localRotation;
        _restCaptured = true;
    }

    void ApplyRaisePose()
    {
        if (_viewModel == null || !_restCaptured)
            return;
        float t = _raise * _raise * (3f - 2f * _raise);
        Vector3 raisedPos = _restPos + new Vector3(-0.06f, 0.16f, -0.04f);
        Quaternion raisedRot = _restRot * Quaternion.Euler(-68f, -6f, 8f);
        _viewModel.localPosition = Vector3.Lerp(_restPos, raisedPos, t);
        _viewModel.localRotation = Quaternion.Slerp(_restRot, raisedRot, t);
    }
}
