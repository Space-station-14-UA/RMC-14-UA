using System.Linq;
using System.Numerics;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.NPC.AI;

public sealed class AISquadSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float UpdateInterval = 0.5f;
    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < UpdateInterval)
            return;

        _accumulator = 0f;

        var members = new List<(EntityUid Uid, AIPlannerComponent Comp)>();
        var query = EntityQueryEnumerator<AIPlannerComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled || TerminatingOrDeleted(uid))
                continue;

            members.Add((uid, comp));
        }

        if (members.Count == 0)
            return;

        var assigned = new HashSet<EntityUid>();
        var squadId = 0;

        foreach (var (uid, comp) in members)
        {
            if (!assigned.Add(uid))
                continue;

            var squad = new List<(EntityUid Uid, AIPlannerComponent Comp)> { (uid, comp) };
            var origin = GetPosition(uid);

            foreach (var (otherUid, otherComp) in members)
            {
                if (otherUid == uid || assigned.Contains(otherUid))
                    continue;

                if (Vector2.Distance(origin, GetPosition(otherUid)) > comp.SquadRadius)
                    continue;

                assigned.Add(otherUid);
                squad.Add((otherUid, otherComp));
            }

            AssignSquad(squadId++, squad);
        }

        UpdatePatrolGroups(members);
    }

    private void AssignSquad(int id, List<(EntityUid Uid, AIPlannerComponent Comp)> squad)
    {
        var grenadeCooldown = squad.Max(member => member.Comp.GrenadeCooldownUntil);

        foreach (var (_, comp) in squad)
            comp.SquadGrenadeCooldownUntil = grenadeCooldown;

        var combatants = squad.Where(member => member.Comp.HasTarget).ToList();

        var roleDuration = MathF.Max(squad[0].Comp.SquadRoleDuration, 1f);
        var phase = _timing.CurTime.TotalSeconds + id * 3.7;
        var rotation = (int) (phase / roleDuration);

        var suppressIndex = -1;

        if (combatants.Count >= 2)
            suppressIndex = ((rotation % combatants.Count) + combatants.Count) % combatants.Count;

        foreach (var (uid, comp) in squad)
        {
            comp.SquadId = id;
            comp.SquadRole = "None";
            comp.SquadCanGrenade = comp.CurrentGoal is AIFlushGoal;
            comp.SquadOrder = comp.HasTarget ? "AdvanceCover" : "Search";

            if (!comp.HasTarget)
                continue;

            var combatIndex = combatants.FindIndex(member => member.Uid == uid);

            if (combatIndex == suppressIndex)
            {
                comp.SquadRole = "Suppress";
                comp.SquadOrder = "GetToCover";
            }
            else
            {
                comp.SquadRole = "Flank";
            }
        }

        NominateGrenadier(squad, combatants);
    }

    private void NominateGrenadier(
        List<(EntityUid Uid, AIPlannerComponent Comp)> squad,
        List<(EntityUid Uid, AIPlannerComponent Comp)> combatants)
    {
        if (squad.Any(member => member.Comp.CurrentGoal is AIFlushGoal))
            return;

        if (combatants.Count == 0)
            return;

        if (_timing.CurTime < squad[0].Comp.SquadGrenadeCooldownUntil)
            return;

        foreach (var (uid, comp) in combatants)
        {
            if (!comp.GrenadeAvailable || !comp.TargetBehindCover || comp.TargetVisible)
                continue;

            if (AllyInBlast(squad, uid, comp.LastKnownTargetPosition, comp.GrenadeFriendlyRadius))
                continue;

            comp.SquadCanGrenade = true;
            return;
        }
    }

    private bool AllyInBlast(
        List<(EntityUid Uid, AIPlannerComponent Comp)> squad,
        EntityUid thrower,
        Vector2 impact,
        float radius)
    {
        foreach (var (uid, comp) in squad)
        {
            if (uid == thrower || !comp.HasTarget)
                continue;

            if (Vector2.Distance(GetPosition(uid), impact) <= radius)
                return true;
        }

        return false;
    }

    private void UpdatePatrolGroups(List<(EntityUid Uid, AIPlannerComponent Comp)> members)
    {
        var patrollers = new List<(EntityUid Uid, AIPlannerComponent Comp)>();

        foreach (var member in members)
        {
            if (member.Comp.HasTarget)
            {
                ClearPatrolGroup(member.Comp);
                continue;
            }

            patrollers.Add(member);
        }

        if (patrollers.Count == 0)
            return;

        patrollers.Sort((a, b) => a.Uid.Id.CompareTo(b.Uid.Id));

        var claimed = new HashSet<EntityUid>();

        foreach (var seed in patrollers)
        {
            if (claimed.Contains(seed.Uid))
                continue;

            var seedPos = GetPosition(seed.Uid);
            var radius = MathF.Max(seed.Comp.PatrolGroupRadius, 1f);
            var minSize = Math.Max(seed.Comp.PatrolMinGroupSize, 2);
            var maxSize = Math.Max(seed.Comp.PatrolMaxGroupSize, minSize);

            var candidates = new List<(EntityUid Uid, AIPlannerComponent Comp)>();

            foreach (var other in patrollers)
            {
                if (other.Uid == seed.Uid || claimed.Contains(other.Uid))
                    continue;

                if (Vector2.Distance(seedPos, GetPosition(other.Uid)) > radius)
                    continue;

                candidates.Add(other);
            }

            candidates.Sort((a, b) =>
            {
                var aRank = a.Comp.PatrolLeader == seed.Uid ? a.Comp.PatrolSlot : int.MaxValue - 1;
                var bRank = b.Comp.PatrolLeader == seed.Uid ? b.Comp.PatrolSlot : int.MaxValue - 1;
                var rank = aRank.CompareTo(bRank);

                if (rank != 0)
                    return rank;

                return Vector2.DistanceSquared(seedPos, GetPosition(a.Uid))
                    .CompareTo(Vector2.DistanceSquared(seedPos, GetPosition(b.Uid)));
            });

            var cluster = new List<(EntityUid Uid, AIPlannerComponent Comp)> { seed };
            claimed.Add(seed.Uid);

            foreach (var candidate in candidates)
            {
                if (cluster.Count >= maxSize)
                    break;

                cluster.Add(candidate);
                claimed.Add(candidate.Uid);
            }

            if (cluster.Count < minSize)
            {
                foreach (var member in cluster)
                    ClearPatrolGroup(member.Comp);

                continue;
            }

            var leaderPos = GetPosition(seed.Uid);
            var spread = 0f;

            for (var i = 0; i < cluster.Count; i++)
            {
                var member = cluster[i];
                member.Comp.PatrolLeader = seed.Uid;
                member.Comp.PatrolSlot = i;
                member.Comp.PatrolGroupSize = cluster.Count;

                if (member.Uid != seed.Uid)
                    spread = MathF.Max(spread, Vector2.Distance(leaderPos, GetPosition(member.Uid)));
            }

            foreach (var member in cluster)
                member.Comp.PatrolGroupSpread = spread;
        }
    }

    private static void ClearPatrolGroup(AIPlannerComponent comp)
    {
        comp.PatrolLeader = EntityUid.Invalid;
        comp.PatrolSlot = -1;
        comp.PatrolGroupSize = 0;
        comp.PatrolGroupSpread = 0f;
    }

    private Vector2 GetPosition(EntityUid uid)
    {
        return _transform.GetWorldPosition(Transform(uid));
    }
}
