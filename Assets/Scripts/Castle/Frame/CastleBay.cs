using UnityEngine;

/// <summary>
/// One wall bay on the castle frame: optional destructible shell + swappable content
/// (empty sliced wall OR same-footprint module prefab).
/// Author shells/content in the PlayerCastle prefab; runtime only swaps Content.
/// </summary>
[DisallowMultipleComponent]
public sealed class CastleBay : MonoBehaviour
{
    [Tooltip("Surrounding wall pieces (chunk destructibility). Leave empty if content is a full solid wall.")]
    [SerializeField] Transform _shellRoot;

    [Tooltip("Anchor for EmptyWall or Module prefab instance (same local footprint).")]
    [SerializeField] Transform _contentRoot;

    [Tooltip("Optional link to build socket for this bay.")]
    [SerializeField] CastleSocket _socket;

    [SerializeField] CastleWallSegment _legacySegment;

    public Transform ShellRoot => _shellRoot != null ? _shellRoot : null;
    public Transform ContentRoot => _contentRoot != null ? _contentRoot : transform;
    public CastleSocket Socket => _socket;
    public CastleWallSegment LegacySegment => _legacySegment;

    public CastleModuleBreakable CurrentModule =>
        ContentRoot != null ? ContentRoot.GetComponentInChildren<CastleModuleBreakable>(true) : null;

    public bool HasModule => CurrentModule != null && !CurrentModule.IsDestroyed;

    void Reset()
    {
        if (_contentRoot == null)
        {
            var existing = transform.Find("Content");
            if (existing != null)
                _contentRoot = existing;
        }

        if (_shellRoot == null)
        {
            var existing = transform.Find("Shell");
            if (existing != null)
                _shellRoot = existing;
        }

        if (_socket == null)
            _socket = GetComponent<CastleSocket>();
        if (_legacySegment == null)
            _legacySegment = GetComponent<CastleWallSegment>();
    }

    /// <summary>Ensure Content / Shell child folders exist for prefab authoring.</summary>
    [ContextMenu("Ensure Shell And Content Folders")]
    public void EnsureFolders()
    {
        if (_shellRoot == null)
        {
            var go = new GameObject("Shell");
            go.transform.SetParent(transform, false);
            _shellRoot = go.transform;
        }

        if (_contentRoot == null)
        {
            var go = new GameObject("Content");
            go.transform.SetParent(transform, false);
            _contentRoot = go.transform;
        }
    }

    /// <summary>
    /// Replace content with a prefab instance (module or empty wall). Destroys previous content children.
    /// Returns the new instance root, or null if prefab is null (clears content only).
    /// </summary>
    public GameObject SwapContent(GameObject prefab, bool worldPositionStays = false)
    {
        Transform anchor = ContentRoot;
        for (int i = anchor.childCount - 1; i >= 0; i--)
        {
            var child = anchor.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        if (_legacySegment != null)
            _legacySegment.SetReplaced(prefab != null);

        if (_socket != null && prefab == null)
        {
            // Socket occupancy is owned by CastleSocket.TryPlace — clearing only content here.
        }

        if (prefab == null)
            return null;

        GameObject instance = Instantiate(prefab, anchor);
        instance.name = prefab.name;
        if (!worldPositionStays)
        {
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        return instance;
    }
}
