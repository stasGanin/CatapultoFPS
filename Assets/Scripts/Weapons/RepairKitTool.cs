using UnityEngine;

/// <summary>Hotbar repair kit: aim at an owned castle module, LMB restores all remaining pieces.</summary>
[DefaultExecutionOrder(25)]
public sealed class RepairKitTool : MonoBehaviour
{
    const float Range = 8f;
    const string KitId = "repair_kit";

    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;

    CastleModuleRoot _aimed;
    bool _aimedNeedsRepair;

    public static string AimPrompt { get; private set; }

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
    }

    void OnDisable() => ClearAim();

    void Update()
    {
        if (!HoldingKit() || (_inventory != null && _inventory.BlocksGameplayInput))
        {
            ClearAim();
            return;
        }

        TickAim();

        if (_input != null && _input.AttackPressed && _aimed != null && _aimedNeedsRepair)
            TryRepair();
    }

    bool HoldingKit()
    {
        ItemDefinition item = _inventory != null ? _inventory.SelectedItem : null;
        return item != null && item.Id == KitId;
    }

    void TickAim()
    {
        CastleModuleRoot module = FindAimedModule();
        if (module != null && !module.BelongsToPlayerCastle)
            module = null;
        bool needs = module != null && module.NeedsRepair();
        SetAimed(module, needs);
    }

    CastleModuleRoot FindAimedModule()
    {
        if (_camera == null)
            return null;

        var ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, Range, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
            {
                var fromHit = hit.collider.GetComponentInParent<CastleModuleRoot>();
                if (fromHit != null && fromHit.BelongsToPlayerCastle)
                    return fromHit;
            }
        }

        return FindModuleAlongRay(ray);
    }

    static CastleModuleRoot FindModuleAlongRay(Ray ray)
    {
        var castle = SquareCastle.FindPlayerOwned();
        if (castle == null)
            return null;

        var modules = castle.GetComponentsInChildren<CastleModuleRoot>(true);
        CastleModuleRoot best = null;
        float bestAlong = Range;
        for (int i = 0; i < modules.Length; i++)
        {
            var module = modules[i];
            if (module == null)
                continue;
            Vector3 center = module.transform.position + module.transform.up * (module.Height * 0.45f);
            Vector3 to = center - ray.origin;
            float along = Vector3.Dot(to, ray.direction);
            if (along < 0.5f || along > Range)
                continue;
            Vector3 closest = ray.origin + ray.direction * along;
            if (Vector3.Distance(closest, center) > 3.2f)
                continue;
            if (along < bestAlong)
            {
                bestAlong = along;
                best = module;
            }
        }

        return best;
    }

    void SetAimed(CastleModuleRoot module, bool needsRepair)
    {
        if (_aimed == module && _aimedNeedsRepair == needsRepair)
        {
            RefreshPrompt();
            return;
        }

        if (_aimed != null)
            _aimed.SetRepairHighlight(false);

        _aimed = module;
        _aimedNeedsRepair = needsRepair;
        if (_aimed != null)
            _aimed.SetRepairHighlight(true, needsRepair);
        RefreshPrompt();
    }

    void ClearAim()
    {
        if (_aimed != null)
            _aimed.SetRepairHighlight(false);
        _aimed = null;
        _aimedNeedsRepair = false;
        AimPrompt = null;
    }

    void RefreshPrompt()
    {
        if (_aimed == null || !_aimed.BelongsToPlayerCastle)
        {
            AimPrompt = null;
            return;
        }

        AimPrompt = _aimedNeedsRepair ? "LMB — Repair module" : "Module intact";
    }

    void TryRepair()
    {
        if (_aimed == null || !_aimed.TryRepairFull())
            return;
        _inventory.TryConsumeSelected(1);
        _aimedNeedsRepair = false;
        _aimed.SetRepairHighlight(true, false);
        RefreshPrompt();
    }
}
