#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds editable UI prefabs and places a GameUI root in the active scene.
/// Menu: Catapulto / UI / Generate Prefabs And Place In Scene
/// </summary>
public static class CatapultoUIGenerator
{
    const string PrefabFolder = "Assets/Prefabs/UI";
    const string GameUiPrefabPath = PrefabFolder + "/GameUI.prefab";
    const string SlotPrefabPath = PrefabFolder + "/InventorySlot.prefab";
    const string BuildIconPrefabPath = PrefabFolder + "/BuildIcon.prefab";

    [MenuItem("Catapulto/UI/Generate Prefabs And Place In Scene")]
    public static void GenerateAndPlace()
    {
        const string sampleScene = "Assets/Scenes/SampleScene.unity";
        if (File.Exists(Path.GetFullPath(sampleScene)) || AssetDatabase.LoadAssetAtPath<SceneAsset>(sampleScene) != null)
        {
            var active = SceneManager.GetActiveScene();
            if (active.path != sampleScene)
                EditorSceneManager.OpenScene(sampleScene);
        }

        EnsureFolder(PrefabFolder);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject slotPrefab = BuildSlotPrefab(font);
        GameObject iconPrefab = BuildIconPrefab(font);
        GameObject gameUi = BuildGameUi(font, slotPrefab.GetComponent<InventorySlotView>(), iconPrefab);

        PrefabUtility.SaveAsPrefabAsset(slotPrefab, SlotPrefabPath);
        PrefabUtility.SaveAsPrefabAsset(iconPrefab, BuildIconPrefabPath);
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(gameUi, GameUiPrefabPath);

        Object.DestroyImmediate(slotPrefab);
        Object.DestroyImmediate(iconPrefab);
        Object.DestroyImmediate(gameUi);

        PlaceInActiveScene(saved);
        StripLegacyPlayerUi();

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeObject = saved;
        Debug.Log("Catapulto UI: prefabs saved under Prefabs/UI and GameUI placed in the scene. Edit hierarchy freely.");
    }

    static void PlaceInActiveScene(GameObject prefab)
    {
        EnsureEventSystemInScene();

        var existing = Object.FindFirstObjectByType<GameUIRoot>();
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);

        PrefabUtility.InstantiatePrefab(prefab);
    }

    static void StripLegacyPlayerUi()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        // UI now lives on GameUI — remove old player-side UI components that rebuild canvases
        RemoveComponent<CrosshairUI>(player);
        RemoveComponent<InventoryUI>(player);
        RemoveComponent<BuildingMenuUI>(player);
        RemoveComponent<PlayerHealthHud>(player);
    }

    static void RemoveComponent<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null)
            Object.DestroyImmediate(c);
    }

    static void EnsureEventSystemInScene()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
        Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
    }

    static GameObject BuildSlotPrefab(Font font)
    {
        var go = new GameObject("InventorySlot", typeof(RectTransform));
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.85f, 0.75f, 0.35f, 1f);
        outline.effectDistance = new Vector2(2f, 2f);
        outline.enabled = false;

        var iconGo = CreateUiChild(go.transform, "Icon");
        Stretch(iconGo, 0.15f, 0.25f, 0.85f, 0.85f);
        var icon = iconGo.gameObject.AddComponent<Image>();
        icon.enabled = false;
        icon.raycastTarget = false;

        var labelGo = CreateUiChild(go.transform, "Label");
        Stretch(labelGo, 0f, 0f, 1f, 0.35f);
        var label = labelGo.gameObject.AddComponent<Text>();
        label.font = font;
        label.fontSize = 12;
        label.alignment = TextAnchor.LowerCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        var hintGo = CreateUiChild(go.transform, "KeyHint");
        Stretch(hintGo, 0f, 0.7f, 0.4f, 1f);
        hintGo.offsetMin = new Vector2(2f, 0f);
        var hint = hintGo.gameObject.AddComponent<Text>();
        hint.font = font;
        hint.fontSize = 11;
        hint.alignment = TextAnchor.UpperLeft;
        hint.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
        hint.raycastTarget = false;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = 56f;
        le.preferredHeight = le.minHeight = 56f;

        var view = go.AddComponent<InventorySlotView>();
        var so = new SerializedObject(view);
        so.FindProperty("_background").objectReferenceValue = bg;
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_label").objectReferenceValue = label;
        so.FindProperty("_outline").objectReferenceValue = outline;
        so.FindProperty("_keyHint").objectReferenceValue = hint;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    static GameObject BuildIconPrefab(Font font)
    {
        var go = new GameObject("BuildIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var img = go.GetComponent<Image>();
        img.color = Color.gray;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);

        var cost = CreateUiChild(go.transform, "Cost");
        cost.anchorMin = cost.anchorMax = new Vector2(1f, 0f);
        cost.pivot = new Vector2(1f, 0f);
        cost.anchoredPosition = new Vector2(-4f, 4f);
        cost.sizeDelta = new Vector2(40f, 20f);
        var costText = cost.gameObject.AddComponent<Text>();
        costText.font = font;
        costText.fontSize = 14;
        costText.alignment = TextAnchor.LowerRight;
        costText.color = Color.white;
        costText.raycastTarget = false;

        var name = CreateUiChild(go.transform, "Name");
        name.anchorMin = new Vector2(0f, 1f);
        name.anchorMax = new Vector2(1f, 1f);
        name.pivot = new Vector2(0.5f, 1f);
        name.anchoredPosition = new Vector2(0f, -4f);
        name.sizeDelta = new Vector2(-6f, 18f);
        var nameText = name.gameObject.AddComponent<Text>();
        nameText.font = font;
        nameText.fontSize = 12;
        nameText.alignment = TextAnchor.UpperCenter;
        nameText.color = Color.white;
        nameText.raycastTarget = false;

        go.GetComponent<Button>().targetGraphic = img;
        go.AddComponent<BuildIconHover>();
        return go;
    }

    static GameObject BuildGameUi(Font font, InventorySlotView slotPrefab, GameObject iconPrefab)
    {
        var root = new GameObject("GameUI");
        var uiRoot = root.AddComponent<GameUIRoot>();

        // --- Health ---
        var healthCanvas = CreateCanvas(root.transform, "HealthCanvas", 40);
        var hpTextGo = CreateUiChild(healthCanvas.transform, "HpText");
        hpTextGo.anchorMin = hpTextGo.anchorMax = new Vector2(0f, 1f);
        hpTextGo.pivot = new Vector2(0f, 1f);
        hpTextGo.anchoredPosition = new Vector2(24f, -20f);
        hpTextGo.sizeDelta = new Vector2(420f, 40f);
        var hpText = hpTextGo.gameObject.AddComponent<Text>();
        hpText.font = font;
        hpText.fontSize = 22;
        hpText.color = new Color(0.95f, 0.95f, 0.92f, 0.9f);
        hpText.alignment = TextAnchor.UpperLeft;
        hpText.text = "HP  10000 / 10000";
        hpText.raycastTarget = false;
        var healthHud = healthCanvas.AddComponent<PlayerHealthHud>();
        SetRef(healthHud, "_label", hpText);

        // --- Crosshair ---
        var crossCanvas = CreateCanvas(root.transform, "CrosshairCanvas", 50);
        var crossRoot = CreateUiChild(crossCanvas.transform, "Crosshair");
        crossRoot.anchorMin = crossRoot.anchorMax = new Vector2(0.5f, 0.5f);
        crossRoot.sizeDelta = Vector2.zero;
        CreateCrossArm(crossRoot, Vector2.zero, new Vector2(4f, 4f));
        CreateCrossArm(crossRoot, new Vector2(0f, 11f), new Vector2(2f, 10f));
        CreateCrossArm(crossRoot, new Vector2(0f, -11f), new Vector2(2f, 10f));
        CreateCrossArm(crossRoot, new Vector2(11f, 0f), new Vector2(10f, 2f));
        CreateCrossArm(crossRoot, new Vector2(-11f, 0f), new Vector2(10f, 2f));
        var crosshair = crossCanvas.AddComponent<CrosshairUI>();
        SetRef(crosshair, "_root", crossRoot.gameObject);

        // --- Inventory ---
        var invCanvas = CreateCanvas(root.transform, "InventoryCanvas", 100);
        var dragLayer = CreateUiChild(invCanvas.transform, "DragLayer");
        StretchFull(dragLayer);

        var hotbar = CreateUiChild(invCanvas.transform, "Hotbar");
        hotbar.anchorMin = hotbar.anchorMax = new Vector2(0.5f, 0f);
        hotbar.pivot = new Vector2(0.5f, 0f);
        hotbar.anchoredPosition = new Vector2(0f, 24f);
        hotbar.sizeDelta = new Vector2(520f, 72f);
        var hotbarImg = hotbar.gameObject.AddComponent<Image>();
        hotbarImg.color = new Color(0.05f, 0.05f, 0.05f, 0.55f);
        var hotbarLayout = hotbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hotbarLayout.spacing = 6f;
        hotbarLayout.childAlignment = TextAnchor.MiddleCenter;
        hotbarLayout.padding = new RectOffset(8, 8, 8, 8);

        for (int i = 0; i < PlayerInventory.HotbarSize; i++)
        {
            var slot = Object.Instantiate(slotPrefab, hotbar);
            slot.name = $"Hotbar_{i + 1}";
            var keyHintText = slot.transform.Find("KeyHint")?.GetComponent<Text>();
            if (keyHintText != null)
                keyHintText.text = (i + 1).ToString();
        }

        var bag = CreateUiChild(invCanvas.transform, "BagPanel");
        bag.anchorMin = bag.anchorMax = new Vector2(0.5f, 0.5f);
        bag.sizeDelta = new Vector2(420f, 360f);
        bag.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        var title = CreateUiChild(bag, "Title");
        title.anchorMin = new Vector2(0f, 1f);
        title.anchorMax = new Vector2(1f, 1f);
        title.pivot = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(0f, 36f);
        title.anchoredPosition = new Vector2(0f, -8f);
        var titleText = title.gameObject.AddComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 22;
        titleText.alignment = TextAnchor.UpperCenter;
        titleText.color = Color.white;
        titleText.text = "Inventory";
        titleText.raycastTarget = false;

        var bagGrid = CreateUiChild(bag, "BagGrid");
        Stretch(bagGrid, 0.05f, 0.08f, 0.95f, 0.85f);
        var grid = bagGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(56f, 56f);
        grid.spacing = new Vector2(6f, 6f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 9;
        grid.childAlignment = TextAnchor.UpperCenter;

        for (int i = 0; i < PlayerInventory.BagSize; i++)
        {
            var slot = Object.Instantiate(slotPrefab, bagGrid);
            slot.name = $"Bag_{PlayerInventory.HotbarSize + i}";
            slot.SetKeyHint(string.Empty);
        }

        bag.gameObject.SetActive(false);
        dragLayer.SetAsLastSibling();

        var inventoryUi = invCanvas.AddComponent<InventoryUI>();
        SetRef(inventoryUi, "_bagRoot", bag.gameObject);
        SetRef(inventoryUi, "_dragLayer", dragLayer);
        SetRef(inventoryUi, "_hotbarRoot", hotbar);
        SetRef(inventoryUi, "_bagGridRoot", bagGrid);
        SetRef(inventoryUi, "_slotPrefab", slotPrefab);

        // --- Building ---
        var buildCanvas = CreateCanvas(root.transform, "BuildingCanvas", 200);
        var buildPanel = CreateUiChild(buildCanvas.transform, "BuildPanel");
        buildPanel.anchorMin = buildPanel.anchorMax = new Vector2(0.5f, 0.5f);
        buildPanel.sizeDelta = new Vector2(560f, 460f);
        buildPanel.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.96f);

        var bTitle = CreateUiChild(buildPanel, "Title");
        bTitle.anchorMin = new Vector2(0f, 1f);
        bTitle.anchorMax = new Vector2(1f, 1f);
        bTitle.pivot = new Vector2(0.5f, 1f);
        bTitle.anchoredPosition = new Vector2(0f, -12f);
        bTitle.sizeDelta = new Vector2(-24f, 36f);
        var bTitleText = bTitle.gameObject.AddComponent<Text>();
        bTitleText.font = font;
        bTitleText.fontSize = 28;
        bTitleText.alignment = TextAnchor.UpperCenter;
        bTitleText.color = Color.white;
        bTitleText.text = "Building";
        bTitleText.raycastTarget = false;

        var stone = CreateUiChild(buildPanel, "StoneCount");
        stone.anchorMin = stone.anchorMax = new Vector2(1f, 1f);
        stone.pivot = new Vector2(1f, 1f);
        stone.anchoredPosition = new Vector2(-16f, -16f);
        stone.sizeDelta = new Vector2(160f, 28f);
        var stoneText = stone.gameObject.AddComponent<Text>();
        stoneText.font = font;
        stoneText.fontSize = 18;
        stoneText.alignment = TextAnchor.UpperRight;
        stoneText.color = Color.white;
        stoneText.text = "Stone: 0";
        stoneText.raycastTarget = false;

        var hint = CreateUiChild(buildPanel, "Hint");
        hint.anchorMin = new Vector2(0f, 0f);
        hint.anchorMax = new Vector2(1f, 0f);
        hint.pivot = new Vector2(0.5f, 0f);
        hint.anchoredPosition = new Vector2(0f, 10f);
        hint.sizeDelta = new Vector2(-20f, 24f);
        var hintText = hint.gameObject.AddComponent<Text>();
        hintText.font = font;
        hintText.fontSize = 14;
        hintText.alignment = TextAnchor.LowerCenter;
        hintText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        hintText.text = "LMB select · RMB / Esc close · I inventory";
        hintText.raycastTarget = false;

        var buildGrid = CreateUiChild(buildPanel, "Grid");
        buildGrid.anchorMin = buildGrid.anchorMax = new Vector2(0.5f, 0.5f);
        buildGrid.sizeDelta = new Vector2(480f, 300f);
        buildGrid.anchoredPosition = new Vector2(0f, 10f);
        var bGrid = buildGrid.gameObject.AddComponent<GridLayoutGroup>();
        bGrid.cellSize = new Vector2(96f, 96f);
        bGrid.spacing = new Vector2(12f, 12f);
        bGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        bGrid.constraintCount = 4;

        var tip = CreateUiChild(buildCanvas.transform, "BuildTooltip");
        tip.anchorMin = tip.anchorMax = Vector2.zero;
        tip.sizeDelta = new Vector2(240f, 70f);
        var tipImg = tip.gameObject.AddComponent<Image>();
        tipImg.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        tipImg.raycastTarget = false;
        var tipTextGo = CreateUiChild(tip, "TipText");
        StretchFull(tipTextGo);
        tipTextGo.offsetMin = new Vector2(10f, 6f);
        tipTextGo.offsetMax = new Vector2(-10f, -6f);
        var tipText = tipTextGo.gameObject.AddComponent<Text>();
        tipText.font = font;
        tipText.fontSize = 16;
        tipText.alignment = TextAnchor.MiddleLeft;
        tipText.color = Color.white;
        tipText.raycastTarget = false;
        tip.gameObject.SetActive(false);
        buildCanvas.SetActive(false);

        var buildingMenu = buildCanvas.AddComponent<BuildingMenuUI>();
        SetRef(buildingMenu, "_canvasGo", buildCanvas);
        SetRef(buildingMenu, "_root", buildPanel.gameObject);
        SetRef(buildingMenu, "_gridRoot", buildGrid);
        SetRef(buildingMenu, "_tooltip", tip.gameObject);
        SetRef(buildingMenu, "_tooltipText", tipText);
        SetRef(buildingMenu, "_stoneCountText", stoneText);
        SetRef(buildingMenu, "_iconPrefab", iconPrefab);

        // Wire GameUIRoot
        var rootSo = new SerializedObject(uiRoot);
        rootSo.FindProperty("_healthHud").objectReferenceValue = healthHud;
        rootSo.FindProperty("_crosshair").objectReferenceValue = crosshair;
        rootSo.FindProperty("_inventoryUi").objectReferenceValue = inventoryUi;
        rootSo.FindProperty("_buildingMenu").objectReferenceValue = buildingMenu;
        rootSo.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    static GameObject CreateCanvas(Transform parent, string name, int sort)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sort;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static RectTransform CreateUiChild(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void CreateCrossArm(Transform parent, Vector2 pos, Vector2 size)
    {
        var rt = CreateUiChild(parent, "Arm");
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.85f);
        img.raycastTarget = false;
    }

    static void Stretch(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetRef(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
