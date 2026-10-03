using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Дроп камня: обводка по нормалям + мягкое свечение-billboard лицом к камере.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class WorldLootPickup : MonoBehaviour
{
    const string StoneResourcePath = "Loot/StoneLoot";
    const string StoneNormalResourcePath = "Loot/StoneLoot_Normal";
    const float VisualScale = 0.22f;

    static Mesh _cachedStoneMesh;
    static Texture2D _cachedGlowTex;
    static Texture2D _cachedNormalTex;

    [SerializeField] ItemDefinition _item;
    [SerializeField] int _count = 1;
    [SerializeField] Color _outlineColor = new Color(1f, 0.96f, 0.82f, 0.9f);
    [SerializeField] Color _glowColor = new Color(1f, 0.94f, 0.75f, 0.55f);
    [SerializeField] float _outlineWidth = 0.06f;
    [SerializeField] float _glowSize = 0.9f;
    [SerializeField] float _glowBehind = 0.04f;

    Rigidbody _body;
    bool _collecting;
    Transform _glowBillboard;

    public ItemDefinition Item => _item;
    public int Count => _count;

    public void Init(ItemDefinition item, int count, float pickupRadius = 2f)
    {
        _item = item;
        _count = Mathf.Max(1, count);
        if (IsChronum)
        {
            transform.localScale = Vector3.one * 0.3f;
            _outlineColor = new Color(0.45f, 0.95f, 1f, 0.95f);
            _glowColor = new Color(0.3f, 0.9f, 1f, 0.8f);
        }

        ApplyVisual();
        TintHighlight();
    }

    void TintHighlight()
    {
        var outline = transform.Find("Outline");
        if (outline != null)
        {
            var r = outline.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterial != null)
                r.sharedMaterial.SetColor("_Color", _outlineColor);
        }

        if (_glowBillboard != null)
        {
            var r = _glowBillboard.GetComponent<MeshRenderer>();
            if (r != null && r.sharedMaterial != null)
                r.sharedMaterial.SetColor("_Color", _glowColor);
        }
    }

    void Awake()
    {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = false;
        col.radius = 0.5f;

        _body = GetComponent<Rigidbody>();
        if (_body == null)
            _body = gameObject.AddComponent<Rigidbody>();

        _body.constraints = RigidbodyConstraints.FreezeRotation;
        _body.mass = 0.4f;
        _body.linearDamping = 4f;
        _body.angularDamping = 10f;
        _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _body.interpolation = RigidbodyInterpolation.Interpolate;

        ApplyVisual();
        EnsureHighlight();
    }

    void LateUpdate()
    {
        if (_glowBillboard == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        // Свечение «за» камнем, всегда лицом к камере
        _glowBillboard.position = transform.position - cam.transform.forward * _glowBehind;
        _glowBillboard.rotation = cam.transform.rotation;
        _glowBillboard.localScale = Vector3.one * _glowSize;
    }

    public void TryCollect(PlayerInventory inventory)
    {
        if (_collecting || inventory == null || _item == null || _count <= 0)
            return;

        int added = inventory.TryAddItem(_item, _count);
        if (added <= 0)
            return;

        _count -= added;
        if (_count <= 0)
        {
            _collecting = true;
            Destroy(gameObject);
        }
    }

    void ApplyVisual()
    {
        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);

        Color color = _item != null ? _item.IconColor : new Color(0.45f, 0.42f, 0.38f);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);

        Texture2D normal = LoadStoneNormal();
        if (normal != null)
        {
            // URP Lit: bump map + keyword
            renderer.sharedMaterial.EnableKeyword("_NORMALMAP");
            renderer.sharedMaterial.SetTexture("_BumpMap", normal);
            renderer.sharedMaterial.SetFloat("_BumpScale", 1.15f);
            block.SetTexture("_BumpMap", normal);
            block.SetFloat("_BumpScale", 1.15f);
        }

        renderer.SetPropertyBlock(block);

        if (IsChronum && renderer.sharedMaterial != null)
        {
            renderer.sharedMaterial.EnableKeyword("_EMISSION");
            renderer.sharedMaterial.SetColor("_EmissionColor", new Color(0.15f, 0.55f, 0.7f) * 2.4f);
        }
    }

    bool IsChronum => _item != null && _item.Id == "chronum";

    void EnsureHighlight()
    {
        if (transform.Find("Outline") != null)
            return;

        Mesh sourceMesh = GetComponent<MeshFilter>() != null ? GetComponent<MeshFilter>().sharedMesh : null;
        if (sourceMesh == null)
            return;

        // Обводка: тот же меш, Cull Front + лёгкий push по нормалям в шейдере
        var outlineGo = new GameObject("Outline");
        outlineGo.transform.SetParent(transform, false);
        outlineGo.transform.localPosition = Vector3.zero;
        outlineGo.transform.localRotation = Quaternion.identity;
        outlineGo.transform.localScale = Vector3.one;

        var outlineFilter = outlineGo.AddComponent<MeshFilter>();
        outlineFilter.sharedMesh = sourceMesh;
        var outlineRenderer = outlineGo.AddComponent<MeshRenderer>();
        outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;

        Shader outlineShader = Shader.Find("Catapulto/LootOutline");
        if (outlineShader != null)
        {
            var mat = new Material(outlineShader);
            mat.SetColor("_Color", _outlineColor);
            mat.SetFloat("_OutlineWidth", _outlineWidth);
            outlineRenderer.sharedMaterial = mat;
        }

        // Мягкое свечение-billboard (мир, не child scale камня — проще лицом к камере)
        var glowGo = new GameObject("GlowBillboard");
        glowGo.transform.SetParent(null, true);
        glowGo.transform.position = transform.position;
        _glowBillboard = glowGo.transform;

        var glowFilter = glowGo.AddComponent<MeshFilter>();
        glowFilter.sharedMesh = BuildQuadMesh();
        var glowRenderer = glowGo.AddComponent<MeshRenderer>();
        glowRenderer.shadowCastingMode = ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false;

        Shader glowShader = Shader.Find("Catapulto/LootGlowBillboard");
        if (glowShader != null)
        {
            var mat = new Material(glowShader);
            mat.SetColor("_Color", _glowColor);
            mat.SetTexture("_MainTex", GetOrCreateGlowTexture());
            glowRenderer.sharedMaterial = mat;
        }

        // Уничтожаем glow вместе с лутом
        var link = glowGo.AddComponent<LootGlowFollower>();
        link.Bind(transform);
    }

    void OnDestroy()
    {
        if (_glowBillboard != null)
            Destroy(_glowBillboard.gameObject);
    }

    public static WorldLootPickup Spawn(ItemDefinition item, int count, Vector3 position, float pickupRadius = 2f)
    {
        var go = new GameObject(item != null ? $"Loot_{item.Id}" : "Loot");
        go.transform.position = position + Vector3.up * 0.12f;
        go.transform.localScale = Vector3.one * VisualScale;
        go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var filter = go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        Mesh mesh = LoadStoneMesh();
        if (mesh != null)
        {
            filter.sharedMesh = mesh;
        }
        else
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            filter.sharedMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(temp);
        }

        go.AddComponent<SphereCollider>();
        var pickup = go.AddComponent<WorldLootPickup>();
        pickup.Init(item, count, pickupRadius);
        return pickup;
    }

    static Mesh LoadStoneMesh()
    {
        if (_cachedStoneMesh != null)
            return _cachedStoneMesh;

        GameObject model = Resources.Load<GameObject>(StoneResourcePath);
        if (model == null)
            return null;

        MeshFilter mf = model.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
            return null;

        _cachedStoneMesh = mf.sharedMesh;
        return _cachedStoneMesh;
    }

    static Texture2D LoadStoneNormal()
    {
        if (_cachedNormalTex != null)
            return _cachedNormalTex;

        _cachedNormalTex = Resources.Load<Texture2D>(StoneNormalResourcePath);
        return _cachedNormalTex;
    }

    static Mesh BuildQuadMesh()
    {
        var mesh = new Mesh { name = "LootGlowQuad" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
        };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    static Texture2D GetOrCreateGlowTexture()
    {
        if (_cachedGlowTex != null)
            return _cachedGlowTex;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - half) / half;
            float dy = (y - half) / half;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - r);
            a = a * a * (3f - 2f * a); // smoothstep
            a = Mathf.Pow(a, 1.6f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }

        tex.Apply(false, true);
        _cachedGlowTex = tex;
        return _cachedGlowTex;
    }
}

/// <summary>
/// Если родитель-лут уничтожен — убираем billboard glow.
/// </summary>
public class LootGlowFollower : MonoBehaviour
{
    Transform _target;

    public void Bind(Transform target) => _target = target;

    void LateUpdate()
    {
        if (_target == null)
        {
            Destroy(gameObject);
            return;
        }
    }
}
