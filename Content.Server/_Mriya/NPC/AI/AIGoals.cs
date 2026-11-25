using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.NPC.AI;

public sealed partial class AIKillEnemyGoal : AIGoal
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public AIKillEnemyGoal()
    {
        Name = "KillEnemy";
        DesiredState.Set(AIKeys.TargetDead, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget && !_mobState.IsDead(comp.Target);
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.HasTarget || _mobState.IsDead(comp.Target))
            return 0f;

        var range = MathF.Max(comp.RangedRange, 0.1f);
        var closeness = 1f - Math.Clamp(comp.TargetDistance / range, 0f, 1f);
        var priority = 40f + closeness * 30f;

        priority += comp.TargetVisible ? 12f : -8f;

        if (comp.SquadRole == "Flank")
            priority += 6f;

        if (comp.TargetDistance <= comp.MeleeZoneRadius)
            priority += 15f;

        if (comp.SquadOrder == "AdvanceCover")
            priority += 10f;

        return priority;
    }
}

public sealed partial class AIDodgeGoal : AIGoal
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public AIDodgeGoal()
    {
        Name = "Dodge";
        DesiredState.Set(AIKeys.Dodged, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.TargetAimingAtMe;
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.TargetAimingAtMe)
            return 0f;

        if (_timing.CurTime < comp.DodgeCooldownUntil)
            return 0f;

        return 85f;
    }
}

public sealed partial class AICoverGoal : AIGoal
{
    public AICoverGoal()
    {
        Name = "Cover";
        DesiredState.Set(AIKeys.AtCover, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget && (comp.UnderFire || comp.LowHealth || !comp.InCover);
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.HasTarget)
            return 0f;

        if (comp.InCover && !comp.LowHealth && !comp.UnderFire)
            return 0f;

        var priority = 25f;

        if (comp.UnderFire)
            priority += 30f;

        if (comp.LowHealth)
            priority += 20f;

        if (comp.SquadOrder == "GetToCover")
            priority += 15f;

        return priority;
    }
}

public sealed partial class AIAmbushGoal : AIGoal
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public AIAmbushGoal()
    {
        Name = "Ambush";
        DesiredState.Set(AIKeys.AtAmbushNode, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.HasTarget)
            return false;

        if (comp.CoverCompromised)
            return true;

        return comp.SquadRole == "Flank"
               && _timing.CurTime >= comp.FlankCooldownUntil
               && comp.TargetDistance > comp.RangedRange * 0.5f;
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.HasTarget)
            return 0f;

        if (comp.CoverCompromised)
            return 75f;

        if (comp.SquadRole != "Flank" || _timing.CurTime < comp.FlankCooldownUntil)
            return 0f;

        return comp.TargetDistance > comp.RangedRange * 0.5f ? 66f : 0f;
    }
}

public sealed partial class AISuppressGoal : AIGoal
{
    public AISuppressGoal()
    {
        Name = "Suppress";
        DesiredState.Set(AIKeys.Suppressed, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.SquadRole == "Suppress"
               && comp.HasTarget
               && comp.WeaponLoaded;
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.HasTarget || !comp.WeaponLoaded || comp.SquadRole != "Suppress")
            return 0f;

        if (comp.TargetVisible && comp.TargetDistance <= comp.MeleeZoneRadius * 2f)
            return 0f;

        return comp.TargetVisible ? 72f : 50f;
    }
}

public sealed partial class AIFlushGoal : AIGoal
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public AIFlushGoal()
    {
        Name = "FlushOut";
        DesiredState.Set(AIKeys.TargetFlushed, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget
               && !_mobState.IsDead(comp.Target)
               && comp.SquadCanGrenade
               && comp.GrenadeAvailable
               && comp.TargetBehindCover;
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.HasTarget || !comp.SquadCanGrenade || !comp.GrenadeAvailable)
            return 0f;

        return comp.TargetBehindCover ? 85f : 0f;
    }
}

public sealed partial class AISearchGoal : AIGoal
{
    public AISearchGoal()
    {
        Name = "SearchForTarget";
        DesiredState.Set(AIKeys.TargetSearched, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget
               && comp.TargetInMemory
               && comp.SquadRole != "Suppress";
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!comp.HasTarget || !comp.TargetInMemory || comp.SquadRole == "Suppress")
            return 0f;

        return 42f;
    }
}

public sealed partial class AIFleeExplosionGoal : AIGoal
{
    public AIFleeExplosionGoal()
    {
        Name = "FleeExplosion";
        DesiredState.Set(AIKeys.SafeFromExplosion, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.GrenadeDanger;
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.GrenadeDanger ? 200f : 0f;
    }
}

public sealed partial class AIPatrolGoal : AIGoal
{
    public AIPatrolGoal()
    {
        Name = "Patrol";
        DesiredState.Set(AIKeys.PatrolActive, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return !comp.HasTarget;
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget ? 0f : 30f;
    }
}

public sealed partial class AIInspectGoal : AIGoal
{
    public AIInspectGoal()
    {
        Name = "InspectDisturbance";
        DesiredState.Set(AIKeys.Inspected, AIValues.True);
    }

    public override bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return !comp.HasTarget && comp.HeardDisturbance;
    }

    public override float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return !comp.HasTarget && comp.HeardDisturbance ? 35f : 0f;
    }
}
