using UnityEngine;

/// <summary>PlayerPrefs-backed look, audio, and display settings. Does not mutate LookConfig assets.</summary>
public static class GameSettings
{
    const string SensKey = "catapulto.settings.sensitivity";
    const string InvertKey = "catapulto.settings.invert_y";
    const string FovKey = "catapulto.settings.fov";
    const string VolumeKey = "catapulto.settings.volume";
    const string FullscreenKey = "catapulto.settings.fullscreen";
    const string QualityKey = "catapulto.settings.quality";

    const float DefaultSensitivity = 0.15f;
    const float DefaultFov = 75f;
    const float DefaultVolume = 1f;

    static bool _loaded;

    public static float LookSensitivity { get; private set; } = DefaultSensitivity;
    public static bool InvertY { get; private set; }
    public static float FieldOfView { get; private set; } = DefaultFov;
    public static float MasterVolume { get; private set; } = DefaultVolume;
    public static bool Fullscreen { get; private set; } = true;
    public static int QualityLevel { get; private set; }

    public static void EnsureLoaded()
    {
        if (_loaded)
            return;
        _loaded = true;
        LookSensitivity = PlayerPrefs.GetFloat(SensKey, DefaultSensitivity);
        InvertY = PlayerPrefs.GetInt(InvertKey, 0) != 0;
        FieldOfView = PlayerPrefs.GetFloat(FovKey, DefaultFov);
        MasterVolume = PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
        Fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
        QualityLevel = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());
        Apply();
    }

    public static void SetLookSensitivity(float value)
    {
        LookSensitivity = Mathf.Clamp(value, 0.04f, 0.45f);
        PlayerPrefs.SetFloat(SensKey, LookSensitivity);
        Save();
    }

    public static void SetInvertY(bool value)
    {
        InvertY = value;
        PlayerPrefs.SetInt(InvertKey, value ? 1 : 0);
        Save();
    }

    public static void SetFieldOfView(float value)
    {
        FieldOfView = Mathf.Clamp(value, 55f, 100f);
        PlayerPrefs.SetFloat(FovKey, FieldOfView);
        Save();
    }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        ApplyAudio();
        Save();
    }

    public static void SetFullscreen(bool value)
    {
        Fullscreen = value;
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        ApplyDisplay();
        Save();
    }

    public static void CycleQuality(int delta)
    {
        string[] names = QualitySettings.names;
        if (names == null || names.Length == 0)
            return;
        QualityLevel = (QualityLevel + delta + names.Length) % names.Length;
        PlayerPrefs.SetInt(QualityKey, QualityLevel);
        QualitySettings.SetQualityLevel(QualityLevel, true);
        Save();
    }

    public static string QualityName
    {
        get
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0)
                return "Default";
            int i = Mathf.Clamp(QualityLevel, 0, names.Length - 1);
            return names[i];
        }
    }

    public static void Apply()
    {
        ApplyAudio();
        ApplyDisplay();
        string[] names = QualitySettings.names;
        if (names != null && names.Length > 0)
        {
            QualityLevel = Mathf.Clamp(QualityLevel, 0, names.Length - 1);
            QualitySettings.SetQualityLevel(QualityLevel, true);
        }
    }

    static void ApplyAudio()
    {
        AudioListener.volume = MasterVolume;
    }

    static void ApplyDisplay()
    {
        if (Screen.fullScreen != Fullscreen)
            Screen.fullScreen = Fullscreen;
    }

    static void Save() => PlayerPrefs.Save();
}
