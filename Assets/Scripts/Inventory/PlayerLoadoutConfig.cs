using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the player starts with in the bag. Hotbar tools are wired separately.</summary>
[CreateAssetMenu(menuName = "Catapulto/Player/Player Loadout Config", fileName = "PlayerLoadoutConfig")]
public sealed class PlayerLoadoutConfig : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public ItemDefinition Item;
        [Min(1)] public int Count;
    }

    [SerializeField] Entry[] _bagItems = Array.Empty<Entry>();

    public IReadOnlyList<Entry> BagItems => _bagItems;
}
