using System;
using UnityEngine;

/// <summary>Один талант: ветка = строка в окне, Depth = колонка. Пререквизиты — id узлов той же ветки, нужен хотя бы 1 ранг.</summary>
[Serializable]
public sealed class TalentNode
{
    [SerializeField] string _id = "talent";
    [SerializeField] string _displayName = "Talent";
    [SerializeField] string _description;
    [SerializeField] string _branch = "Branch";
    [SerializeField, Min(0)] int _depth;
    [SerializeField] TalentStat _stat;
    [Tooltip("Прибавка за один ранг: для долей 0.1 = +10%, для остального — плоское значение.")]
    [SerializeField] float _valuePerRank = 0.1f;
    [SerializeField, Min(1)] int _maxRank = 3;
    [SerializeField] string[] _prerequisiteIds = Array.Empty<string>();

    public string Id => _id;
    public string DisplayName => _displayName;
    public string Description => _description ?? string.Empty;
    public string Branch => _branch;
    public int Depth => _depth;
    public TalentStat Stat => _stat;
    public float ValuePerRank => _valuePerRank;
    public int MaxRank => Mathf.Max(1, _maxRank);
    public string[] PrerequisiteIds => _prerequisiteIds ?? Array.Empty<string>();
}

/// <summary>Все таланты героя. Лежит в Resources/Talents/TalentTree.</summary>
[CreateAssetMenu(menuName = "Catapulto/Progression/Talent Tree", fileName = "TalentTree")]
public sealed class TalentTree : ScriptableObject
{
    public const string ResourcePath = "Talents/TalentTree";

    [SerializeField] TalentNode[] _nodes = Array.Empty<TalentNode>();

    public TalentNode[] Nodes => _nodes ?? Array.Empty<TalentNode>();

    public TalentNode Find(string id)
    {
        foreach (var node in Nodes)
        {
            if (node.Id == id)
                return node;
        }

        return null;
    }
}
