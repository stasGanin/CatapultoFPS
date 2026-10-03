using UnityEngine;

/// <summary>
/// Заглушка башни-пушки из примитивов, пока нет арта. Пивот — центр основания на крыше;
/// вращающаяся часть, ствол, камера и люк возвращаются отдельными узлами, чтобы код не зависел от модели.
/// </summary>
public static class CastleTowerVisual
{
    public const float BodyRadius = 1.5f;
    public const float BodyHeight = 2.4f;
    // Камера игрока в башне: над корпусом, чтобы ствол не перекрывал обзор.
    const float SeatHeight = BodyHeight + 1.7f;
    const float BarrelLength = 2.4f;
    const float HatchSize = 1.1f;

    static readonly Color Stone = new Color(0.42f, 0.4f, 0.38f);
    static readonly Color Iron = new Color(0.16f, 0.16f, 0.19f);
    static readonly Color HatchColor = new Color(0.22f, 0.16f, 0.1f);
    static Material _shared;

    public struct Parts
    {
        public GameObject Root;
        public Transform Yaw;
        public Transform Pitch;
        public Transform Muzzle;
        public Transform Seat;
        public GameObject Hatch;
    }

    public static Parts Build(bool ghost)
    {
        var parts = new Parts { Root = new GameObject(ghost ? "Ghost_CannonTower" : "CannonTower") };
        Transform root = parts.Root.transform;

        Part(root, PrimitiveType.Cylinder, new Vector3(0f, BodyHeight * 0.5f, 0f),
            new Vector3(BodyRadius * 2f, BodyHeight * 0.5f, BodyRadius * 2f), Stone, ghost);

        var yaw = new GameObject("Yaw").transform;
        yaw.SetParent(root, false);
        yaw.localPosition = new Vector3(0f, BodyHeight + 0.5f, 0f);
        Part(yaw, PrimitiveType.Cube, Vector3.zero, new Vector3(1.4f, 0.9f, 1.4f), Stone, ghost);

        var pitch = new GameObject("Pitch").transform;
        pitch.SetParent(yaw, false);
        // Cylinder стоит вдоль Y — кладём его вдоль Z, чтобы ствол смотрел вперёд по оси Pitch.
        Part(pitch, PrimitiveType.Cylinder, new Vector3(0f, 0f, BarrelLength * 0.5f),
            new Vector3(0.7f, BarrelLength * 0.5f, 0.7f), Iron, ghost, Quaternion.Euler(90f, 0f, 0f));

        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(pitch, false);
        muzzle.localPosition = new Vector3(0f, 0f, BarrelLength);

        var seat = new GameObject("Seat").transform;
        seat.SetParent(root, false);
        seat.localPosition = new Vector3(0f, SeatHeight, 0f);

        parts.Yaw = yaw;
        parts.Pitch = pitch;
        parts.Muzzle = muzzle;
        parts.Seat = seat;

        if (!ghost)
        {
            var body = parts.Root.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, BodyHeight * 0.5f, 0f);
            body.radius = BodyRadius;
            body.height = BodyHeight;
            parts.Hatch = BuildHatch(root);
        }

        return parts;
    }

    /// <summary>Люк на потолке комнаты прямо под башней; триггер, чтобы игрок не упирался в него головой.</summary>
    static GameObject BuildHatch(Transform tower)
    {
        var hatch = new GameObject("TowerHatch");
        hatch.transform.SetParent(tower, false);
        // Низ крыши лежит на RoofThickness ниже основания башни.
        hatch.transform.localPosition = new Vector3(0f, -CarcassMetrics.RoofThickness, 0f);
        Part(hatch.transform, PrimitiveType.Cube, new Vector3(0f, -0.03f, 0f), new Vector3(HatchSize, 0.06f, HatchSize),
            HatchColor, false);
        var trigger = hatch.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, -0.2f, 0f);
        trigger.size = new Vector3(HatchSize, 0.5f, HatchSize);
        return hatch;
    }

    static void Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color, bool ghost,
        Quaternion? localRotation = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = "Part";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRotation ?? Quaternion.identity;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<MeshRenderer>();
        if (_shared == null)
            _shared = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        renderer.sharedMaterial = _shared;
        // У призрака цвет задаёт материал превью (зелёный/красный) — блок цвета его бы перебил.
        if (ghost)
            return;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
