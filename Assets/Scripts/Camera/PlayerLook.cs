using UnityEngine;

/// <summary>
/// Look только на YawPivot. TickLook вызывается из PlayerMotor в том же Update.
/// </summary>
public class PlayerLook : MonoBehaviour
{
    [SerializeField] LookConfig _config;
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Transform _yawPivot;
    [SerializeField] Transform _cameraPivot;
    [SerializeField] Camera _camera;

    float _yaw;
    float _pitch;
    bool _tickedThisFrame;

    public Vector3 PlanarForward => _yawPivot != null ? _yawPivot.forward : transform.forward;
    public Vector3 PlanarRight => _yawPivot != null ? _yawPivot.right : transform.right;

    void Awake()
    {
        if (_config == null)
            Debug.LogError("PlayerLook: не назначен LookConfig.", this);
        if (_input == null)
            Debug.LogError("PlayerLook: не назначен PlayerInputReader.", this);
        if (_yawPivot == null)
            Debug.LogError("PlayerLook: не назначен yaw pivot.", this);
        if (_cameraPivot == null)
            Debug.LogError("PlayerLook: не назначен camera pivot.", this);

        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();

        GameSettings.EnsureLoaded();

        if (_camera != null)
            _camera.fieldOfView = GameSettings.FieldOfView;

        if (_yawPivot != null)
            _yaw = _yawPivot.localEulerAngles.y;
    }

    void OnEnable()
    {
        if (_inventory == null || !_inventory.BlocksLook)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void LateUpdate()
    {
        // Fallback, если Motor не вызвал TickLook
        if (!_tickedThisFrame)
            TickLook();
        _tickedThisFrame = false;
    }

    public void TickLook()
    {
        if (_config == null || _input == null || _yawPivot == null || _cameraPivot == null)
            return;

        _tickedThisFrame = true;

        // Инвентарь / меню строительства — не крутим камеру (призрак — крутим)
        if (_inventory != null && _inventory.BlocksLook)
            return;

        if (_camera != null)
            _camera.fieldOfView = GameSettings.FieldOfView;

        float sensitivity = GameSettings.LookSensitivity;
        if (_config != null && sensitivity <= 0.001f)
            sensitivity = _config.Sensitivity;
        bool invertY = GameSettings.InvertY || (_config != null && _config.InvertY);

        Vector2 look = _input.Look * sensitivity;
        _yaw += look.x;
        float pitchDelta = invertY ? look.y : -look.y;
        float minPitch = _config != null ? _config.MinPitch : -89f;
        float maxPitch = _config != null ? _config.MaxPitch : 89f;
        _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);

        _yawPivot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
