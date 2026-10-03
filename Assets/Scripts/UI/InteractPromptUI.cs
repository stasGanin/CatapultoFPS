using UnityEngine;
using UnityEngine.UI;

/// <summary>Center prompt above the crosshair when looking at something usable with E.</summary>
[DefaultExecutionOrder(40)]
public sealed class InteractPromptUI : MonoBehaviour
{
    Text _label;
    GameObject _root;
    CanvasGroup _group;
    Transform _player;
    PlayerInventory _inventory;
    PlayerStorageInteractor _interactor;
    float _shown;

    void Awake()
    {
        Build();
    }

    void LateUpdate()
    {
        if (_label == null || _group == null)
            return;

        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null)
            {
                SetVisible(false, null);
                return;
            }

            _player = go.transform;
            _inventory = go.GetComponent<PlayerInventory>();
            _interactor = go.GetComponent<PlayerStorageInteractor>();
        }

        if (_inventory != null && _inventory.BlocksLook)
        {
            SetVisible(false, null);
            return;
        }

        string text = null;
        if (_interactor != null && _interactor.LookTarget != null && _interactor.LookTarget.CanInteract())
            text = $"[E]  {_interactor.LookTarget.InteractLabel}";
        if (!string.IsNullOrEmpty(RepairKitTool.AimPrompt))
            text = RepairKitTool.AimPrompt;
        if (!string.IsNullOrEmpty(CastleBuildController.DemolishPrompt))
            text = CastleBuildController.DemolishPrompt;

        SetVisible(!string.IsNullOrEmpty(text), text);
    }

    void SetVisible(bool on, string text)
    {
        float target = on ? 1f : 0f;
        _shown = Mathf.MoveTowards(_shown, target, Time.deltaTime * 8f);
        _group.alpha = on
            ? _shown * (0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 3.2f))
            : _shown;
        _root.SetActive(_shown > 0.02f);
        if (on && text != null)
            _label.text = text;
    }

    void Build()
    {
        var canvasGo = new GameObject("InteractPromptCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 55;
        UiScale.Configure(canvasGo.AddComponent<CanvasScaler>());

        _root = new GameObject("Prompt", typeof(RectTransform));
        _root.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)_root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 58f);
        rt.sizeDelta = new Vector2(420f, 36f);
        _group = _root.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(_root.transform, false);
        var tr = (RectTransform)textGo.transform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        _label = textGo.AddComponent<Text>();
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                      ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        _label.fontSize = 20;
        _label.fontStyle = FontStyle.Bold;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = new Color(0.98f, 0.93f, 0.72f, 1f);
        _label.raycastTarget = false;
        _root.SetActive(false);
    }
}
