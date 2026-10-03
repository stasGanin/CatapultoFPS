using UnityEngine;

/// <summary>Disables freeform building; enables castle module placement (B = door).</summary>
[DefaultExecutionOrder(-100)]
public sealed class BuildingBootstrap : MonoBehaviour
{
    void Awake()
    {
        // Legacy freeform build OFF
        var legacy = GetComponent<BuildingController>();
        if (legacy != null)
            legacy.enabled = false;

        var legacyMenu = GetComponent<BuildingMenuUI>();
        if (legacyMenu != null)
            legacyMenu.enabled = false;

        var sceneMenu = FindFirstObjectByType<BuildingMenuUI>();
        if (sceneMenu != null)
            sceneMenu.enabled = false;

        if (GetComponent<CastleBuildController>() == null)
            gameObject.AddComponent<CastleBuildController>();
        if (GetComponent<CastleModuleMenuUI>() == null)
            gameObject.AddComponent<CastleModuleMenuUI>();
    }
}
