using UnityEngine;

/// <summary>
/// Impact chips and build/door dust. Shared materials — no per-burst allocations.
/// </summary>
public static class HitSparkVfx
{
    static Material _chipLit;
    static Material _dustUnlit;
    static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

    public static void Play(Vector3 position, Vector3 normal)
    {
        EnsureMats();
        Vector3 n = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        for (int i = 0; i < 8; i++)
        {
            var chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.name = "HitChip";
            Object.Destroy(chip.GetComponent<Collider>());
            chip.transform.position = position + n * 0.05f;
            chip.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);
            chip.transform.rotation = Random.rotation;

            var renderer = chip.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _chipLit;
            Color c = new Color(0.55f, 0.5f, 0.42f);
            Block.SetColor("_BaseColor", c);
            Block.SetColor("_Color", c);
            renderer.SetPropertyBlock(Block);

            var body = chip.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.linearDamping = 0.35f;
            Vector3 dir = (n + Random.insideUnitSphere * 0.85f).normalized;
            body.linearVelocity = dir * Random.Range(2.5f, 5.5f);
            Object.Destroy(chip, 0.45f);
        }
    }

    /// <summary>Soft stone dust for placing walls or swinging a door.</summary>
    public static void PlayDust(Vector3 position, Vector3 normal, int count = 12)
    {
        EnsureMats();
        Vector3 n = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
        int nChips = Mathf.Clamp(count, 4, 18);
        for (int i = 0; i < nChips; i++)
        {
            var chip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chip.name = "DustChip";
            Object.Destroy(chip.GetComponent<Collider>());
            chip.transform.position = position + n * 0.04f + Random.insideUnitSphere * 0.12f;
            float s = Random.Range(0.04f, 0.11f);
            chip.transform.localScale = Vector3.one * s;
            chip.transform.rotation = Random.rotation;

            var renderer = chip.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _dustUnlit;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            float a = Random.Range(0.35f, 0.7f);
            Color c = new Color(0.62f, 0.56f, 0.46f, a);
            Block.SetColor("_BaseColor", c);
            Block.SetColor("_Color", c);
            renderer.SetPropertyBlock(Block);

            var body = chip.AddComponent<Rigidbody>();
            body.mass = 0.02f;
            body.useGravity = true;
            body.linearDamping = 1.4f;
            Vector3 dir = (n * 0.55f + Vector3.up * 0.45f + Random.insideUnitSphere * 0.7f).normalized;
            body.linearVelocity = dir * Random.Range(0.8f, 2.4f);
            Object.Destroy(chip, 0.7f);
        }
    }

    static void EnsureMats()
    {
        if (_chipLit == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _chipLit = shader != null ? new Material(shader) : null;
        }

        if (_dustUnlit == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _dustUnlit = shader != null ? new Material(shader) : null;
            if (_dustUnlit != null)
            {
                _dustUnlit.SetFloat("_Surface", 1f);
                _dustUnlit.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _dustUnlit.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _dustUnlit.SetInt("_ZWrite", 0);
                _dustUnlit.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _dustUnlit.renderQueue = 3000;
            }
        }
    }
}
