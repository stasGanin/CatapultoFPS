using UnityEngine;

/// <summary>
/// В билде — VSync. В Editor не капаем (VSync включай в Game view dropdown).
/// </summary>
public class PlayModeFrameSettings : MonoBehaviour
{
    void Awake()
    {
#if UNITY_EDITOR
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
#else
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = -1;
#endif
    }
}
