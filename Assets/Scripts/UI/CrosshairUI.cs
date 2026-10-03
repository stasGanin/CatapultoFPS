using UnityEngine;
using UnityEngine.UI;

/// <summary>Crosshair — edit arms under CrosshairCanvas in the GameUI prefab/scene.</summary>
public class CrosshairUI : MonoBehaviour
{
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] BuildingController _building;
    [SerializeField] CraftMenuController _craft;
    [SerializeField] OwnMageStation _mage;
    [SerializeField] GameObject _root;
    Image[] _arms;
    Color _idle = new Color(1f, 1f, 1f, 0.85f);
    Color _use = new Color(0.98f, 0.86f, 0.38f, 0.95f);
    // Хитмаркер: короткая красная вспышка и «раскрытие» прицела — игрок понимает, что попал.
    const float HitMarkerTime = 0.14f;
    const float HitMarkerScale = 1.35f;
    static readonly Color HitColor = new Color(1f, 0.25f, 0.2f, 1f);
    float _hitUntil;

    public void Bind(PlayerInventory inventory, BuildingController building)
    {
        Unhook();
        _inventory = inventory;
        _building = building;
        Hook();
        RefreshVisibility();
    }

    void Awake()
    {
        if (_inventory == null)
            _inventory = FindFirstObjectByType<PlayerInventory>();
        if (_building == null)
            _building = FindFirstObjectByType<BuildingController>();
        if (_craft == null)
            _craft = FindFirstObjectByType<CraftMenuController>();
        if (_mage == null)
            _mage = FindFirstObjectByType<OwnMageStation>();

        if (_root == null)
        {
            var t = transform.Find("CrosshairCanvas/Crosshair") ?? transform.Find("Crosshair");
            if (t != null)
                _root = t.gameObject;
        }

        // Legacy: component still on Player without scene UI → build once
        if (_root == null)
            BuildRuntimeFallback();
        if (_root != null && _arms == null)
            _arms = _root.GetComponentsInChildren<Image>(true);
    }

    void OnEnable()
    {
        Hook();
        CombatFeedback.HitConfirmed += OnHitConfirmed;
    }

    void OnDisable()
    {
        Unhook();
        CombatFeedback.HitConfirmed -= OnHitConfirmed;
    }

    void OnHitConfirmed(Vector3 _) => _hitUntil = Time.time + HitMarkerTime;
    void Start()
    {
        if (_mage == null)
            _mage = FindFirstObjectByType<OwnMageStation>();
        Hook();
        RefreshVisibility();
    }

    void Hook()
    {
        if (_inventory != null)
        {
            _inventory.MenuOpenChanged -= OnInventoryMenu;
            _inventory.MenuOpenChanged += OnInventoryMenu;
        }

        if (_building != null)
        {
            _building.ModeChanged -= OnBuildingMode;
            _building.ModeChanged += OnBuildingMode;
        }

        if (_craft != null)
        {
            _craft.OpenChanged -= OnCraftOpen;
            _craft.OpenChanged += OnCraftOpen;
        }

        if (_mage != null)
        {
            _mage.OpenChanged -= OnMageOpen;
            _mage.OpenChanged += OnMageOpen;
        }
    }

    void Unhook()
    {
        if (_inventory != null)
            _inventory.MenuOpenChanged -= OnInventoryMenu;
        if (_building != null)
            _building.ModeChanged -= OnBuildingMode;
        if (_craft != null)
            _craft.OpenChanged -= OnCraftOpen;
        if (_mage != null)
            _mage.OpenChanged -= OnMageOpen;
    }

    void OnInventoryMenu(bool _) => RefreshVisibility();
    void OnBuildingMode(BuildingMode _) => RefreshVisibility();
    void OnCraftOpen(bool _) => RefreshVisibility();
    void OnMageOpen(bool _) => RefreshVisibility();

    void RefreshVisibility()
    {
        bool hide = (_inventory != null && _inventory.BlocksLook)
                    || (_building != null && _building.enabled && _building.IsMenuOpen)
                    || (_craft != null && _craft.IsOpen)
                    || (_mage != null && _mage.IsOpen);
        if (_root != null)
            _root.SetActive(!hide);
    }

    void LateUpdate()
    {
        if (_root == null || !_root.activeSelf)
            return;
        if (_arms == null || _arms.Length == 0)
            _arms = _root.GetComponentsInChildren<Image>(true);

        bool hot = false;
        if (_inventory != null)
        {
            var look = _inventory.GetComponent<PlayerStorageInteractor>();
            hot = look != null && look.LookTarget != null && look.LookTarget.CanInteract();
        }

        float hit = Mathf.Clamp01((_hitUntil - Time.time) / HitMarkerTime);
        Color c = hit > 0f ? HitColor : hot ? _use : _idle;
        _root.transform.localScale = Vector3.one * Mathf.Lerp(1f, HitMarkerScale, hit);
        for (int i = 0; i < _arms.Length; i++)
        {
            if (_arms[i] != null)
                _arms[i].color = c;
        }
    }

    void BuildRuntimeFallback()
    {
        var canvasGo = new GameObject("CrosshairCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        UiScale.Configure(canvasGo.AddComponent<CanvasScaler>());

        _root = new GameObject("Crosshair", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        var rootRt = (RectTransform)_root.transform;
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta = Vector2.zero;

        CreateBar(_root.transform, Vector2.zero, new Vector2(4f, 4f));
        CreateBar(_root.transform, new Vector2(0f, 11f), new Vector2(2f, 10f));
        CreateBar(_root.transform, new Vector2(0f, -11f), new Vector2(2f, 10f));
        CreateBar(_root.transform, new Vector2(11f, 0f), new Vector2(10f, 2f));
        CreateBar(_root.transform, new Vector2(-11f, 0f), new Vector2(10f, 2f));
        _arms = _root.GetComponentsInChildren<Image>(true);
    }

    static void CreateBar(Transform parent, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject("Arm", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.85f);
        img.raycastTarget = false;
    }
}