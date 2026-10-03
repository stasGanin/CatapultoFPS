using UnityEngine;

/// <summary>
/// Instantiates a dedicated weapon FBX as a first-person viewmodel.
/// Separate files only — do not slice the combined Maya scene at runtime.
/// </summary>
public static class WeaponViewModelFactory
{
    public const string CannonResource = "Weapons/HandCannon";
    public const string CrossbowResource = "Weapons/Crossbow";
    public const string StaffResource = "Weapons/Staff";

    public static Transform AttachCannon(Transform cameraPivot)
    {
        return AttachFromResource(
            cameraPivot,
            "HandCannonArt",
            CannonResource,
            new Vector3(0.32f, -0.26f, 0.52f),
            Quaternion.Euler(4f, 10f, 0f),
            0.55f);
    }

    public static Transform AttachBallista(Transform cameraPivot)
    {
        return AttachFromResource(
            cameraPivot,
            "CrossbowArt",
            CrossbowResource,
            new Vector3(0.30f, -0.24f, 0.46f),
            Quaternion.Euler(2f, 8f, 0f),
            0.50f);
    }

    public static Transform AttachStaff(Transform cameraPivot)
    {
        return AttachFromResource(
            cameraPivot,
            "StaffView",
            StaffResource,
            new Vector3(0.28f, -0.34f, 0.48f),
            Quaternion.Euler(18f, 22f, -12f),
            0.92f);
    }

    public static Transform AttachScattergun(Transform cameraPivot)
    {
        Transform fromArt = AttachFromResource(
            cameraPivot,
            "ScattergunView",
            "Weapons/Scattergun",
            new Vector3(0.30f, -0.22f, 0.48f),
            Quaternion.Euler(6f, 8f, 0f),
            0.52f);
        return fromArt != null ? fromArt : AttachPrimitiveScattergun(cameraPivot);
    }

    public static Transform AttachEmberLauncher(Transform cameraPivot)
    {
        Transform fromArt = AttachFromResource(
            cameraPivot,
            "EmberLauncherView",
            "Weapons/EmberLauncher",
            new Vector3(0.28f, -0.24f, 0.50f),
            Quaternion.Euler(8f, 12f, -6f),
            0.48f);
        return fromArt != null ? fromArt : AttachPrimitiveEmberLauncher(cameraPivot);
    }

    static Transform AttachPrimitiveScattergun(Transform parent)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find("ScattergunView");
        if (existing != null)
            return existing;

        var root = new GameObject("ScattergunView");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0.30f, -0.22f, 0.48f);
        root.transform.localRotation = Quaternion.Euler(6f, 8f, 0f);

        AddPrim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.12f), new Vector3(0.08f, 0.09f, 0.42f),
            new Color(0.28f, 0.22f, 0.16f));
        AddPrim(root.transform, PrimitiveType.Cube, new Vector3(0.04f, -0.02f, -0.08f), new Vector3(0.05f, 0.12f, 0.08f),
            new Color(0.22f, 0.16f, 0.12f));
        AddPrim(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.28f), new Vector3(0.16f, 0.06f, 0.10f),
            new Color(0.18f, 0.16f, 0.14f));
        return root.transform;
    }

    static Transform AttachPrimitiveEmberLauncher(Transform parent)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find("EmberLauncherView");
        if (existing != null)
            return existing;

        var root = new GameObject("EmberLauncherView");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = new Vector3(0.28f, -0.24f, 0.50f);
        root.transform.localRotation = Quaternion.Euler(8f, 12f, -6f);

        AddPrim(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.16f), new Vector3(0.07f, 0.22f, 0.07f),
            new Color(0.32f, 0.22f, 0.16f));
        Transform tube = root.transform.GetChild(root.transform.childCount - 1);
        tube.localRotation = Quaternion.Euler(90f, 0f, 0f);
        AddPrim(root.transform, PrimitiveType.Sphere, new Vector3(0f, -0.04f, 0.02f), Vector3.one * 0.14f,
            new Color(0.85f, 0.32f, 0.10f));
        return root.transform;
    }

    static void AddPrim(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = type.ToString();
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        Object.Destroy(go.GetComponent<Collider>());
        var rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        EnsureVisibleMaterials(go);
        if (rend != null)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            rend.SetPropertyBlock(block);
        }
    }

    public static Transform AttachFromResource(
        Transform parent,
        string viewName,
        string resourcePath,
        Vector3 localPosition,
        Quaternion localRotation,
        float targetLength)
    {
        if (parent == null)
            return null;

        Transform existing = parent.Find(viewName);
        if (existing != null)
            return existing;

        var prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
            return null;

        var root = new GameObject(viewName);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = localRotation;
        root.transform.localScale = Vector3.one;

        GameObject instance = Object.Instantiate(prefab, root.transform, false);
        instance.name = "Mesh";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        StripGameplayJunk(root);
        EnsureVisibleMaterials(root);

        if (!HasRenderer(root))
        {
            Debug.LogWarning($"WeaponViewModelFactory: {resourcePath} has no mesh renderer.");
            DestroyGo(root);
            return null;
        }

        RecenterAndFit(root, instance.transform, targetLength);
        CreateMuzzle(root, parent);
        return root.transform;
    }

    static bool HasRenderer(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        return renderers != null && renderers.Length > 0;
    }

    static void StripGameplayJunk(GameObject root)
    {
        var colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            DestroyGo(colliders[i]);

        var animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
            animators[i].enabled = false;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }
    }

    static void EnsureVisibleMaterials(GameObject root)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return;

        var fallback = new Material(shader);
        fallback.color = new Color(0.42f, 0.34f, 0.26f);
        fallback.SetColor("_BaseColor", fallback.color);

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].sharedMaterial;
            if (mat == null || mat.shader == null || mat.shader.name.Contains("InternalError"))
                renderers[i].sharedMaterial = fallback;
        }
    }

    static void RecenterAndFit(GameObject root, Transform model, float targetLength)
    {
        Bounds bounds = CombinedBounds(root);
        model.position += root.transform.position - bounds.center;

        bounds = CombinedBounds(root);
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (longest < 1e-4f || targetLength <= 0.01f)
            return;

        float scale = Mathf.Clamp(targetLength / longest, 0.0001f, 1000f);
        root.transform.localScale = Vector3.one * scale;
    }

    static Bounds CombinedBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    static void CreateMuzzle(GameObject root, Transform aim)
    {
        if (root.transform.Find("Muzzle") != null)
            return;

        Vector3 forward = aim != null ? aim.forward : root.transform.forward;
        Vector3 tip = root.transform.position + forward * 0.25f;
        Bounds bounds = CombinedBounds(root);
        float best = float.NegativeInfinity;
        Vector3[] corners =
        {
            new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
            new Vector3(bounds.max.x, bounds.max.y, bounds.max.z),
        };
        for (int i = 0; i < corners.Length; i++)
        {
            float d = Vector3.Dot(corners[i] - bounds.center, forward);
            if (d > best)
            {
                best = d;
                tip = corners[i];
            }
        }

        var muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(root.transform, true);
        muzzle.transform.position = tip;
        muzzle.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    static void DestroyGo(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Object.Destroy(obj);
        else
            Object.DestroyImmediate(obj);
    }
}
