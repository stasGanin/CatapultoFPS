using UnityEngine;
using UnityEngine.UI;

/// <summary>Держит референс-разрешение канваса в соответствии с настройкой масштаба интерфейса.</summary>
[RequireComponent(typeof(CanvasScaler))]
public sealed class UiScaleFollower : MonoBehaviour
{
    CanvasScaler _scaler;

    void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
    }

    void OnEnable()
    {
        GameSettings.EnsureLoaded();
        GameSettings.UiScaleChanged += Apply;
        Apply();
    }

    void OnDisable()
    {
        GameSettings.UiScaleChanged -= Apply;
    }

    // Больший масштаб = меньшее референс-разрешение: элементы занимают больше места на экране.
    void Apply()
    {
        _scaler.referenceResolution = UiScale.ReferenceResolution / GameSettings.UiScale;
    }
}
