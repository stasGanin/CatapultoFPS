using UnityEngine;

/// <summary>Look-at E: door, chest, mage. Prompt is driven by LookTarget.</summary>
[DefaultExecutionOrder(20)]
public sealed class PlayerStorageInteractor : MonoBehaviour
{
    const float LookRange = 4f;

    [SerializeField] PlayerInputReader _input;
    [SerializeField] PlayerInventory _inventory;
    [SerializeField] Camera _camera;

    public IPlayerInteractable LookTarget { get; private set; }

    void Awake()
    {
        if (_input == null)
            _input = GetComponent<PlayerInputReader>();
        if (_inventory == null)
            _inventory = GetComponent<PlayerInventory>();
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (_input == null || _inventory == null)
            return;

        TickLook();

        if (!_input.InteractPressed)
            return;
        if (_inventory.BlocksGameplayInput
            && !_inventory.IsMenuOpen
            && !_inventory.IsMageOpen
            && !_inventory.IsFurniturePlacing)
            return;

        if (OwnMageStation.OpenStation != null)
        {
            OwnMageStation.OpenStation.Close();
            return;
        }

        if (LookTarget != null && LookTarget.CanInteract())
            LookTarget.Interact(_inventory);
    }

    void TickLook()
    {
        LookTarget = null;
        if (_camera == null)
            _camera = GetComponentInChildren<Camera>();
        if (_camera == null || _inventory.BlocksLook)
            return;

        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, LookRange, ~0, QueryTriggerInteraction.Collide))
            return;

        var target = hit.collider.GetComponentInParent<IPlayerInteractable>();
        if (target == null || !target.CanInteract())
            return;

        if (target is MonoBehaviour mb)
        {
            float dist = Vector3.Distance(transform.position, mb.transform.position);
            if (target is StorageContainer storage && !storage.IsInRange(transform.position))
                return;
            if (target is OwnMageStation mage && !mage.IsInRange(transform.position))
                return;
            if (dist > LookRange + 2f)
                return;
        }

        LookTarget = target;
    }
}
