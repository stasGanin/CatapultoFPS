using UnityEngine;
using UnityEngine.UI;

/// <summary>Single place for runtime canvas scaling so every screen scales the same way.</summary>
public static class UiScale
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    // 0.5 — компромисс: на 21:9 и 32:9 окна не раздуваются по высоте и не вылезают за экран,
    // а на 4:3 не сжимаются до нечитаемости.
    public const float MatchWidthOrHeight = 0.5f;

    /// <summary>Канвас игрового интерфейса: размер зависит от пользовательской настройки масштаба.</summary>
    public static void Configure(CanvasScaler scaler)
    {
        ConfigureFixed(scaler);
        scaler.gameObject.AddComponent<UiScaleFollower>();
    }

    /// <summary>Канвас без пользовательского масштаба (меню настроек: иначе слайдер «уезжает» из-под курсора).</summary>
    public static void ConfigureFixed(CanvasScaler scaler)
    {
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = MatchWidthOrHeight;
    }
}
