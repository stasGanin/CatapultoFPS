using UnityEngine;

/// <summary>Loads the shared sorcerer mesh used as castle heart / mage core.</summary>
public static class CastleMageVisual
{
    public const string ResourcePath = "Castle/Mage/Sorcerer";
    public const string AssetPath = "Assets/Resources/Castle/Mage/Sorcerer.fbx";

    /// <summary>
    /// Instantiates the sorcerer under parent.
    /// Scale the returned root (or SorcererVisual child) in the prefab to tune size.
    /// </summary>
    public static GameObject Spawn(Transform parent, Vector3 localPosition, float heightScale = 3.2f, bool attachMageComponent = false)
    {
        GameObject root = new GameObject(attachMageComponent ? "EnemyMage" : "MageHeart");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = localPosition;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject visual = null;
        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab != null)
        {
            visual = Object.Instantiate(prefab, root.transform);
            visual.name = "SorcererVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            FitHeight(visual, heightScale);
        }
        else
        {
            Debug.LogWarning($"CastleMageVisual: missing Resources/{ResourcePath}.fbx — using fallback capsule.");
            visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "MageFallback";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, heightScale * 0.5f, 0f);
            visual.transform.localScale = new Vector3(0.7f, heightScale * 0.5f, 0.7f);
            if (Application.isPlaying)
                Object.Destroy(visual.GetComponent<Collider>());
            else
                Object.DestroyImmediate(visual.GetComponent<Collider>());
            ApplyColor(visual, new Color(0.55f, 0.2f, 0.75f));
        }

        AttachHeartLight(root.transform, heightScale, attachMageComponent);

        if (attachMageComponent)
        {
            var hit = root.AddComponent<CapsuleCollider>();
            hit.height = heightScale;
            hit.radius = Mathf.Max(0.45f, heightScale * 0.22f);
            hit.center = new Vector3(0f, heightScale * 0.5f, 0f);

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            root.AddComponent<EnemyCastleMage>();
        }

        return root;
    }

    static void AttachHeartLight(Transform parent, float heightScale, bool enemy)
    {
        var go = new GameObject("HeartLight");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, heightScale * 0.55f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 12f;
        light.intensity = enemy ? 2.1f : 3.6f;
        light.color = enemy
            ? new Color(0.95f, 0.28f, 0.32f)
            : new Color(0.62f, 0.42f, 1f);
        light.shadows = LightShadows.None;
    }

    /// <summary>Uniform-scale visual so its local AABB height matches targetHeight (meters).</summary>
    public static void FitHeight(GameObject visual, float targetHeight)
    {
        if (visual == null || targetHeight <= 0.01f)
            return;

        float current = MeasureLocalHeight(visual);
        if (current < 1e-5f)
        {
            // FBX often imports as cm — try a blunt boost so it is editable in the prefab
            visual.transform.localScale = Vector3.one * targetHeight;
            return;
        }

        float scale = targetHeight / current;
        // Clamp absurd ratios from broken bounds
        scale = Mathf.Clamp(scale, 0.001f, 10000f);
        visual.transform.localScale = Vector3.one * scale;

        // Sit feet on local Y=0 of parent
        float bottom = MeasureLocalBottom(visual);
        visual.transform.localPosition -= new Vector3(0f, bottom, 0f);
    }

    static float MeasureLocalHeight(GameObject visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return 0f;

        bool any = false;
        Bounds local = new Bounds(Vector3.zero, Vector3.zero);
        Transform root = visual.transform;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null)
                continue;

            // localBounds is in renderer local space — convert corners to visual root
            Bounds lb = r.localBounds;
            Vector3[] corners =
            {
                new Vector3(lb.min.x, lb.min.y, lb.min.z),
                new Vector3(lb.min.x, lb.min.y, lb.max.z),
                new Vector3(lb.min.x, lb.max.y, lb.min.z),
                new Vector3(lb.min.x, lb.max.y, lb.max.z),
                new Vector3(lb.max.x, lb.min.y, lb.min.z),
                new Vector3(lb.max.x, lb.min.y, lb.max.z),
                new Vector3(lb.max.x, lb.max.y, lb.min.z),
                new Vector3(lb.max.x, lb.max.y, lb.max.z),
            };

            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 world = r.transform.TransformPoint(corners[c]);
                Vector3 inRoot = root.InverseTransformPoint(world);
                if (!any)
                {
                    local = new Bounds(inRoot, Vector3.zero);
                    any = true;
                }
                else
                    local.Encapsulate(inRoot);
            }
        }

        return any ? local.size.y : 0f;
    }

    static float MeasureLocalBottom(GameObject visual)
    {
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return 0f;

        float minY = float.MaxValue;
        Transform root = visual.transform;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            Bounds lb = r.localBounds;
            Vector3[] corners =
            {
                new Vector3(lb.min.x, lb.min.y, lb.min.z),
                new Vector3(lb.min.x, lb.min.y, lb.max.z),
                new Vector3(lb.max.x, lb.min.y, lb.min.z),
                new Vector3(lb.max.x, lb.min.y, lb.max.z),
                new Vector3(lb.min.x, lb.max.y, lb.min.z),
                new Vector3(lb.min.x, lb.max.y, lb.max.z),
                new Vector3(lb.max.x, lb.max.y, lb.min.z),
                new Vector3(lb.max.x, lb.max.y, lb.max.z),
            };
            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 inRoot = root.InverseTransformPoint(r.transform.TransformPoint(corners[c]));
                if (inRoot.y < minY)
                    minY = inRoot.y;
            }
        }

        return minY == float.MaxValue ? 0f : minY;
    }

    static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return;
        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        renderer.sharedMaterial = mat;
    }
}
