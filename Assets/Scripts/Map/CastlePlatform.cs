using UnityEngine;

/// <summary>
/// Truncated-cone mound authored in the scene. Transform is the flat top center — move it freely.
/// Mesh is rebuilt in editor; play mode does not snap or respawn the pad.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public sealed class CastlePlatform : MonoBehaviour
{
    const float MaxSlopeDegrees = 26f;
    const int Sides = 28;

    [SerializeField] float _height = 4.5f;
    [SerializeField] float _topRadius = 14f;

    Mesh _mesh;

    public Vector3 SpawnPoint => transform.position + Vector3.up * 0.06f;
    public float SurfaceRadius => Mathf.Max(4f, _topRadius);

    public static CastlePlatform Ensure(MapNodeMarker marker)
    {
        if (marker == null)
            return null;

        var go = marker.gameObject;
        go.transform.localScale = Vector3.one;

        var capsule = go.GetComponent<CapsuleCollider>();
        if (capsule != null)
            DestroyCompat(capsule);
        var sphere = go.GetComponent<SphereCollider>();
        if (sphere != null)
            DestroyCompat(sphere);

        var platform = go.GetComponent<CastlePlatform>();
        if (platform == null)
            platform = go.AddComponent<CastlePlatform>();

        platform.RebuildMesh();
        return platform;
    }

    [ContextMenu("Rebuild Mesh")]
    public void RebuildMesh()
    {
        float height = Mathf.Max(1.5f, _height);
        float topR = Mathf.Max(2f, _topRadius);
        float slope = MaxSlopeDegrees * Mathf.Deg2Rad;
        float bottomR = topR + height / Mathf.Tan(slope);
        BuildMesh(bottomR, topR, height);
        BindMesh();
        ApplyTint();
    }

#if UNITY_EDITOR
    [ContextMenu("Snap Top To Terrain")]
    public void SnapTopToTerrain()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null)
            return;
        Vector3 p = transform.position;
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y + 0.45f;
        transform.position = p;
        RebuildMesh();
    }
#endif

    void OnEnable()
    {
        if (_mesh == null)
            RebuildMesh();
    }

    void OnDestroy()
    {
        DestroyMesh();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;
        UnityEditor.EditorApplication.delayCall += RebuildMeshIfAlive;
    }

    void RebuildMeshIfAlive()
    {
        if (this != null)
            RebuildMesh();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(SpawnPoint, SurfaceRadius);
    }
#endif

    void BuildMesh(float bottomRadius, float topRadius, float height)
    {
        DestroyMesh();
        _mesh = new Mesh { name = "CastlePlatformFrustum" };
        _mesh.hideFlags = HideFlags.DontSave;

        int vCount = (Sides + 1) * 2 + 2;
        var verts = new Vector3[vCount];
        var uvs = new Vector2[vCount];

        for (int i = 0; i <= Sides; i++)
        {
            float t = i / (float)Sides;
            float ang = t * Mathf.PI * 2f;
            float c = Mathf.Cos(ang);
            float s = Mathf.Sin(ang);

            verts[i] = new Vector3(c * bottomRadius, -height, s * bottomRadius);
            verts[i + Sides + 1] = new Vector3(c * topRadius, 0f, s * topRadius);
            uvs[i] = new Vector2(t, 0f);
            uvs[i + Sides + 1] = new Vector2(t, 1f);
        }

        int topCenter = (Sides + 1) * 2;
        int botCenter = topCenter + 1;
        verts[topCenter] = Vector3.zero;
        verts[botCenter] = new Vector3(0f, -height, 0f);
        uvs[topCenter] = new Vector2(0.5f, 0.5f);
        uvs[botCenter] = new Vector2(0.5f, 0.5f);

        int triCount = Sides * 4;
        var tris = new int[triCount * 3];
        int tOff = 0;
        for (int i = 0; i < Sides; i++)
        {
            int b0 = i;
            int b1 = i + 1;
            int t0 = i + Sides + 1;
            int t1 = i + Sides + 2;

            tris[tOff++] = b0;
            tris[tOff++] = t0;
            tris[tOff++] = b1;
            tris[tOff++] = t0;
            tris[tOff++] = t1;
            tris[tOff++] = b1;

            tris[tOff++] = topCenter;
            tris[tOff++] = t1;
            tris[tOff++] = t0;

            tris[tOff++] = botCenter;
            tris[tOff++] = b0;
            tris[tOff++] = b1;
        }

        _mesh.vertices = verts;
        _mesh.uv = uvs;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    void BindMesh()
    {
        var filter = GetComponent<MeshFilter>();
        var col = GetComponent<MeshCollider>();
        if (filter != null)
            filter.sharedMesh = _mesh;
        if (col == null)
            return;
        col.sharedMesh = null;
        col.convex = true;
        col.sharedMesh = _mesh;
    }

    void ApplyTint()
    {
        var marker = GetComponent<MapNodeMarker>();
        Color c = marker != null && marker.Kind == MapNodeKind.PlayerCastle
            ? new Color(0.42f, 0.48f, 0.38f)
            : new Color(0.48f, 0.38f, 0.34f);

        var renderer = GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader != shader)
            renderer.sharedMaterial = new Material(shader);

        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", c);
        block.SetColor("_Color", c);
        renderer.SetPropertyBlock(block);
    }

    void DestroyMesh()
    {
        if (_mesh == null)
            return;
        if (Application.isPlaying)
            Destroy(_mesh);
        else
            DestroyImmediate(_mesh);
        _mesh = null;
    }

    static void DestroyCompat(Object obj)
    {
        if (obj == null)
            return;
        if (Application.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj);
    }
}
