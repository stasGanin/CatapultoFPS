using System;
using UnityEngine;

/// <summary>All research nodes shown on the research table.</summary>
[CreateAssetMenu(menuName = "Catapulto/Crafting/Research Tree", fileName = "ResearchTree")]
public sealed class ResearchTree : ScriptableObject
{
    public const string ResourcePath = "Crafting/ResearchTree";

    [SerializeField] ResearchNode[] _nodes = Array.Empty<ResearchNode>();

    public ResearchNode[] Nodes => _nodes ?? Array.Empty<ResearchNode>();
}
