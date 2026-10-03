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
        float diameter = _config.ProjectileRadius * 2f;

        ProjectileVfx.SpawnMuzzleFlash(_config.MuzzleFlash, _muzzle, origin, direction);

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Cannonball";
        ball.transform.position = origin + direction * (_config.ProjectileRadius + 0.05f);
        ball.transform.rotation = Quaternion.LookRotation(direction);
        ball.transform.localScale = Vector3.one * diameter;

        var meshRenderer = ball.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.enabled = _config.ProjectileVisual == null;

        CharacterController playerController = GetComponent<CharacterController>();
        SphereCollider ballCollider = ball.GetComponent<SphereCollider>();
        if (ballCollider != null)
            ballCollider.isTrigger = true;
        if (playerController != null && ballCollider != null)
            Physics.IgnoreCollision(ballCollider, playerController, true);

        Rigidbody body = ball.AddComponent<Rigidbody>();
        body.mass = _config.ProjectileMass;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = direction * _config.MuzzleSpeed;

        Cannonball projectile = ball.AddComponent<Cannonball>();
        projectile.Init(
            _config.BlastRadius,
            _config.ExplosionForce,
            _config.ProjectileLifetime,
            _config.ImpactVisual);

        var vfxAnchor = new GameObject("Vfx");
        vfxAnchor.transform.SetParent(ball.transform, false);
        float ballScale = ball.transform.localScale.x;
        if (ballScale > 1e-4f)
            vfxAnchor.transform.localScale = Vector3.one / ballScale;
        ProjectileVfx.AttachFlight(vfxAnchor.transform, _config.ProjectileVisual, direction);

        if (_config.ProjectileVisual == null)
            ApplyColor(ball, new Color(0.12f, 0.12f, 0.14f));
    }

    static void ApplyColor(GameObject target, Color color)
    {
        var renderer = target.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);

        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
