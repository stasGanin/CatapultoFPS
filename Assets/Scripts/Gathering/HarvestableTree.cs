using UnityEngine;

/// <summary>Pickaxe target: terrain-painted tree or a scene mesh tree.</summary>
public sealed class HarvestableTree : MonoBehaviour
{
    [SerializeField] int _maxHp = 18;

    Terrain _terrain;
    int _treeIndex = -1;
    int _hp;
    bool _cut;

    void Awake()
    {
        if (_hp <= 0)
            _hp = Mathf.Max(1, _maxHp);
    }

    public void Bind(Terrain terrain, int treeIndex)
    {
        _terrain = terrain;
        _treeIndex = treeIndex;
        _hp = Mathf.Max(1, _maxHp);
    }

    public bool TryChop(int damage, Vector3 hitPoint, Vector3 hitNormal, float lootMultiplier = 1f)
    {
        if (_cut || damage <= 0)
            return false;

        HitSparkVfx.Play(hitPoint, hitNormal);
        _hp -= damage;
        if (_hp > 0)
            return true;

        _cut = true;
        HideVisual();
        SpawnStump();
        SpawnWood(lootMultiplier);
        Destroy(gameObject);
        return true;
    }

    void HideVisual()
    {
        if (_terrain != null)
        {
            HideTerrainTree();
            return;
        }

        var rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].enabled = false;
        }
    }

    void HideTerrainTree()
    {
        if (_terrain.terrainData == null)
            return;

        var data = _terrain.terrainData;
        var trees = data.treeInstances;
        if (_treeIndex < 0 || _treeIndex >= trees.Length)
            return;

        var inst = trees[_treeIndex];
        inst.heightScale = 0f;
        inst.widthScale = 0f;
        trees[_treeIndex] = inst;
        data.treeInstances = trees;
    }

    void SpawnStump()
    {
        var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stump.name = "TreeStump";
        stump.transform.position = transform.position + Vector3.up * 0.22f;
        stump.transform.localScale = new Vector3(0.55f, 0.22f, 0.55f);
        ApplyColor(stump, new Color(0.32f, 0.2f, 0.12f));
    }

    void SpawnWood(float lootMultiplier)
    {
        var wood = Resources.Load<ItemDefinition>("Items/WoodItem");
        if (wood == null)
            return;
        int count = Mathf.Max(1, Mathf.RoundToInt(Random.Range(2, 5) * lootMultiplier));
        WorldLootPickup.Spawn(wood, count, transform.position + Vector3.up * 0.4f, 1.8f);
    }

    static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
            renderer.sharedMaterial = new Material(shader);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
