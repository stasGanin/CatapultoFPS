using UnityEngine;

/// <summary>
/// Placing and demolishing interior stations for CastleBuildController:
/// ghost on the floor grid, R rotates 90°, LMB builds; demolish refunds the full cost.
/// </summary>
public sealed class StationBuildTool
{
    static readonly Color DemolishTint = new Color(1f, 0.3f, 0.25f, 1f);

    readonly PlayerInventory _inventory;
    StationDefinition _definition;
    GameObject _ghost;
    int _quarterTurns;
    StationPlacement.Result _snap;
    bool _hasSnap;
    PlacedStation _demolishTarget;
    readonly System.Collections.Generic.Dictionary<Renderer, Color> _savedColors = new System.Collections.Generic.Dictionary<Renderer, Color>();

    public StationBuildTool(PlayerInventory inventory)
    {
        _inventory = inventory;
    }

    public bool IsActive => _definition != null;
    public PlacedStation DemolishTarget => _demolishTarget;

    public void Begin(StationDefinition definition)
    {
        End();
        _definition = definition;
        _quarterTurns = 0;
        _ghost = StationFactory.CreateGhost(definition);
        CastleBuildController.ApplyGhostMaterials(_ghost);
    }

    public void End()
    {
        if (_ghost != null)
            Object.Destroy(_ghost);
        _ghost = null;
        _definition = null;
        _hasSnap = false;
        SetDemolishTarget(null);
    }

    public void Rotate() => _quarterTurns = (_quarterTurns + 1) % 4;

    public void Tick(CarcassCastle castle, Ray ray)
    {
        if (_ghost == null || castle == null)
            return;

        _hasSnap = StationPlacement.TrySnap(castle, ray, _definition, _quarterTurns, out _snap);
        if (_hasSnap)
        {
            _ghost.transform.SetPositionAndRotation(
                castle.transform.TransformPoint(_snap.LocalCenter),
                castle.transform.rotation * _snap.LocalRotation);
        }
        else
        {
            _ghost.transform.position = ray.origin + ray.direction * 5f;
        }

        CastleBuildController.TintGhost(_ghost, _hasSnap && _snap.IsValid && _definition.CanAfford(_inventory));
    }

    public bool TryPlace(CarcassCastle castle)
    {
        if (!_hasSnap || !_snap.IsValid || castle == null)
            return false;
        if (!_definition.TryPay(_inventory))
        {
            GameMessages.Post("Not enough resources");
            return false;
        }

        StationFactory.Create(_definition, castle.StationsRoot, _snap.LocalCenter, _snap.LocalRotation);
        HitSparkVfx.PlayDust(castle.transform.TransformPoint(_snap.LocalCenter) + Vector3.up * 0.3f, Vector3.up, 12);
        return true;
    }

    /// <summary>Станция своего замка под прицелом — для режима сноса.</summary>
    public PlacedStation FindDemolishTarget(Ray ray, float range)
    {
        if (!Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            return null;
        var station = hit.collider.GetComponentInParent<PlacedStation>();
        if (station == null)
            return null;
        var castle = station.GetComponentInParent<CarcassCastle>();
        return castle != null && castle.IsPlayerOwned ? station : null;
    }

    public void SetDemolishTarget(PlacedStation station)
    {
        if (station == _demolishTarget)
            return;
        Tint(_demolishTarget, false);
        _demolishTarget = station;
        Tint(_demolishTarget, true);
    }

    public string DemolishPrompt(PlacedStation station)
    {
        if (!station.CanDemolish(out string reason))
            return reason;
        return $"[LMB] Demolish {station.Definition.DisplayName} (refund: {station.Definition.CostLabel().Replace('\n', ',')})";
    }

    public void Demolish(PlacedStation station)
    {
        if (station == null || !station.CanDemolish(out _))
            return;
        station.Definition.Refund(_inventory);
        HitSparkVfx.PlayDust(station.transform.position + Vector3.up * 0.4f, Vector3.up, 16);
        if (station == _demolishTarget)
            _demolishTarget = null;
        Object.Destroy(station.gameObject);
    }

    /// <summary>У каждой детали заглушки свой цвет в PropertyBlock — запоминаем его перед подсветкой.</summary>
    void Tint(PlacedStation station, bool on)
    {
        if (station == null)
            return;
        var block = new MaterialPropertyBlock();
        foreach (var r in station.GetComponentsInChildren<Renderer>())
        {
            r.GetPropertyBlock(block);
            if (on)
            {
                _savedColors[r] = block.GetColor("_BaseColor");
                block.SetColor("_BaseColor", DemolishTint);
                block.SetColor("_Color", DemolishTint);
            }
            else if (_savedColors.TryGetValue(r, out Color original))
            {
                block.SetColor("_BaseColor", original);
                block.SetColor("_Color", original);
            }

            r.SetPropertyBlock(block);
        }

        if (!on)
            _savedColors.Clear();
    }
}
