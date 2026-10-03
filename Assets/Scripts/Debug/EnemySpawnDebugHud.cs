using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Debug toggle for enemy castle spawners. Off until pressed (or F8).
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class EnemySpawnDebugHud : MonoBehaviour
{
    public static bool SpawningEnabled { get; private set; }

    Text _label;
    Image _background;
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
        SpawningEnabled = true;
        _applied = true;
        EnsureEventSystem();
        Build();
        RefreshVisual();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f8Key.wasPressedThisFrame)
            SetEnabled(!SpawningEnabled);

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
        if (_label != null)
            _label.text = SpawningEnabled ? "Enemies ON    [F8]" : "Enemies OFF    [F8]";
        if (_background != null)
            _background.color = SpawningEnabled
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
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var go = new GameObject("SpawnToggle", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(200f, 40f);

        _background = go.AddComponent<Image>();
        _background.color = new Color(0.10f, 0.13f, 0.18f, 0.92f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = _background;
        btn.transition = Selectable.Transition.ColorTint;
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.95f, 0.85f, 1f);
        colors.pressedColor = new Color(0.82f, 0.78f, 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() => SetEnabled(!SpawningEnabled));

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(8f, 2f);
        textRt.offsetMax = new Vector2(-8f, -2f);
        _label = textGo.AddComponent<Text>();
        _label.font = font;
        _label.fontSize = 16;
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = Color.white;
        _label.raycastTarget = false;
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
