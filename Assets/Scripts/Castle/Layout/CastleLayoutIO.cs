using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>JSON load/save for castle layouts. Play Mode writes into Resources when in Editor.</summary>
public static class CastleLayoutIO
{
    public const string PlayerId = "player_t1";
    public const string EnemyId = "enemy_t1";

    public static CastleLayoutData LoadOrDefault(string id, bool playerOwned)
    {
        var loaded = Load(id);
        if (loaded != null && loaded.sections != null && loaded.sections.Count > 0)
            return loaded;
        return playerOwned ? CastleLayoutData.DefaultPlayer() : CastleLayoutData.DefaultEnemy();
    }

    public static CastleLayoutData Load(string id)
    {
        string persist = PersistPath(id);
        if (File.Exists(persist))
        {
            var fromPersist = TryParse(File.ReadAllText(persist), id + " persist");
            if (fromPersist != null)
                return fromPersist;
        }

#if UNITY_EDITOR
        string projectPath = ResourcePath(id);
        if (File.Exists(projectPath))
        {
            var fromProject = TryParse(File.ReadAllText(projectPath), id + " project");
            if (fromProject != null)
                return fromProject;
        }
#endif

        var text = Resources.Load<TextAsset>("Castle/Layouts/" + id);
        if (text != null && !string.IsNullOrEmpty(text.text))
            return TryParse(text.text, id + " resources");

        return null;
    }

    static CastleLayoutData TryParse(string json, string tag)
    {
        try
        {
            return JsonUtility.FromJson<CastleLayoutData>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"CastleLayoutIO: parse failed ({tag}): {ex.Message}");
            return null;
        }
    }

    public static bool Save(string id, CastleLayoutData data)
    {
        if (data == null)
            return false;

        string json = JsonUtility.ToJson(data, true);
        Directory.CreateDirectory(Path.GetDirectoryName(PersistPath(id)));
        File.WriteAllText(PersistPath(id), json);

#if UNITY_EDITOR
        string projectPath = ResourcePath(id);
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
        File.WriteAllText(projectPath, json);
        AssetDatabase.Refresh();
#endif
        Debug.Log($"CastleLayoutIO: saved '{id}'");
        return true;
    }

    static string PersistPath(string id)
    {
        return Path.Combine(Application.persistentDataPath, "CastleLayouts", id + ".json");
    }

    static string ResourcePath(string id)
    {
        return Path.Combine(Application.dataPath, "Resources", "Castle", "Layouts", id + ".json");
    }
}
