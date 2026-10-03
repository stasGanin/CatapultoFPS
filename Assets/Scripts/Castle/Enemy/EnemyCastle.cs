using System.Collections.Generic;
using UnityEngine;

/// <summary>Enemy triangular castle — mage death turns walls into physics debris.</summary>
public sealed class EnemyCastle : MonoBehaviour
{
    [SerializeField] EnemyCastleMage _mage;
    const float CollapseImpulse = 4.2f;

    readonly List<Transform> _collapsePieces = new();
    readonly List<MonoBehaviour> _spawnModules = new();
    bool _collapsed;

    public void RegisterPiece(Transform piece)
    {
        if (piece != null)
            _collapsePieces.Add(piece);
    }

    public void RegisterSpawner(MonoBehaviour spawner)
    {
        if (spawner != null)
            _spawnModules.Add(spawner);
    }

    public void BindMage(EnemyCastleMage mage) => _mage = mage;

    public void OnMageKilled(in DamageInfo info)
    {
        if (_collapsed)
            return;
        _collapsed = true;
        ReleaseLootChests();

        for (int i = 0; i < _spawnModules.Count; i++)
        {
            if (_spawnModules[i] != null)
                _spawnModules[i].enabled = false;
        }

        Vector3 blast = info.Point.sqrMagnitude > 0.01f ? info.Point : transform.position + Vector3.up;
        CastleMagicBlast.Spawn(transform.position + Vector3.up * 2f, 16f);

        // Целые стены каркаса ещё не нарезаны на куски — режем и обрушиваем их все.
        var carcassWalls = GetComponentsInChildren<CarcassWallBreakable>(true);
        for (int i = 0; i < carcassWalls.Length; i++)
            carcassWalls[i].Collapse(blast, CollapseImpulse);

        var modules = GetComponentsInChildren<CastleModuleRoot>(true);
        if (modules.Length > 0)
        {
            var chunks = GetComponentsInChildren<CastleWallChunk>(true);
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                if (chunk == null || chunk.IsDetached)
                    continue;
                Vector3 outward = chunk.transform.position - blast;
                chunk.Detach(chunk.transform.position, outward, CollapseImpulse);
            }
        }
        else
        {
            for (int i = 0; i < _collapsePieces.Count; i++)
            {
                var piece = _collapsePieces[i];
                if (piece == null)
                    continue;
                ShatterPiece(piece.gameObject, blast);
            }
        }

        if (_mage != null)
            Destroy(_mage.gameObject);

        Destroy(gameObject, 1.6f);
    }

    void ReleaseLootChests()
    {
        var chests = GetComponentsInChildren<StorageContainer>(true);
        for (int i = 0; i < chests.Length; i++)
        {
            if (chests[i] == null)
                continue;
            chests[i].transform.SetParent(null, true);
        }
    }

    static void ShatterPiece(GameObject piece, Vector3 blastPoint)
    {
        if (piece == null)
            return;

        Vector3 center = piece.transform.position;
        Vector3 scale = piece.transform.lossyScale;
        Quaternion rot = piece.transform.rotation;
        Color color = new Color(0.45f, 0.28f, 0.26f);
        var renderer = piece.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            // keep default rubble color
        }

        Destroy(piece);

        int chunks = 3;
        for (int i = 0; i < chunks; i++)
        {
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "CastleRubble";
            debris.transform.position = center + Random.insideUnitSphere * 0.4f;
            debris.transform.rotation = Random.rotation;
            float s = Mathf.Max(0.35f, Mathf.Min(scale.x, scale.y, scale.z) * Random.Range(0.25f, 0.55f));
            debris.transform.localScale = Vector3.one * s;

            ApplyColor(debris, color);

            var body = debris.AddComponent<Rigidbody>();
            body.mass = 2.5f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Vector3 push = (debris.transform.position - blastPoint).normalized;
            if (push.sqrMagnitude < 0.01f)
                push = Random.onUnitSphere;
            push.y = Mathf.Abs(push.y) + 0.35f;
            body.AddForce(push * Random.Range(4f, 9f), ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * 6f, ForceMode.Impulse);

            var mine = debris.AddComponent<MineableDebris>();
            mine.ResetHp(8);

            Destroy(debris, 90f);
        }
    }

    static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
