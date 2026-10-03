using UnityEngine;
using UnityEngine.UI;

/// <summary>On-screen palette for the F9 castle layout editor.</summary>
public sealed class CastlePlayEditorHud : MonoBehaviour
{
    Text _label;
    Image _background;

    public void SetText(string text, bool on)
    {
        if (_label != null)
            _label.text = text;
        if (_background != null)
        {
            _background.color = on
                ? new Color(0.18f, 0.22f, 0.28f, 0.92f)
                : new Color(0.18f, 0.22f, 0.28f, 0.55f);
        }
    }

    public static CastlePlayEditorHud Create(Transform parent)
    {
        var hud = parent.GetComponent<CastlePlayEditorHud>();
        if (hud != null)
            return hud;
        hud = parent.gameObject.AddComponent<CastlePlayEditorHud>();
        hud.Build();
        return hud;
    }

    void Build()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        var canvasGo = new GameObject("CastleEditorCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        UiScale.Configure(canvasGo.AddComponent<CanvasScaler>());
        canvasGo.AddComponent<GraphicRaycaster>();

        var go = new GameObject("Panel", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -72f);
        rt.sizeDelta = new Vector2(420f, 220f);

        _background = go.AddComponent<Image>();
        _background.color = new Color(0.18f, 0.22f, 0.28f, 0.92f);
        Sprite spr = InventoryUiTheme.ButtonSprite;
        if (spr != null)
        {
            _background.sprite = spr;
            _background.type = Image.Type.Sliced;
        }

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = (RectTransform)textGo.transform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14f, 10f);
        textRt.offsetMax = new Vector2(-14f, -10f);
        _label = textGo.AddComponent<Text>();
        _label.font = font;
        _label.fontSize = 16;
        _label.alignment = TextAnchor.UpperLeft;
        _label.color = new Color(0.92f, 0.9f, 0.82f, 1f);
        _label.raycastTarget = false;
        _label.horizontalOverflow = HorizontalWrapMode.Wrap;
        _label.verticalOverflow = VerticalWrapMode.Overflow;
    }
}
