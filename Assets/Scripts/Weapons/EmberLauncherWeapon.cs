using UnityEngine;

/// <summary>Lobs an ember bomb. Consumes ember charges.</summary>
public sealed class EmberLauncherWeapon : MonoBehaviour
{
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;
    [SerializeField] Transform _viewModel;
    [SerializeField] float _fireInterval = 1.15f;
    [SerializeField] float _lobSpeed = 18f;
    [SerializeField] float _damage = 22f;
    [SerializeField] float _blast = 2.8f;

    float _nextFire;
    Vector3 _viewLocal;
    ItemDefinition _ammo;

    public GameObject ViewModelObject => _viewModel != null ? _viewModel.gameObject : null;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        _ammo = Resources.Load<ItemDefinition>("Items/EmberChargeItem");
        TryAttachView();
        if (_viewModel != null)
            _viewLocal = _viewModel.localPosition;
    }

    void Update()
    {
        if (_input == null || _camera == null || !enabled)
            return;
        if (_inventory != null && _inventory.BlocksGameplayInput)
            return;
        if (!_input.AttackPressed || Time.time < _nextFire)
            return;
        Fire();
    }

    void Fire()
    {
        if (_ammo != null && _inventory != null && !_inventory.TryConsumeItem(_ammo, 1))
            return;

        _nextFire = Time.time + _fireInterval;
        if (_viewModel != null)
            _viewModel.localPosition = _viewLocal + Vector3.back * 0.05f;

        Vector3 origin = _camera.transform.position + _camera.transform.forward * 0.7f + _camera.transform.right * 0.18f;
        Vector3 aim = WeaponAim.GetPoint(_camera, gameObject, 50f);
        Vector3 dir = WeaponAim.GetDirection(origin, aim, _camera.transform.forward);
        dir = (dir + Vector3.up * 0.18f).normalized;
        EmberBomb.Spawn(origin, dir, _lobSpeed, _damage, _blast, gameObject, hurtPlayer: false);
    }

    void LateUpdate()
    {
        if (_viewModel != null)
            _viewModel.localPosition = Vector3.Lerp(_viewModel.localPosition, _viewLocal, Time.deltaTime * 10f);
    }

    void TryAttachView()
    {
        if (_camera == null)
            return;
        Transform existing = _camera.transform.Find("EmberLauncherView");
        if (existing != null)
        {
            _viewModel = existing;
            return;
        }

        _viewModel = WeaponViewModelFactory.AttachEmberLauncher(_camera.transform);
    }
}
