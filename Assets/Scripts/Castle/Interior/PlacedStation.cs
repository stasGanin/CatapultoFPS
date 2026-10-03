using UnityEngine;

/// <summary>Marks a station standing on the castle floor; demolish reads its definition for the refund.</summary>
public sealed class PlacedStation : MonoBehaviour
{
    [SerializeField] StationDefinition _definition;

    public StationDefinition Definition => _definition;

    public void Bind(StationDefinition definition) => _definition = definition;

    /// <summary>Сундук с вещами не сносим — иначе содержимое молча пропадёт.</summary>
    public bool CanDemolish(out string reason)
    {
        var storage = GetComponentInChildren<StorageContainer>();
        if (storage != null && !storage.IsEmpty)
        {
            reason = "Empty the chest first";
            return false;
        }

        reason = null;
        return true;
    }
}
