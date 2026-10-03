using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carcass wall module (wall / window / door). Shows the clean mesh until the first hit,
/// then swaps to the pre-fractured art where each cell is a CastleWallChunk.
/// Columns, floors and roofs are intentionally not breakable.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CastleModuleRoot))]
public sealed class CarcassWallBreakable : MonoBehaviour, IDamageable
{
    // Когда целых кусков меньше этой доли — стена считается проломленной и осыпается целиком,
    // иначе остаются «зубы», которые блокируют проход, но уже не выглядят стеной.
    const float BreachAttachedFraction = 0.4f;
    const float BreachImpulse = 0.8f;

    static readonly List<CarcassWallBreakable> Registry = new List<CarcassWallBreakable>(64);

    readonly List<CastleWallChunk> _chunks = new List<CastleWallChunk>(40);

    CastleModuleRoot _module;
    SquareCastle _castle;
    bool _shattered;
    bool _breached;

    public static IReadOnlyList<CarcassWallBreakable> All => Registry;

    public bool IsBreached => _breached;
    public bool IsPlayerOwned => Castle != null && Castle.IsPlayerOwned;
    public Vector3 AimPoint => transform.TransformPoint(Vector3.up * (CarcassMetrics.WallHeight * 0.5f));

    SquareCastle Castle
    {
        get
        {
            // Модуль могут перевесить при стройке, поэтому владельца не кешируем навсегда.
            if (_castle == null)
                _castle = GetComponentInParent<SquareCastle>();
            return _castle;
        }
    }

    void Awake()
    {
        _module = GetComponent<CastleModuleRoot>();
    }

    void OnEnable() => Registry.Add(this);
    void OnDisable() => Registry.Remove(this);

    public static CarcassWallBreakable FindNearestStanding(Vector3 from, bool playerOwned)
    {
        CarcassWallBreakable best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < Registry.Count; i++)
        {
            var wall = Registry[i];
            if (wall == null || wall._breached || wall.IsPlayerOwned != playerOwned)
                continue;
            float sqr = (wall.AimPoint - from).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = wall;
            }
        }

        return best;
    }

    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (_breached || amount <= 0f || Castle == null)
            return;
        // Игрок не ломает свой замок, враги — свой.
        if (info.FromPlayer == IsPlayerOwned)
            return;

        if (!_shattered)
            Shatter();

        CastleWallChunk chunk = NearestAttached(info.Point);
        if (chunk != null)
            chunk.ApplyHit(Mathf.CeilToInt(amount), info.Point, info.Normal, info.FromPlayer);
    }

    /// <summary>Called by CastleModuleRoot whenever one of our cells breaks off.</summary>
    public void OnChunkDetached(CastleWallChunk detached)
    {
        if (_breached || !_shattered)
            return;
        if (AttachedFraction() >= BreachAttachedFraction)
            return;

        _breached = true;
        Vector3 outward = detached != null
            ? EnemySenses.Flatten(detached.transform.position - transform.position)
            : transform.right;
        for (int i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            if (chunk != null && !chunk.IsDetached)
                chunk.Detach(chunk.transform.position, outward, BreachImpulse);
        }

        if (!IsPlayerOwned)
            LootDrop.Module(_module.Kind, AimPoint);
    }

    public void OnRepaired()
    {
        _breached = false;
    }

    void Shatter()
    {
        _shattered = true;

        GameObject broken = CarcassKit.CreateBrokenArt(_module.Kind, transform);
        if (broken != null)
            RemoveIntactVisuals(broken.transform);

        // Целые куски превращаются в CastleWallChunk; без нарезанного арта вся стена — один кусок.
        _module.PrepareAuthoredPieces();
        _chunks.Clear();
        GetComponentsInChildren(true, _chunks);
    }

    void RemoveIntactVisuals(Transform keep)
    {
        foreach (var col in GetComponents<Collider>())
            Destroy(col);

        // Створка двери живёт в целой модели — после пролома дверь просто исчезает.
        var door = GetComponent<InteractableDoor>();
        if (door != null)
            Destroy(door);

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == keep)
                continue;
            // Отцепляем сразу: Destroy отложен до конца кадра, а PrepareAuthoredPieces ищет меши уже сейчас.
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    CastleWallChunk NearestAttached(Vector3 point)
    {
        CastleWallChunk best = null;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _chunks.Count; i++)
        {
            var chunk = _chunks[i];
            if (chunk == null || chunk.IsDetached)
                continue;
            float sqr = (chunk.transform.position - point).sqrMagnitude;
            var rend = chunk.GetComponent<Renderer>();
            if (rend != null)
                sqr = (rend.bounds.center - point).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = chunk;
            }
        }

        return best;
    }

    float AttachedFraction()
    {
        if (_chunks.Count == 0)
            return 0f;
        int attached = 0;
        for (int i = 0; i < _chunks.Count; i++)
        {
            if (_chunks[i] != null && !_chunks[i].IsDetached)
                attached++;
        }

        return attached / (float)_chunks.Count;
    }
}
