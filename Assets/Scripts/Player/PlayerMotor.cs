using UnityEngine;

/// <summary>
/// Канон FPS: CharacterController.Move в Update + Time.deltaTime.
/// Мгновенный разворот (Doom-style). Корень не крутим — look на YawPivot.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [SerializeField] PlayerMotorConfig _config;
    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerLook _look;
    [SerializeField] CharacterController _controller;

    float _verticalVelocity;

    void Awake()
    {
        if (_controller == null)
            _controller = GetComponent<CharacterController>();
        if (_look == null)
            _look = GetComponent<PlayerLook>();

        if (_controller != null)
        {
            _controller.enabled = true;
            _controller.minMoveDistance = 0f;
        }
    }

    void Update()
    {
        if (_config == null || _input == null || _controller == null || _look == null)
            return;

        float dt = Time.deltaTime;
        bool grounded = _controller.isGrounded;

        if (grounded && _verticalVelocity < 0f)
            _verticalVelocity = _config.GroundedVerticalVelocity;

        // Сначала обновляем look — движение в том же кадре смотрит куда камера
        _look.TickLook();

        Vector3 move =
            _look.PlanarRight * _input.Move.x +
            _look.PlanarForward * _input.Move.y;
        move.y = 0f;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float speed = _input.SprintHeld ? _config.SprintSpeed : _config.WalkSpeed;
        move *= speed;

        if (_input.JumpPressed && grounded)
            _verticalVelocity = Mathf.Sqrt(_config.JumpHeight * -2f * _config.Gravity);

        _verticalVelocity += _config.Gravity * dt;
        move.y = _verticalVelocity;

        _controller.Move(move * dt);
    }
}
