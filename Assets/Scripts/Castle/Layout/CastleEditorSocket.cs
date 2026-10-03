using UnityEngine;

/// <summary>Click target for the in-game castle layout editor.</summary>
public sealed class CastleEditorSocket : MonoBehaviour
{
    public enum Kind
    {
        Opening = 0,
        Small = 1,
        Power = 2,
        Section = 3
    }

    public Kind SocketKind;
    public int X;
    public int Y;
    public int Z;
    public int Wall;
    public int Col;
    public int Row;
    public int Index;
    public int Corner;
}
