using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared runtime uGUI builders in the parchment style. New HUD pieces use this instead of
/// copying canvas/panel/text setup; older screens can migrate to it over time.
/// </summary>
public static class UiFactory
{
    static Font _font;

    public static Font Font
    {
        get
        {
            if (_font == null)
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }

    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        UiScale.Configure(go.AddComponent<CanvasScaler>());
        return canvas;
    }

    public static RectTransform CreateRect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rt = CreateRect(parent, name, anchor, pivot, position, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        Sprite parchment = InventoryUiTheme.PanelSprite;
        if (parchment != null)
        {
            image.sprite = parchment;
            image.type = Image.Type.Sliced;
            image.color = InventoryUiTheme.PanelTint;
        }
        else
        {
            image.color = InventoryUiTheme.PanelTint;
        }

        return image;
    }

    public static Image CreateFill(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Text CreateText(Transform parent, string name, int fontSize, Color color, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);
        var text = go.AddComponent<Text>();
        text.font = Font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>Horizontal bar: background + left-anchored fill. Drive it via SetBar.</summary>
    public static Image CreateBar(Transform parent, string name, Vector2 position, Vector2 size, Color fill)
    {
        RectTransform back = CreateRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), position, size);
        var bg = back.gameObject.AddComponent<Image>();
        bg.color = InventoryUiTheme.BarTrack;
        bg.raycastTarget = false;

        Image bar = CreateFill(back, "Fill", fill);
        var rt = bar.rectTransform;
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        return bar;
    }

    public static void SetBar(Image bar, float fraction)
    {
        var rt = bar.rectTransform;
        rt.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
