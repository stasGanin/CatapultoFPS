using UnityEngine;

/// <summary>
/// Ручная пушка: спавнит физическое ядро из дула.
/// </summary>
public class CannonWeapon : MonoBehaviour
{
    [SerializeField] WeaponConfig _config;
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;
    [SerializeField] Transform _muzzle;
    [SerializeField] Transform _viewModel;

    float _nextFireTime;
    Vector3 _viewModelLocalPos;

    public GameObject ViewModelObject => _viewModel != null ? _viewModel.gameObject : null;

    void Awake()
    {
        if (_config == null)
            Debug.LogError("CannonWeapon: не назначен WeaponConfig.", this);
        if (_input == null)
            Debug.LogError("CannonWeapon: не назначен PlayerInputReader.", this);
        if (_camera == null)
            Debug.LogError("CannonWeapon: не назначена Camera.", this);
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        TryAttachArt();

        if (_muzzle == null)
            Debug.LogError("CannonWeapon: не назначен muzzle.", this);

        if (_viewModel != null)
            _viewModelLocalPos = _viewModel.localPosition;
    }

    void TryAttachArt()
    {
        if (_camera == null)
            return;

        Transform art = WeaponViewModelFactory.AttachCannon(_camera.transform);
        if (art == null || art.GetComponentInChildren<Renderer>() == null)
            return;

        if (_viewModel != null && _viewModel != art)
            _viewModel.gameObject.SetActive(false);

        _viewModel = art;
        Transform muzzle = art.Find("Muzzle");
        if (muzzle != null)
            _muzzle = muzzle;
    }

    void Update()
    {
        if (_config == null || _input == null || _camera == null || _muzzle == null)
            return;
        if (_inventory != null && _inventory.BlocksGameplayInput)
            return;

        bool wantsFire = _config.Automatic ? _input.AttackHeld : _input.AttackPressed;
        if (wantsFire && Time.time >= _nextFireTime)
            Fire();
    }

    void Fire()
    {
        _nextFireTime = Time.time + _config.FireInterval;

        if (_viewModel != null)
            _viewModel.localPosition = _viewModelLocalPos;

        Vector3 origin = _muzzle.position;
        Vector3 aimPoint = WeaponAim.GetPoint(_camera, gameObject, _config.AimRange);
        Vector3 direction = WeaponAim.GetDirection(origin, aimPoint, _camera.transform.forward);

        ProjectileVfx.SpawnMuzzleFlash(_config.MuzzleFlash, _muzzle, origin, direction);
        Cannonball.Launch(_config, origin, direction, GetComponent<CharacterController>());
    }
}
