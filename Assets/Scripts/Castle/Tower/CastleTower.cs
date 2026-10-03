using UnityEngine;

/// <summary>Внешний угол клетки, на который можно поставить башню: клетка плана и сторона (0/1 по X и Z).</summary>
public readonly struct TowerCorner
{
    public readonly int CellX;
    public readonly int CellZ;
    public readonly int DX;
    public readonly int DZ;

    public TowerCorner(int cellX, int cellZ, int dx, int dz)
    {
        CellX = cellX;
        CellZ = cellZ;
        DX = dx;
        DZ = dz;
    }

    /// <summary>Вершина сетки: общий ключ для всех клеток, сходящихся в этом углу.</summary>
    public Vector2Int Vertex => new Vector2Int(CellX + DX, CellZ + DZ);
}

/// <summary>
/// Башня-пушка на крыше угловой секции. Всегда стоит на верхнем этаже своей колонки:
/// когда сверху достраивают секцию, <see cref="MoveToFloor"/> переносит её на новую крышу.
/// Под башней, на потолке комнаты, висит люк — через него игрок садится за пушку.
/// </summary>
public sealed class CastleTower : MonoBehaviour, IDamageable
{
    public const float MaxHealth = 300f;

    CarcassCastle _castle;
    TowerCorner _corner;
    int _floorY;
    float _health;
    Transform _yaw;
    Transform _pitch;
    Transform _muzzle;
    Transform _seat;

    public TowerCorner Corner => _corner;
    public CarcassCastle Castle => _castle;
    public Transform Muzzle => _muzzle;
    public Vector3 SeatWorld => _seat.position;

    /// <summary>Пол комнаты под люком: сюда возвращается игрок, когда выходит из башни.</summary>
    public Vector3 ExitPointWorld
    {
        get
        {
            Vector3 local = transform.localPosition;
            local.y = _floorY * CarcassMetrics.ColumnHeight + CarcassMetrics.FloorThickness + 0.05f;
            return _castle.transform.TransformPoint(local);
        }
    }

    public static CastleTower Create(CarcassCastle castle, Transform parent, TowerCorner corner, int floorY)
    {
        CastleTowerVisual.Parts parts = CastleTowerVisual.Build(ghost: false);
        parts.Root.transform.SetParent(parent, false);
        var tower = parts.Root.AddComponent<CastleTower>();
        tower._castle = castle;
        tower._corner = corner;
        tower._health = MaxHealth;
        tower._yaw = parts.Yaw;
        tower._pitch = parts.Pitch;
        tower._muzzle = parts.Muzzle;
        tower._seat = parts.Seat;
        parts.Hatch.AddComponent<TowerHatch>().Bind(tower);
        if (castle.IsPlayerOwned)
            tower.gameObject.AddComponent<CastleTowerAutoFire>();
        else
            tower.gameObject.AddComponent<EnemyTowerFire>();
        tower.MoveToFloor(floorY);
        return tower;
    }

    public static GameObject CreateGhost() => CastleTowerVisual.Build(ghost: true).Root;

    public void MoveToFloor(int floorY)
    {
        _floorY = floorY;
        transform.localPosition = CarcassMetrics.TowerBaseLocal(_corner.CellX, _corner.CellZ, _corner.DX, _corner.DZ, floorY);
    }

    /// <summary>Разворачивает ствол в сторону прицела игрока: башня за ним только доворачивается.</summary>
    public void AimAt(Vector3 direction)
    {
        Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flat.sqrMagnitude > 0.0001f)
            _yaw.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        _pitch.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    /// <summary>Башню бьёт только противоположная сторона: свои взрывы рядом с ней её не трогают.</summary>
    public void ApplyDamage(float amount, in DamageInfo info)
    {
        if (info.FromPlayer == _castle.IsPlayerOwned || _health <= 0f)
            return;

        _health -= amount;
        if (_health <= 0f)
            _castle.RemoveTower(this, destroyedByDamage: true);
    }

    /// <summary>Снос или разрушение; <see cref="TowerOperator"/> сам решает, ранить ли игрока внутри.</summary>
    public void Dismantle(bool destroyedByDamage)
    {
        TowerOperator.NotifyTowerGone(this, destroyedByDamage);
        HitSparkVfx.PlayDust(transform.position + Vector3.up * 1.5f, Vector3.up, 24);
        Destroy(gameObject);
    }
}
