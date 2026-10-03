using System.Collections.Generic;
using UnityEngine;

/// <summary>Tiered castle hull with fixed module sockets. Prefer editing the PlayerCastle_T1 prefab.</summary>
public sealed class CastleFrame : MonoBehaviour
{
    [SerializeField] int _tier = 1;
    [SerializeField] List<CastleSocket> _sockets = new();

    public int Tier => _tier;
    public IReadOnlyList<CastleSocket> Sockets => _sockets;

    public void Bind(int tier, List<CastleSocket> sockets)
    {
        _tier = tier;
        _sockets = sockets ?? new List<CastleSocket>();
    }

    /// <summary>Rebuild socket list from children — use after rearranging the prefab hierarchy.</summary>
    [ContextMenu("Refresh Sockets From Children")]
    public void RefreshSocketsFromChildren()
    {
        var found = GetComponentsInChildren<CastleSocket>(true);
        _sockets = new List<CastleSocket>(found.Length);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                _sockets.Add(found[i]);
        }
    }

    public CastleSocket FindFree(CastleSocketKind kind)
    {
        for (int i = 0; i < _sockets.Count; i++)
        {
            var s = _sockets[i];
            if (s != null && !s.IsOccupied && s.Kind == kind)
                return s;
        }
        return null;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_sockets == null || _sockets.Count == 0)
            RefreshSocketsFromChildren();
    }
#endif
}
