using UnityEngine;

/// <summary>Placed building piece — used for neighbor snap queries.</summary>
[DisallowMultipleComponent]
public sealed class BuildingPiece : MonoBehaviour
{
    [SerializeField] BuildingDefinition _definition;
    [SerializeField] Vector3 _size = new Vector3(2f, 0.3f, 2f);

    public BuildingDefinition Definition => _definition;
    public Vector3 Size => _definition != null ? _definition.Size : _size;
    public string DefinitionId => _definition != null ? _definition.Id : string.Empty;

    public void Init(BuildingDefinition definition)
    {
        _definition = definition;
        if (definition != null)
            _size = definition.Size;
    }

    /// <summary>World positions of edge snap sockets (N/E/S/W) for foundations.</summary>
    public void GetEdgeSnapPoints(System.Collections.Generic.List<Vector3> into)
    {
        into.Clear();
        Vector3 size = Size;
        float hx = size.x * 0.5f;
        float hz = size.z * 0.5f;
        // Size is already the world footprint. Do not use TransformPoint with Size
        // offsets — runtime cubes set localScale = Size and would double the distance.
        Vector3 p = transform.position;
        into.Add(p + transform.forward * hz);
        into.Add(p + transform.right * hx);
        into.Add(p - transform.forward * hz);
        into.Add(p - transform.right * hx);
    }
}
