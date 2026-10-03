using UnityEngine;

/// <summary>
/// Shared enemy decision layer: which goal (exit home / fight player / siege wall / guard)
/// and what the enemy currently knows about the player. Throttled to avoid per-frame target flip.
/// </summary>
public sealed class EnemyTargeting
{
    public enum Goal { Exit, Engage, Siege, Guard }

    const float SenseInterval = 0.25f;
    const float ExitTimeout = 5f;

    readonly EnemyConfig _config;
    readonly Transform _self;

    float _nextSense;
    float _lastSeenTime = float.NegativeInfinity;
    float _exitElapsed;

    public EnemyTargeting(EnemyConfig config, Transform self)
    {
        _config = config;
        _self = self;
    }

    public Goal Current { get; private set; } = Goal.Guard;
    public bool SeesPlayer { get; private set; }
    public Vector3 LastSeenPos { get; private set; }
    public CarcassWallBreakable SiegeWall { get; private set; }
    public Transform HomeCastle { get; private set; }
    public Vector3 HomeCenter { get; private set; }

    /// <summary>Returns true when the enemy was born inside its castle and must walk out first.</summary>
    public bool Begin(Transform homeCastle, Vector3 position)
    {
        HomeCastle = homeCastle;
        HomeCenter = homeCastle != null ? EnemySenses.CastleCenter(homeCastle) : position;
        bool bornInside = EnemySenses.IsInsideCastle(homeCastle, position);
        Current = bornInside ? Goal.Exit : Goal.Guard;
        _exitElapsed = 0f;
        _nextSense = 0f;
        return bornInside;
    }

    /// <summary>Re-evaluates the goal at a fixed cadence. Returns true on the tick the exit phase ends.</summary>
    public bool Tick(Vector3 eyePosition)
    {
        if (Time.time < _nextSense)
            return false;
        _nextSense = Time.time + SenseInterval;

        bool exitFinished = false;
        if (Current == Goal.Exit)
        {
            _exitElapsed += SenseInterval;
            bool outside = !EnemySenses.IsInsideCastle(HomeCastle, eyePosition);
            if (!outside && _exitElapsed < ExitTimeout)
                return false;
            exitFinished = true;
        }

        Transform player = EnemySenses.Player;
        SeesPlayer = false;
        if (player != null)
        {
            Vector3 aim = EnemySenses.PlayerAimPoint();
            if (Vector3.Distance(eyePosition, aim) <= _config.AggroRange
                && EnemySenses.HasClearPath(eyePosition, aim, 0f, _self, player))
            {
                SeesPlayer = true;
                _lastSeenTime = Time.time;
                LastSeenPos = aim;
            }
        }

        // Короткая память о игроке — без неё цель дёргается между игроком и стеной на каждом кадре.
        if (player != null && Time.time - _lastSeenTime <= _config.LoseSightGrace)
        {
            Current = Goal.Engage;
            return exitFinished;
        }

        if (SiegeWall == null || SiegeWall.IsBreached)
            SiegeWall = PickSiegeWall(eyePosition);
        Current = SiegeWall != null ? Goal.Siege : Goal.Guard;
        return exitFinished;
    }

    /// <summary>Получив урон, враг «замечает» игрока, даже если по нему стреляли из-за угла.</summary>
    public void NotifyDamaged()
    {
        if (EnemySenses.Player == null)
            return;
        _lastSeenTime = Time.time;
        LastSeenPos = EnemySenses.PlayerAimPoint();
        _nextSense = 0f;
    }

    CarcassWallBreakable PickSiegeWall(Vector3 from)
    {
        SquareCastle playerCastle = EnemySenses.PlayerCastle;
        if (playerCastle == null)
            return null;
        Vector3 target = EnemySenses.CastleCenter(playerCastle.transform);
        if (EnemySenses.PlanarDistance(HomeCenter, target) > _config.RaidRange)
            return null;
        return CarcassWallBreakable.FindNearestStanding(from, playerOwned: true);
    }
}
