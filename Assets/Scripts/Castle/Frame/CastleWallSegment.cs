using UnityEngine;

/// <summary>One replaceable wall chunk bound to a castle socket.</summary>
public sealed class CastleWallSegment : MonoBehaviour
{
    [SerializeField] bool _replaced;

    public bool IsReplaced => _replaced;

    public void SetReplaced(bool replaced)
    {
        _replaced = replaced;
        gameObject.SetActive(!replaced);
    }
}
