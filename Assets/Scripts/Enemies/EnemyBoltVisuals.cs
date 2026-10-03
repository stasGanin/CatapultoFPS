using UnityEngine;

[CreateAssetMenu(fileName = "EnemyBoltVisuals", menuName = "Catapulto/Enemies/Bolt Visuals")]
public sealed class EnemyBoltVisuals : ScriptableObject
{
    [SerializeField] GameObject _flight;
    [SerializeField] GameObject _impact;
    [SerializeField] GameObject _muzzle;

    public GameObject Flight => _flight;
    public GameObject Impact => _impact;
    public GameObject Muzzle => _muzzle;
}
