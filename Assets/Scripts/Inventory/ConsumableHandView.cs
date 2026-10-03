using UnityEngine;

/// <summary>
/// Viewmodel расходника в руке. Пока шар-заглушка цвета предмета; во время поедания
/// поднимается к лицу и уменьшается — место под настоящую анимацию.
/// </summary>
public sealed class ConsumableHandView : MonoBehaviour
{
    static readonly Vector3 RestPosition = new Vector3(0.3f, -0.25f, 0.5f);
    static readonly Vector3 EatPosition = new Vector3(0.05f, -0.1f, 0.3f);
    const float BaseScale = 0.14f;

    [SerializeField] PlayerInventory _inventory;
    [SerializeField] ConsumableUser _user;

    Transform _view;
    MeshRenderer _renderer;
    MaterialPropertyBlock _block;

    void Awake()
    {
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_user == null)
            _user = GetComponent<ConsumableUser>();
    }

    void LateUpdate()
    {
        ItemDefinition item = _inventory.SelectedItem;
        bool show = item != null && item.IsConsumable && !_inventory.BlocksGameplayInput && !TowerOperator.IsOperating;
        if (!show)
        {
            if (_view != null)
                _view.gameObject.SetActive(false);
            return;
        }

        EnsureView();
        if (_view == null)
            return;

        _view.gameObject.SetActive(true);
        SetColor(item.IconColor);
        float t = _user.EatProgress;
        _view.localPosition = Vector3.Lerp(RestPosition, EatPosition, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, t * 4f)));
        _view.localScale = Vector3.one * BaseScale * Mathf.Lerp(1f, 0.3f, t);
    }

    void EnsureView()
    {
        if (_view != null)
            return;

        Camera cam = GetComponentInChildren<Camera>();
        if (cam == null)
            return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ConsumableView";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(cam.transform, false);
        _view = go.transform;
        _renderer = go.GetComponent<MeshRenderer>();
        _block = new MaterialPropertyBlock();
    }

    void SetColor(Color color)
    {
        _renderer.GetPropertyBlock(_block);
        _block.SetColor("_BaseColor", color);
        _block.SetColor("_Color", color);
        _renderer.SetPropertyBlock(_block);
    }
}
