using UnityEngine;

/// <summary>
/// Floor transfer inside a carcass castle: a ladder on the lower floor and a hatch on the upper one.
/// E moves the player to the other end — no climbing physics, just a quick hop.
/// Placeholder geometry until the ladder model arrives.
/// </summary>
public sealed class CarcassLadder : MonoBehaviour, IPlayerInteractable
{
    // Лестница в юго-западном углу клетки, вплотную к западной стене и сразу за колонной.
    const float WallClearance = 0.25f;
    const float CornerOffset = 2f;
    // Куда ставим игрока: на метр от лестницы вглубь комнаты, чтобы не застрять в ней.
    const float ArrivalDistance = 1.2f;
    const float LadderWidth = 0.7f;
    const float RungStep = 0.35f;
    const float RailSize = 0.08f;
    const float HatchSize = 1.1f;
    static readonly Color WoodColor = new Color(0.45f, 0.3f, 0.18f);
    static readonly Color HatchColor = new Color(0.22f, 0.16f, 0.1f);

    static Material _sharedMaterial;

    Transform _castle;
    Vector3 _destinationLocal;
    string _label;

    public string InteractLabel => _label;

    public bool CanInteract() => isActiveAndEnabled;

    public void Interact(PlayerInventory inventory)
    {
        if (inventory == null || _castle == null)
            return;

        Transform player = inventory.transform;
        var controller = player.GetComponent<CharacterController>();
        // CharacterController перетирает transform.position, пока включён.
        if (controller != null)
            controller.enabled = false;
        player.position = _castle.TransformPoint(_destinationLocal);
        if (controller != null)
            controller.enabled = true;
        // Пыль у лестницы/люка, а не на игроке: у частиц есть коллайдеры, они толкали бы капсулу.
        HitSparkVfx.PlayDust(transform.position + Vector3.up * 0.2f, Vector3.up, 8);
    }

    /// <summary>Ladder in cell (x, lowerY, z) going up to (x, lowerY + 1, z), plus the hatch above it.</summary>
    public static void CreatePair(Transform castle, Transform parent, int x, int lowerY, int z)
    {
        Vector3 corner = CarcassMetrics.CellCenterLocal(x, lowerY, z)
                         - new Vector3(CarcassMetrics.FloorSize * 0.5f, 0f, CarcassMetrics.FloorSize * 0.5f);
        float wallInnerX = corner.x + CarcassMetrics.WallThickness;
        float ladderX = wallInnerX + WallClearance;
        float ladderZ = corner.z + CarcassMetrics.ColumnSize + CornerOffset;
        float lowerFloorTop = lowerY * CarcassMetrics.ColumnHeight + CarcassMetrics.FloorThickness;
        float upperFloorTop = lowerFloorTop + CarcassMetrics.ColumnHeight;

        var ladderBase = new Vector3(ladderX, lowerFloorTop, ladderZ);
        var upArrival = new Vector3(ladderX + ArrivalDistance, upperFloorTop + 0.05f, ladderZ);
        var downArrival = new Vector3(ladderX + ArrivalDistance, lowerFloorTop + 0.05f, ladderZ);

        var ladder = BuildLadder(parent, ladderBase, upperFloorTop - lowerFloorTop + 1f);
        ladder.Bind(castle, upArrival, "Climb up");

        var hatch = BuildHatch(parent, new Vector3(ladderX + HatchSize * 0.5f - LadderWidth * 0.25f, upperFloorTop, ladderZ));
        hatch.Bind(castle, downArrival, "Climb down");

        KeepClear(parent, downArrival);
        KeepClear(parent, upArrival);
    }

    void Bind(Transform castle, Vector3 destinationLocal, string label)
    {
        _castle = castle;
        _destinationLocal = destinationLocal;
        _label = label;
    }

    /// <summary>
    /// Пустой триггер там, куда ставим игрока: станции на сетке пола проверяют триггеры
    /// и не встанут сюда — иначе переход выбросит игрока внутрь верстака.
    /// Отдельный объект, не ребёнок лестницы, чтобы луч E по нему не находил лестницу.
    /// </summary>
    static void KeepClear(Transform parent, Vector3 arrivalLocal)
    {
        var go = new GameObject("LadderKeepClear");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = arrivalLocal;
        var box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.center = new Vector3(0f, 1f, 0f);
        box.size = new Vector3(1.2f, 2f, 1.2f);
    }

    static CarcassLadder BuildLadder(Transform parent, Vector3 baseLocal, float height)
    {
        var root = new GameObject("Ladder");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = baseLocal;

        float half = LadderWidth * 0.5f;
        Part(root.transform, new Vector3(0f, height * 0.5f, -half), new Vector3(RailSize, height, RailSize), WoodColor);
        Part(root.transform, new Vector3(0f, height * 0.5f, half), new Vector3(RailSize, height, RailSize), WoodColor);
        for (float y = RungStep; y < height - 0.1f; y += RungStep)
            Part(root.transform, new Vector3(0f, y, 0f), new Vector3(RailSize * 0.8f, RailSize * 0.6f, LadderWidth), WoodColor);

        // Триггер на всю лестницу: луч E попадает в неё, а игрок проходит сквозь.
        var hit = root.AddComponent<BoxCollider>();
        hit.isTrigger = true;
        hit.center = new Vector3(0.15f, height * 0.5f, 0f);
        hit.size = new Vector3(0.5f, height, LadderWidth + 0.2f);
        return root.AddComponent<CarcassLadder>();
    }

    static CarcassLadder BuildHatch(Transform parent, Vector3 centerLocal)
    {
        var root = new GameObject("Hatch");
        root.transform.SetParent(parent, false);
        root.transform.localPosition = centerLocal;
        Part(root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(HatchSize, 0.03f, HatchSize), HatchColor);

        var hit = root.AddComponent<BoxCollider>();
        hit.isTrigger = true;
        hit.center = new Vector3(0f, 0.3f, 0f);
        hit.size = new Vector3(HatchSize, 0.6f, HatchSize);
        return root.AddComponent<CarcassLadder>();
    }

    static void Part(Transform parent, Vector3 localPos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Part";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = size;
        var renderer = go.GetComponent<MeshRenderer>();
        if (_sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _sharedMaterial = new Material(shader);
        }
        renderer.sharedMaterial = _sharedMaterial;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
