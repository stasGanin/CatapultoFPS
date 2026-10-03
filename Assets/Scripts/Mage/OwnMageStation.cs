using System;
using UnityEngine;

/// <summary>Player-owned mage heart. E opens the station: feed chronum, T1 repair-aura upgrade.</summary>
[DefaultExecutionOrder(-15)]
public sealed class OwnMageStation : MonoBehaviour, IPlayerInteractable
{
    public const float InteractRange = 3.2f;

    [SerializeField] float _satiety = 60f;
    [SerializeField] float _maxSatiety = 100f;
    [SerializeField] float _drainPerSecond = 0.35f;
    [SerializeField] int _feedChronumCost = 1;
    [SerializeField] float _feedAmount = 25f;
    [SerializeField] int _upgradeChronumCost = 5;
    [SerializeField] int _upgradeMetalCost = 8;
    [SerializeField] float _auraRadius = 10f;
    [SerializeField] float _auraHealPerSecond = 2.5f;

    static OwnMageStation _open;

    ItemDefinition _chronum;
    ItemDefinition _metal;
    bool _openFlag;
    int _upgradeLevel;
    float _auraAcc;
    Light _heartLight;
    float _baseIntensity;

    public static OwnMageStation OpenStation => _open;
    public bool IsOpen => _openFlag;
    public float Satiety => _satiety;
    public float MaxSatiety => _maxSatiety;
    public int UpgradeLevel => _upgradeLevel;
    public bool HasRepairAura => _upgradeLevel >= 1;
    public int FeedChronumCost => _feedChronumCost;
    public int UpgradeChronumCost => _upgradeChronumCost;
    public int UpgradeMetalCost => _upgradeMetalCost;

    public event Action Changed;
    public event Action<bool> OpenChanged;

    void Awake()
    {
        _chronum = Resources.Load<ItemDefinition>("Items/ChronumItem");
        _metal = Resources.Load<ItemDefinition>("Items/MetalItem");
        EnsureHitbox();
        _heartLight = GetComponentInChildren<Light>(true);
        if (_heartLight != null)
            _baseIntensity = _heartLight.intensity;
    }

    void Update()
    {
        if (_satiety > 0f)
        {
            _satiety = Mathf.Max(0f, _satiety - _drainPerSecond * Time.deltaTime);
            if (_openFlag)
                Changed?.Invoke();
        }

        if (HasRepairAura)
            TickAura();

        if (_heartLight != null)
        {
            float hungry = 1f - Mathf.Clamp01(_satiety / Mathf.Max(1f, _maxSatiety));
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * (1.4f + hungry * 2.2f));
            _heartLight.intensity = _baseIntensity * pulse * (0.75f + 0.25f * (1f - hungry));
            _heartLight.color = Color.Lerp(
                new Color(0.62f, 0.42f, 1f),
                new Color(0.95f, 0.35f, 0.2f),
                hungry);
        }

        if (!_openFlag)
            return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null || !IsInRange(player.transform.position))
        {
            Close();
            return;
        }

        var input = player.GetComponent<PlayerInputReader>();
        if (input != null && (input.CancelPressed || input.BuildMenuPressed || input.CraftMenuPressed))
            Close();
    }

    public bool IsInRange(Vector3 worldPos)
    {
        return (transform.position - worldPos).sqrMagnitude <= InteractRange * InteractRange;
    }

    public bool CanInteract() => isActiveAndEnabled;

    public void Interact(PlayerInventory inventory)
    {
        Toggle();
    }

    public void Toggle()
    {
        if (_openFlag)
            Close();
        else
            Open();
    }

    public void Open()
    {
        if (_openFlag)
            return;

        CloseOpen();
        var player = GameObject.FindGameObjectWithTag("Player");
        var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        inventory?.CloseMenuAndStorage();
        if (player != null)
        {
            player.GetComponent<CraftMenuController>()?.Close();
            player.GetComponent<BuildingController>()?.CloseAll();
            player.GetComponent<CastleBuildController>()?.CloseAll();
        }

        _openFlag = true;
        _open = this;
        ApplyCursor(true);
        OpenChanged?.Invoke(true);
        inventory?.NotifyGameplayBlockChanged();
    }

    public void Close()
    {
        if (!_openFlag)
            return;
        _openFlag = false;
        if (_open == this)
            _open = null;
        ApplyCursor(false);
        OpenChanged?.Invoke(false);
        var player = GameObject.FindGameObjectWithTag("Player");
        player?.GetComponent<PlayerInventory>()?.NotifyGameplayBlockChanged();
    }

    public static void CloseOpen()
    {
        _open?.Close();
    }

    public bool TryFeed(PlayerInventory inventory)
    {
        if (inventory == null || _chronum == null)
            return false;
        if (_satiety >= _maxSatiety - 0.01f)
            return false;
        if (!inventory.TryConsumeItem(_chronum, _feedChronumCost))
            return false;
        _satiety = Mathf.Min(_maxSatiety, _satiety + _feedAmount);
        Changed?.Invoke();
        return true;
    }

    public bool CanUpgrade(PlayerInventory inventory)
    {
        if (_upgradeLevel >= 1 || inventory == null || _chronum == null || _metal == null)
            return false;
        return inventory.CountItem(_chronum) >= _upgradeChronumCost
               && inventory.CountItem(_metal) >= _upgradeMetalCost;
    }

    public bool TryUpgrade(PlayerInventory inventory)
    {
        if (!CanUpgrade(inventory))
            return false;
        if (!inventory.TryConsumeItem(_chronum, _upgradeChronumCost))
            return false;
        if (!inventory.TryConsumeItem(_metal, _upgradeMetalCost))
        {
            inventory.TryAddItem(_chronum, _upgradeChronumCost);
            return false;
        }

        _upgradeLevel = 1;
        _maxSatiety = 150f;
        _satiety = Mathf.Min(_maxSatiety, _satiety + 20f);
        Changed?.Invoke();
        return true;
    }

    void TickAura()
    {
        _auraAcc += Time.deltaTime;
        if (_auraAcc < 1f)
            return;
        _auraAcc = 0f;

        var castle = GetComponentInParent<SquareCastle>();
        if (castle == null || !castle.IsPlayerOwned)
            return;

        float r2 = _auraRadius * _auraRadius;
        var chunks = castle.GetComponentsInChildren<CastleWallChunk>(true);
        int heal = Mathf.RoundToInt(_auraHealPerSecond);
        for (int i = 0; i < chunks.Length; i++)
        {
            var chunk = chunks[i];
            if (chunk == null || chunk.IsDetached)
                continue;
            if ((chunk.transform.position - transform.position).sqrMagnitude > r2)
                continue;
            chunk.Heal(heal);
        }
    }

    void EnsureHitbox()
    {
        if (GetComponent<Collider>() != null)
            return;
        var hit = gameObject.AddComponent<CapsuleCollider>();
        hit.isTrigger = true;
        hit.height = 3.2f;
        hit.radius = 0.7f;
        hit.center = new Vector3(0f, 1.6f, 0f);
    }

    void ApplyCursor(bool show)
    {
        if (show)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        var player = GameObject.FindGameObjectWithTag("Player");
        var inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        if (inventory != null && (inventory.IsMenuOpen || inventory.IsCraftOpen))
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        if (_openFlag)
            Close();
    }
}
