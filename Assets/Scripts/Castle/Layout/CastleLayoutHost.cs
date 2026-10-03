using UnityEngine;

/// <summary>Runtime castle assembled from a section layout. Rebuilds placeholder geometry in place.</summary>
public sealed class CastleLayoutHost : MonoBehaviour
{
    [SerializeField] bool _playerOwned;
    [SerializeField] string _saveId;
    [SerializeField] bool _showSockets;

    CastleLayoutData _data = CastleLayoutData.DefaultPlayer();

    public bool PlayerOwned => _playerOwned;
    public string SaveId => _saveId;
    public CastleLayoutData Data => _data;
    public bool ShowSockets => _showSockets;

    public void Bind(CastleLayoutData data, string saveId, bool playerOwned)
    {
        _data = data ?? CastleLayoutData.DefaultPlayer();
        _saveId = saveId;
        _playerOwned = playerOwned;
    }

    public void SetSocketsVisible(bool visible)
    {
        if (_showSockets == visible)
            return;
        _showSockets = visible;
        Rebuild();
    }

    public void Rebuild()
    {
        CastleLayoutBuilder.Build(this);
    }

    public bool Save()
    {
        return CastleLayoutIO.Save(_saveId, _data);
    }

    public static CastleLayoutHost FindPlayer()
    {
        var all = FindObjectsByType<CastleLayoutHost>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].PlayerOwned)
                return all[i];
        }

        return null;
    }

    public static CastleLayoutHost FindEnemy()
    {
        var all = FindObjectsByType<CastleLayoutHost>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !all[i].PlayerOwned)
                return all[i];
        }

        return null;
    }
}
