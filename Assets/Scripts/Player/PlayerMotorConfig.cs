using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMotorConfig", menuName = "Catapulto/Player/Motor Config")]
public class PlayerMotorConfig : ScriptableObject
{
    [SerializeField, Min(0f)] float _walkSpeed = 5f;
    [SerializeField, Min(0f)] float _sprintSpeed = 8f;
    [SerializeField, Min(0f)] float _jumpHeight = 1.2f;
    [SerializeField] float _gravity = -20f;
    [SerializeField] float _groundedVerticalVelocity = -2f;

    public float WalkSpeed => _walkSpeed;
    public float SprintSpeed => _sprintSpeed;
    public float JumpHeight => _jumpHeight;
    public float Gravity => _gravity;
    public float GroundedVerticalVelocity => _groundedVerticalVelocity;
}
