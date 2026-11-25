using System.Linq;
using System.Numerics;
using Content.Server.Hands.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.NPC.AI;

public sealed class AIPlannerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AIPlannerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AIPlannerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(EntityUid uid, AIPlannerComponent comp, MapInitEvent args)
    {
        EnsureComp<ActiveNPCComponent>(uid);

        if (comp.UseDefaultActions && comp.Actions.Count == 0)
            comp.Actions = AIDefaults.CreateActions();

        if (comp.UseDefaultGoals && comp.Goals.Count == 0)
            comp.Goals = AIDefaults.CreateGoals();

        foreach (var action in comp.Actions)
            action.Initialize(EntityManager.EntitySysManager);

        foreach (var goal in comp.Goals)
            goal.Initialize(EntityManager.EntitySysManager);

        comp.NeedsReplan = true;
        comp.PlanAccumulator = 0f;
        comp.SensorAccumulator = 0f;
    }

    private void OnShutdown(EntityUid uid, AIPlannerComponent comp, ComponentShutdown args)
    {
        ShutdownCurrent(uid, comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<AIPlannerComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled || TerminatingOrDeleted(uid))
                continue;

            if (_mobState.IsDead(uid) || _mobState.IsIncapacitated(uid))
            {
                ShutdownCurrent(uid, comp);
                continue;
            }

            comp.SensorAccumulator -= frameTime;

            if (comp.SensorAccumulator <= 0f)
            {
                comp.SensorAccumulator += MathF.Max(comp.SensorInterval, 0.05f);
                UpdateSensors(uid, comp);
            }

            comp.PlanAccumulator -= frameTime;

            var needsPlan = comp.NeedsReplan
                            || comp.Plan == null
                            || comp.PlanAccumulator <= 0f
                            || !PlanStillValid(comp);

            if (needsPlan)
            {
                comp.NeedsReplan = false;
                comp.PlanAccumulator = MathF.Max(comp.PlanCooldown, 0.05f);
                TryPlan(uid, comp);
            }

            if (comp.Plan == null)
                continue;

            TickAction(uid, comp, frameTime);
        }
    }

    #region Sensors / working memory

    private void UpdateSensors(EntityUid uid, AIPlannerComponent comp)
    {
        comp.WorldState.Set(AIKeys.Dodged, false);
        comp.WorldState.Set(AIKeys.AtAmbushNode, false);
        comp.WorldState.Set(AIKeys.Inspected, false);
        comp.WorldState.Set(AIKeys.PatrolActive, false);
        comp.WorldState.Set(AIKeys.Suppressed, false);
        comp.WorldState.Set(AIKeys.TargetFlushed, false);
        comp.WorldState.Set(AIKeys.TargetSearched, false);
        comp.WorldState.Set(AIKeys.SafeFromExplosion, false);

        comp.HasTarget = false;
        comp.TargetVisible = false;
        comp.TargetAimingAtMe = false;
        comp.TargetDistance = float.MaxValue;

        var xform = Transform(uid);
        var transform = EntityManager.System<SharedTransformSystem>();
        var ownerWorld = transform.GetWorldPosition(xform);

        EntityUid seen = EntityUid.Invalid;
        var seenDistance = float.MaxValue;

        foreach (var hostile in _faction.GetNearbyHostiles(uid, comp.VisionRange))
        {
            if (hostile == uid || _mobState.IsDead(hostile))
                continue;

            var hostileXform = Transform(hostile);

            if (hostileXform.MapID != xform.MapID)
                continue;

            if (!xform.Coordinates.TryDistance(EntityManager, hostileXform.Coordinates, out var distance))
                continue;

            if (distance >= seenDistance)
                continue;

            if (!_interaction.InRangeUnobstructed(
                    uid,
                    hostile,
                    distance + 0.1f,
                    CollisionGroup.Opaque,
                    predicate: blocker => blocker == hostile
                                          || blocker == uid
                                          || _mobState.IsDead(blocker)
                                          || NPCLineOfSight.IsSeeThrough(blocker, EntityManager)))
            {
                continue;
            }

            seen = hostile;
            seenDistance = distance;
        }

        if (seen != EntityUid.Invalid)
        {
            comp.Target = seen;
            comp.MemoryTarget = seen;
            comp.HasTarget = true;
            comp.TargetVisible = true;
            comp.TargetDistance = seenDistance;
            comp.LastSeenTime = _timing.CurTime;
            comp.LastSeenAge = 0f;
            comp.LastKnownTargetPosition = transform.GetWorldPosition(Transform(seen));
            comp.TargetAimingAtMe = HasGunOut(seen);
        }
        else
        {
            var age = _timing.CurTime - comp.LastSeenTime;
            comp.LastSeenAge = (float) age.TotalSeconds;

            var memoryValid = comp.MemoryTarget.IsValid()
                              && !TerminatingOrDeleted(comp.MemoryTarget)
                              && EntityManager.EntityExists(comp.MemoryTarget)
                              && !_mobState.IsDead(comp.MemoryTarget)
                              && age <= TimeSpan.FromSeconds(comp.TargetMemoryDuration);

            if (memoryValid)
            {
                comp.Target = comp.MemoryTarget;
                comp.HasTarget = true;
                comp.TargetVisible = false;
                comp.TargetDistance = Vector2.Distance(ownerWorld, comp.LastKnownTargetPosition);
            }
            else
            {
                comp.Target = EntityUid.Invalid;
                comp.MemoryTarget = EntityUid.Invalid;
                comp.LastSeenAge = 0f;
            }
        }

        comp.TargetInMemory = comp.HasTarget && !comp.TargetVisible;

        comp.TargetBehindCover = comp.TargetInMemory
                                 && comp.LastSeenAge <= comp.GrenadeCoverWindow
                                 && comp.TargetDistance <= comp.GrenadeThrowRange;

        UpdateExplosiveDanger(uid, comp, ownerWorld, xform.MapID);

        if (TryComp<DamageableComponent>(uid, out var damage))
        {
            if (damage.TotalDamage > comp.LastDamage)
            {
                comp.UnderFire = true;
                comp.CoverCompromisedUntil = _timing.CurTime + TimeSpan.FromSeconds(comp.CoverCompromisedWindow);
            }
            else
            {
                comp.UnderFire = false;
            }

            comp.LastDamage = damage.TotalDamage;
            comp.LowHealth = _mobState.IsCritical(uid);
        }

        comp.CoverCompromised = _timing.CurTime < comp.CoverCompromisedUntil || comp.LowHealth;

        comp.InCover = comp.HasTarget && !comp.TargetVisible;

        comp.WeaponLoaded = HasLoadedGun(uid);

        var activeItem = _hands.GetActiveItem(uid);

        if (activeItem is { } held)
        {
            comp.WeaponEquipped = HasComp<GunComponent>(held);
            comp.WeaponWielded = comp.WeaponEquipped &&
                                 (!TryComp<WieldableComponent>(held, out var wieldable) || wieldable.Wielded);
            comp.MeleeArmed = HasComp<MeleeWeaponComponent>(held) && !HasComp<GunComponent>(held);
        }
        else
        {
            comp.WeaponEquipped = false;
            comp.WeaponWielded = false;
            comp.MeleeArmed = false;
        }

        comp.GrenadeAvailable = comp.HasGrenade && AIEquipment.TryFindGrenade(uid, EntityManager, out _);

        comp.InRange = comp.HasTarget && comp.TargetDistance <= comp.RangedRange;
        comp.InMeleeZone = comp.HasTarget && comp.TargetDistance <= comp.MeleeZoneRadius;

        comp.WorldState.Set(AIKeys.WeaponLoaded, comp.WeaponLoaded);
        comp.WorldState.Set(AIKeys.WeaponEquipped, comp.WeaponEquipped);
        comp.WorldState.Set(AIKeys.WeaponWielded, comp.WeaponWielded);
        comp.WorldState.Set(AIKeys.MeleeArmed, comp.MeleeArmed);
        comp.WorldState.Set(AIKeys.GrenadeAvailable, comp.GrenadeAvailable);
        comp.WorldState.Set(AIKeys.InRange, comp.InRange);
        comp.WorldState.Set(AIKeys.InMeleeZone, comp.InMeleeZone);
        comp.WorldState.Set(AIKeys.TargetVisible, comp.TargetVisible);
        comp.WorldState.Set(AIKeys.TargetDead, !comp.HasTarget);
        comp.WorldState.Set(AIKeys.AtCover, comp.InCover);
    }

    private void UpdateExplosiveDanger(EntityUid uid, AIPlannerComponent comp, Vector2 ownerWorld, MapId mapId)
    {
        comp.GrenadeDanger = false;

        var transform = EntityManager.System<SharedTransformSystem>();
        var nearest = float.MaxValue;

        var query = EntityQueryEnumerator<ActiveTimerTriggerComponent, TransformComponent>();

        while (query.MoveNext(out var grenadeUid, out var timer, out var grenadeXform))
        {
            if (timer.TimeRemaining <= 0f)
                continue;

            if (grenadeXform.MapID != mapId)
                continue;

            if (grenadeXform.ParentUid == uid)
                continue;

            var position = transform.GetWorldPosition(grenadeXform);
            var distance = Vector2.Distance(ownerWorld, position);

            if (distance > comp.GrenadeDangerRadius || distance >= nearest)
                continue;

            nearest = distance;
            comp.GrenadeDanger = true;
            comp.GrenadeDangerPosition = position;
        }
    }

    private bool HasLoadedGun(EntityUid uid)
    {
        if (!AIEquipment.TryFindGun(uid, EntityManager, out var gunUid))
            return false;

        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(gunUid, ref ev);
        return ev.Count > 0;
    }

    private bool HasGunOut(EntityUid target)
    {
        return _hands.TryGetActiveItem(target, out var held) &&
               HasComp<GunComponent>(held);
    }

    #endregion

    #region Planning

    private void TryPlan(EntityUid uid, AIPlannerComponent comp)
    {
        AIGoal? bestGoal = null;
        List<AIAction>? bestPlan = null;
        var bestPriority = float.MinValue;

        foreach (var goal in comp.Goals)
        {
            if (!goal.IsRelevant(uid, comp, EntityManager))
                continue;

            var priority = goal.GetPriority(uid, comp, EntityManager);
            if (priority <= 0f || priority < bestPriority)
                continue;

            var plan = AIPlanner.Plan(goal, comp, uid, EntityManager);
            if (plan == null || plan.Count == 0)
                continue;

            if (priority <= bestPriority)
                continue;

            bestPriority = priority;
            bestGoal = goal;
            bestPlan = plan;
        }

        if (bestGoal == null || bestPlan == null)
            return;

        if (comp.CurrentGoal == bestGoal && SameRemainingPlan(comp, bestPlan))
            return;

        ShutdownCurrent(uid, comp);
        comp.CurrentGoal = bestGoal;
        comp.Plan = bestPlan;
        comp.PlanIndex = 0;
        comp.ActionStarted = false;
    }

    private static bool SameRemainingPlan(AIPlannerComponent comp, List<AIAction> fresh)
    {
        if (comp.Plan == null || comp.PlanIndex >= comp.Plan.Count)
            return false;

        if (comp.Plan.Count - comp.PlanIndex != fresh.Count)
            return false;

        for (var i = 0; i < fresh.Count; i++)
        {
            if (!ReferenceEquals(comp.Plan[comp.PlanIndex + i], fresh[i]))
                return false;
        }

        return true;
    }

    private static bool PlanStillValid(AIPlannerComponent comp)
    {
        if (comp.Plan == null)
            return true;

        var state = comp.WorldState.Clone();

        for (var i = comp.PlanIndex; i < comp.Plan.Count; i++)
        {
            var action = comp.Plan[i];

            if (!action.PreconditionsMet(state))
                return false;

            state = state.Apply(action);
        }

        return true;
    }

    #endregion

    #region Execution

    private void TickAction(EntityUid uid, AIPlannerComponent comp, float frameTime)
    {
        if (comp.Plan == null || comp.PlanIndex >= comp.Plan.Count)
        {
            comp.Plan = null;
            return;
        }

        var action = comp.Plan[comp.PlanIndex];

        if (!action.IsProcedurallyValid(uid, comp, EntityManager))
        {
            if (comp.ActionStarted)
                action.Shutdown(uid, comp, EntityManager);

            comp.ActionStarted = false;
            comp.Plan = null;
            comp.NeedsReplan = true;
            return;
        }

        if (!comp.ActionStarted)
        {
            action.Start(uid, comp, EntityManager);
            comp.ActionStarted = true;
        }

        var status = action.Update(uid, comp, EntityManager, frameTime);

        switch (status)
        {
            case AIActionStatus.Continuing:
                return;

            case AIActionStatus.Finished:
            {
                action.Shutdown(uid, comp, EntityManager);
                comp.ActionStarted = false;

                foreach (var (key, value) in action.Effects)
                    comp.WorldState.Set(key, value);

                comp.PlanIndex++;

                if (comp.PlanIndex >= comp.Plan.Count)
                {
                    comp.Plan = null;
                    comp.NeedsReplan = true;
                }

                return;
            }

            case AIActionStatus.Failed:
            default:
            {
                action.Shutdown(uid, comp, EntityManager);
                comp.ActionStarted = false;
                comp.Plan = null;
                comp.NeedsReplan = true;
                return;
            }
        }
    }

    private void ShutdownCurrent(EntityUid uid, AIPlannerComponent comp)
    {
        if (comp.Plan != null && comp.ActionStarted && comp.PlanIndex < comp.Plan.Count)
            comp.Plan[comp.PlanIndex].Shutdown(uid, comp, EntityManager);

        comp.ActionStarted = false;
        comp.Plan = null;
        comp.PlanIndex = 0;
    }

    #endregion
}
