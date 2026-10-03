using UnityEngine;

[CreateAssetMenu(fileName = "LookConfig", menuName = "Catapulto/Camera/Look Config")]
public class LookConfig : ScriptableObject
{
    [SerializeField, Min(0f)] float _sensitivity = 0.15f;
    [SerializeField] float _minPitch = -89f;
    [SerializeField] float _maxPitch = 89f;
    [SerializeField] bool _invertY;
    [SerializeField, Min(1f)] float _fieldOfView = 75f;

    public float Sensitivity => _sensitivity;
    public float MinPitch => _minPitch;
    public float MaxPitch => _maxPitch;
    public bool InvertY => _invertY;
    public float FieldOfView => _fieldOfView;
}
