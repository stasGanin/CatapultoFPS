using UnityEngine;

/// <summary>Shared unlit cubes/labels for the section prototype.</summary>
public static class CastlePrim
{
    static Shader _shader;
    static Font _font;

    public static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color, bool trigger = false)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        Tint(go, color);

        var col = go.GetComponent<BoxCollider>();
        if (col != null)
            col.isTrigger = trigger;
        return go;
    }

    public static void Tint(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        if (_shader == null)
            _shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Unlit/Color");
        if (_shader != null)
            renderer.sharedMaterial = new Material(_shader);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    public static void Label(Transform parent, Vector3 localPos, string text, Color color)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos + Vector3.up * 0.12f;
        go.transform.localRotation = Quaternion.identity;
        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.fontSize = 32;
        mesh.characterSize = 0.06f;
        mesh.anchor = TextAnchor.LowerCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = color;
        if (_font == null)
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (_font != null)
            mesh.font = _font;
    }
}
