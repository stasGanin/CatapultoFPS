using UnityEngine;

/// <summary>Map node marker for T1 location (castle pad or settlement/dungeon).</summary>
public enum MapNodeKind
{
    PlayerCastle = 0,
    EnemyCastle = 1,
    Settlement = 2,
    BossCastle = 3,
}

public sealed class MapNodeMarker : MonoBehaviour
{
    [SerializeField] MapNodeKind _kind = MapNodeKind.EnemyCastle;
    [SerializeField] string _nodeId = "node";
    [SerializeField] int[] _linkedNodeIndices = System.Array.Empty<int>();

    public MapNodeKind Kind => _kind;
    public string NodeId => _nodeId;
    public int[] LinkedNodeIndices => _linkedNodeIndices;

    public void Setup(MapNodeKind kind, string id, int[] links)
    {
        _kind = kind;
        _nodeId = id;
        _linkedNodeIndices = links ?? System.Array.Empty<int>();
        gameObject.name = $"Node_{id}_{kind}";
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = _kind switch
        {
            MapNodeKind.PlayerCastle => new Color(0.2f, 0.75f, 0.35f),
            MapNodeKind.BossCastle => new Color(0.9f, 0.2f, 0.15f),
            MapNodeKind.Settlement => new Color(0.85f, 0.7f, 0.25f),
            _ => new Color(0.75f, 0.35f, 0.3f),
        };
        Gizmos.DrawWireCube(transform.position + Vector3.up * 1.2f, new Vector3(28f, 2.4f, 28f));
        Gizmos.DrawSphere(transform.position + Vector3.up * 2.4f, 0.9f);
    }
#endif
}
