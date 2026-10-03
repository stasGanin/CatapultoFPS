using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Debug toggles for enemy castle spawners (F8) and enemy castle towers (F7). Both off until pressed.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class EnemySpawnDebugHud : MonoBehaviour
{
    public static bool SpawningEnabled { get; private set; }
    public static bool TowersEnabled { get; private set; }

    Text _label;
    Image _background;
    Text _towersLabel;
    Image _towersBackground;
    bool _applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<EnemySpawnDebugHud>() != null)
            return;
        var go = new GameObject("EnemySpawnDebugHud");
        go.AddComponent<EnemySpawnDebugHud>();
    }

    void Awake()
    {
        // По умолчанию враги выключены — включаются по F8, когда нужен бой.
        SpawningEnabled = false;
        TowersEnabled = false;
        _applied = false;
        EnsureEventSystem();
        Build();
        RefreshVisual();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f8Key.wasPressedThisFrame)
            SetEnabled(!SpawningEnabled);
        if (kb != null && kb.f7Key.wasPressedThisFrame)
            SetTowersEnabled(!TowersEnabled);

        ApplyGate();
    }

    void SetEnabled(bool on)
    {
        if (SpawningEnabled == on)
            return;
        SpawningEnabled = on;
        _applied = false;
        RefreshVisual();
    }

    void SetTowersEnabled(bool on)
    {
        TowersEnabled = on;
        RefreshVisual();
    }

    void ApplyGate()
    {
        var wallSpawners = FindObjectsByType<EnemyCastleSpawnModule>(FindObjectsSortMode.None);
        for (int i = 0; i < wallSpawners.Length; i++)
        {
            var spawner = wallSpawners[i];
            if (spawner == null || !spawner.IsOperational)
                continue;
            if (!SpawningEnabled)
                spawner.enabled = false;
            else if (!_applied)
                spawner.enabled = true;
        }

        var cubes = FindObjectsByType<EnemySpawnerModule>(FindObjectsSortMode.None);
        for (int i = 0; i < cubes.Length; i++)
        {
            var spawner = cubes[i];
            if (spawner == null)
                continue;
            if (!SpawningEnabled)
                spawner.enabled = false;
            else if (!_applied)
                spawner.enabled = true;
        }

        _applied = true;
    }

    void RefreshVisual()
    {
        ApplyToggleVisual(_label, _background, SpawningEnabled ? "Enemies ON    [F8]" : "Enemies OFF    [F8]", SpawningEnabled);
        ApplyToggleVisual(_towersLabel, _towersBackground, TowersEnabled ? "Towers ON    [F7]" : "Towers OFF    [F7]", TowersEnabled);
    }

    static void ApplyToggleVisual(Text label, Image background, string text, bool isOn)
    {
        if (label != null)
            label.text = text;
        if (background != null)
            background.color = isOn
                ? new Color(0.42f, 0.62f, 0.36f, 0.92f)
                : new Color(0.55f, 0.38f, 0.32f, 0.92f);
    }

    void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        var canvasGo = new GameObject("SpawnToggleCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        UiScale.Configure(canvasGo.AddComponent<CanvasScaler>());
        canvasGo.AddComponent<GraphicRaycaster>();

        (_background, _label) = CreateToggle(canvasGo.transform, font, "SpawnToggle", -24f, () => SetEnabled(!SpawningEnabled));
        (_towersBackground, _towersLabel) = CreateToggle(canvasGo.transform, font, "TowersToggle", -72f, () => SetTowersEnabled(!TowersEnabled));
    }

    static (Image background, Text label) CreateToggle(Transform parent, Font font, string name, float y, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, y);
        rt.sizeDelta = new Vector2(200f, 40f);

        var background = go.AddComponent<Image>();
        background.color = new Color(0.10f, 0.13f, 0.18f, 0.92f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = background;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.85f, 1f);
        colors.pressedColor = new Color(0.82f, 0.78f, 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 2f);
        textRt.offsetMax = new Vector2(-8f, -2f);
        var label = textGo.AddComponent<Text>();
        label.font = font;
        label.fontSize = 16;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        return (background, label);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }
}
