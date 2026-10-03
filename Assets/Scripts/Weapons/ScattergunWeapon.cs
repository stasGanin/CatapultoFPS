using UnityEngine;

/// <summary>Close-range scattergun. Consumes scatter shells.</summary>
public sealed class ScattergunWeapon : MonoBehaviour
{
    const string AmmoId = "scatter_shells";

    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;
    [SerializeField] Transform _viewModel;
    [SerializeField] float _fireInterval = 0.85f;
    [SerializeField] int _pellets = 7;
    [SerializeField] float _spread = 7.5f;
    [SerializeField] float _pelletDamage = 7f;
    [SerializeField] float _speed = 52f;

    float _nextFire;
    Vector3 _viewLocal;
    ItemDefinition _ammo;

    public ItemDefinition Ammo => _ammo;
    public GameObject ViewModelObject => _viewModel != null ? _viewModel.gameObject : null;

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        _ammo = Resources.Load<ItemDefinition>("Items/ScatterShellsItem");
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
            _viewModel.localPosition = _viewLocal + Vector3.back * 0.06f;

        Vector3 origin = _camera.transform.position + _camera.transform.forward * 0.55f;
        Vector3 aim = WeaponAim.GetPoint(_camera, gameObject, 40f);
        Vector3 dir = WeaponAim.GetDirection(origin, aim, _camera.transform.forward);
        ScatterPellet.SpawnVolley(origin, dir, _pellets, _spread, _speed, _pelletDamage, 0.55f, gameObject);
        HitSparkVfx.PlayDust(origin + dir * 0.3f, dir, 6);
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
        Transform existing = _camera.transform.Find("ScattergunView");
        if (existing != null)
        {
            _viewModel = existing;
            return;
        }

        _viewModel = WeaponViewModelFactory.AttachScattergun(_camera.transform);
    }
}
