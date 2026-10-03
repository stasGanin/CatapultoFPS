using UnityEngine;

/// <summary>Fast automatic crossbow — light bolts for shredding spawners and troops.</summary>
public sealed class CrossbowWeapon : MonoBehaviour
{
    public const string ViewPrefabResource = "Weapons/CrossbowView";

    [SerializeField] WeaponConfig _config;
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;
    [SerializeField] Transform _muzzle;
    [SerializeField] Transform _viewModel;
    [SerializeField] float _boltDamage = 10f;

    float _nextFireTime;
    Vector3 _viewModelLocalPos;

    public GameObject ViewModelObject => _viewModel != null ? _viewModel.gameObject : null;

    public void SetConfig(WeaponConfig config) => _config = config;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        if (_config == null)
            _config = Resources.Load<WeaponConfig>("Weapons/CrossbowConfig");
        TryAttachArt();
        BindMuzzle();

        if (_viewModel != null)
            _viewModelLocalPos = _viewModel.localPosition;
    }

    void Update()
    {
        if (_config == null || _input == null || _camera == null)
            return;
        if (_inventory != null && _inventory.BlocksGameplayInput)
            return;
        if (!enabled)
            return;

        bool wantsFire = _config.Automatic ? _input.AttackHeld : _input.AttackPressed;
        if (wantsFire && Time.time >= _nextFireTime)
            Fire();
    }

    void Fire()
    {
        _nextFireTime = Time.time + _config.FireInterval;

        if (_viewModel != null)
            _viewModel.localPosition = _viewModelLocalPos + Vector3.back * 0.03f;

        Vector3 origin = _muzzle != null
            ? _muzzle.position
            : _camera.transform.position + _camera.transform.forward * 0.4f;
        Vector3 aimPoint = WeaponAim.GetPoint(_camera, gameObject, _config.AimRange);
        Vector3 direction = WeaponAim.GetDirection(origin, aimPoint, _camera.transform.forward);

        ProjectileVfx.SpawnMuzzleFlash(_config.MuzzleFlash, _muzzle, origin, direction);

        CrossbowBolt.Spawn(
            origin,
            direction,
            _config.MuzzleSpeed,
            _config.ProjectileRadius,
            _boltDamage,
            _config.ProjectileLifetime,
            gameObject,
            _config.ProjectileVisual,
            _config.ImpactVisual);
    }

    void TryAttachArt()
    {
        if (_camera == null)
            return;

        Transform existing = _camera.transform.Find("CrossbowView");
        if (existing != null)
        {
            _viewModel = existing;
            return;
        }

        var prefab = Resources.Load<GameObject>(ViewPrefabResource);
        if (prefab == null)
        {
            Transform art = WeaponViewModelFactory.AttachBallista(_camera.transform);
            if (art == null || art.GetComponentInChildren<Renderer>() == null)
                return;
            _viewModel = art;
            return;
        }

        GameObject view = Object.Instantiate(prefab, _camera.transform, false);
        view.name = "CrossbowView";
        _viewModel = view.transform;
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

        Transform named = FindChildNamed(_viewModel, "Muzzle");
        if (named != null)
            _muzzle = named;
    }

    static Transform FindChildNamed(Transform root, string name)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
                return all[i];
        }

        return null;
    }
}
