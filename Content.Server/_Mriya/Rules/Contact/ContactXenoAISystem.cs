using Content.Server.Ghost.Roles.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Doors.Components;
using Robust.Shared.Physics.Components;
using Content.Shared.DoAfter;
using Content.Shared._RMC14.Doors;
using Content.Shared.Power.EntitySystems;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Shared.Item;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact AI xenos. HTN does movement and melee; this system feeds it:
/// - sight / hearing / hunt destinations,
/// - smash orders when blocked (progress-based, catches circling around barricades),
/// - hold orders for ranged castes and wind-up abilities,
/// and fires caste abilities (pattern from Wizden's NPCUseActionOnTargetSystem).
/// </summary>
public sealed class ContactXenoAISystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    public const string NoiseActiveKey = "MRContactNoiseActive";
    public const string HuntActiveKey = "MRContactHuntActive";

    // Blackboard keys read by MRContactXenoCompound
    public const string HoldKey = "MRContactHold";
    public const string SmashTargetKey = "MRContactSmashTarget";
    public const string SmashCoordinatesKey = "MRContactSmashCoordinates";
    public const string NoiseKey = "MRContactNoise";
    public const string HuntKey = "MRContactHunt";
    public const string SmashActiveKey = "MRContactSmashActive";
    public const string DefenseActiveKey = "MRContactDefenseActive";
    public const string DefenseTargetKey = "MRContactDefenseTarget";
    public const string DefenseCoordinatesKey = "MRContactDefenseCoordinates";

    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(0.5);

    /// <summary>A gun counts as "heard" for this long after its last shot.</summary>
    private static readonly TimeSpan LoudWindow = TimeSpan.FromSeconds(1.5);

    /// <summary>How far around itself a blocked xeno looks for something to smash.</summary>
    private const float SmashSearchRange = 2f;

    private readonly HashSet<EntityUid> _obstacles = new();
    private TimeSpan _nextUpdate;

    public const string MeleeActiveKey = "MRContactMeleeActive";

    private readonly record struct MarineInfo(EntityUid Uid, MapCoordinates Pos, EntityCoordinates Coords, EntityUid? Grid, bool Loud);

    private readonly List<MarineInfo> _marines = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ContactXenoAIComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ContactXenoAIComponent, EntityTerminatingEvent>(OnTerminating);
    }

    /// <summary>
    /// A door crushing this xeno keeps it in CurrentlyCrushing until it opens again;
    /// if the xeno is deleted meanwhile (despawn, gib), the door networks a dead entity.
    /// </summary>
    private void OnTerminating(Entity<ContactXenoAIComponent> ent, ref EntityTerminatingEvent args)
    {
        var doors = EntityQueryEnumerator<DoorComponent>();
        while (doors.MoveNext(out var uid, out var door))
        {
            if (door.CurrentlyCrushing.Remove(ent))
                Dirty(uid, door);
        }
    }

    private void OnMapInit(Entity<ContactXenoAIComponent> ent, ref MapInitEvent args)
    {
        // AI xenos aren't ghost roles and shouldn't spam hive death announcements
        RemCompDeferred<GhostTakeoverAvailableComponent>(ent);
        RemCompDeferred<XenoAnnounceDeathComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;

        CollectMarines(now);

        var query = EntityQueryEnumerator<ContactXenoAIComponent, HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var ai, out var htn, out var xform))
        {
            if (_mobState.IsIncapacitated(uid))
                continue;

            // doing a do-after (prying a door / winding up an ability): don't touch the plan
            if (HasComp<ActiveDoAfterComponent>(uid))
            {
                // wind-ups start from a hold: keep standing until it's done, or the move cancels it
                if (htn.Blackboard.TryGetValue<float>(HoldKey, out _, EntityManager))
                    htn.Blackboard.SetValue(HoldKey, 1f);

                continue;
            }

            var pos = _transform.GetMapCoordinates(uid, xform);
            var destination = UpdatePerception(uid, ai, htn, pos, xform);

            UpdateHoldRange(uid, ai, htn);
            UpdateSmash(uid, ai, htn, pos, destination, now);
            TryUseAbility(uid, ai, htn, pos, now);
        }
    }

    #region Perception

    private void CollectMarines(TimeSpan now)
    {
        _marines.Clear();
        var query = EntityQueryEnumerator<MarineComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (_mobState.IsDead(uid, mobState))
                continue;

            _marines.Add(new MarineInfo(uid, _transform.GetMapCoordinates(uid, xform), xform.Coordinates, xform.GridUid, IsLoud(uid, now)));
        }
    }

    /// <summary>
    /// Shot a held gun recently. NextFire is set right after a shot, so a recent NextFire means a recent shot.
    /// </summary>
    private bool IsLoud(EntityUid marine, TimeSpan now)
    {
        foreach (var held in _hands.EnumerateHeld(marine))
        {
            if (TryComp(held, out GunComponent? gun) &&
                gun.NextFire != TimeSpan.Zero &&
                gun.NextFire >= now - LoudWindow)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Updates the seen target and the melee/noise/hunt blackboard keys.
    /// Returns where the xeno is currently trying to go (seen marine > defense > heard > hunt).
    /// </summary>
    private MapCoordinates? UpdatePerception(EntityUid uid, ContactXenoAIComponent ai, HTNComponent htn, MapCoordinates pos, TransformComponent xform)
    {
        MarineInfo? seen = null;
        var seenDist = float.MaxValue;
        MarineInfo? heard = null;
        var heardDist = float.MaxValue;
        MarineInfo? hunt = null;
        var huntDist = float.MaxValue;

        foreach (var marine in _marines)
        {
            if (marine.Pos.MapId != pos.MapId)
                continue;

            var dist = (marine.Pos.Position - pos.Position).Length();

            if (dist <= ai.VisionRange && dist < seenDist &&
                _interaction.InRangeUnobstructed(uid, marine.Uid, ai.VisionRange))
            {
                seen = marine;
                seenDist = dist;
            }

            if (marine.Loud && dist <= ai.HearingRange && dist < heardDist)
            {
                heard = marine;
                heardDist = dist;
            }

            if (ai.HuntRange > 0 && dist <= ai.HuntRange && dist < huntDist)
            {
                hunt = marine;
                huntDist = dist;
            }
        }

        ai.Target = seen?.Uid;
        ai.TargetDistance = seenDist;

        // Coordinates are only ever SET (MoveToOperator reads them at startup);
        // the branches are switched with the *Active flags, which only preconditions read.
        var blackboard = htn.Blackboard;

        // pathfinding is per grid: melee only targets marines on our grid,
        // marines inside a dropship are approached through the noise/hunt branches.
        var meleeMarine = seen is { } s && s.Grid == xform.GridUid;
        if (meleeMarine)
            blackboard.SetValue(MeleeActiveKey, true);
        else
            blackboard.Remove<bool>(MeleeActiveKey);

        // deployed defenses (sentry, M2C, ML66D, turrets) in sight come first, even over marines
        MapCoordinates? defensePos = null;
        if (FindDefense(uid, ai, xform) is { } defense)
        {
            defensePos = _transform.GetMapCoordinates(defense);
            blackboard.SetValue(DefenseTargetKey, defense);
            blackboard.SetValue(DefenseCoordinatesKey, Transform(defense).Coordinates);
            blackboard.SetValue(DefenseActiveKey, true);
        }
        else
        {
            blackboard.Remove<bool>(DefenseActiveKey);
        }

        if (heard is { } noise)
        {
            blackboard.SetValue(NoiseKey, OnOwnGrid(xform, noise));
            blackboard.SetValue(NoiseActiveKey, true);
        }
        else
        {
            blackboard.Remove<bool>(NoiseActiveKey);
        }

        if (hunt is { } huntTarget)
        {
            blackboard.SetValue(HuntKey, OnOwnGrid(xform, huntTarget));
            blackboard.SetValue(HuntActiveKey, true);
        }
        else
        {
            blackboard.Remove<bool>(HuntActiveKey);
        }

        return defensePos ?? seen?.Pos ?? heard?.Pos ?? hunt?.Pos;
    }

    /// <summary>
    /// Marine on another grid (inside a dropship): the same spot, but on our grid so the pathfinder can route to it.
    /// </summary>
    private EntityCoordinates OnOwnGrid(TransformComponent xform, MarineInfo marine)
    {
        if (xform.GridUid is not { } grid || marine.Grid == grid)
            return marine.Coords;

        return _transform.ToCoordinates(grid, marine.Pos);
    }

    /// <summary>
    /// Nearest working defense in sight on our grid.
    /// Deployed = anchored, or not an item at all (pre-placed turrets); folded / dropped ones are skipped, and so are broken ones.
    /// </summary>
    private EntityUid? FindDefense(EntityUid uid, ContactXenoAIComponent ai, TransformComponent xform)
    {
        _obstacles.Clear();
        _lookup.GetEntitiesInRange(uid, ai.VisionRange, _obstacles, LookupFlags.Static | LookupFlags.Dynamic);

        var pos = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var ent in _obstacles)
        {
            if (!_whitelist.IsWhitelistPass(ai.DefenseWhitelist, ent))
                continue;

            var entXform = Transform(ent);
            if (entXform.GridUid != xform.GridUid)
                continue;

            if (!entXform.Anchored && HasComp<ItemComponent>(ent))
                continue;

            if (IsBroken(ent))
                continue;

            var dist = (_transform.GetWorldPosition(entXform) - pos).Length();
            if (dist >= bestDist || !_interaction.InRangeUnobstructed(uid, ent, ai.VisionRange))
                continue;

            best = ent;
            bestDist = dist;
        }

        return best;
    }

    /// <summary>
    /// Damage at or past any Destructible threshold = broken (an M2C turns into its broken item instead of being deleted).
    /// Repairing it below the threshold makes it a target again.
    /// </summary>
    private bool IsBroken(EntityUid ent)
    {
        if (!TryComp(ent, out DestructibleComponent? destructible) ||
            !TryComp(ent, out DamageableComponent? damageable))
        {
            return false;
        }

        foreach (var threshold in destructible.Thresholds)
        {
            if (threshold.Trigger is DamageTrigger trigger && damageable.TotalDamage >= trigger.Damage)
                return true;
        }

        return false;
    }

    #endregion

    #region Hold

    /// <summary>
    /// Ranged castes stand still while a target is in their band.
    /// </summary>
    private void UpdateHoldRange(EntityUid uid, ContactXenoAIComponent ai, HTNComponent htn)
    {
        if (ai.HoldRange is not { } range || ai.Target == null)
            return;

        if (ai.TargetDistance > range || ai.TargetDistance <= ai.HoldMinRange)
            return;

        // nothing to shoot right now: close in instead of standing there
        if (!HasUsableAbility(uid, ai))
            return;

        Hold(uid, htn, 1f);
    }

    private bool HasUsableAbility(EntityUid uid, ContactXenoAIComponent ai)
    {
        foreach (var rule in ai.Abilities)
        {
            if (ai.TargetDistance < rule.MinRange || ai.TargetDistance > rule.MaxRange)
                continue;

            if (TryGetAction(uid, ai, rule.Action, out var action) && _actions.ValidAction(action))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Stand still for at least this long. Drops the current move so HTN replans into the hold branch right away.
    /// </summary>
    private void Hold(EntityUid uid, HTNComponent htn, float seconds)
    {
        var blackboard = htn.Blackboard;
        var holding = blackboard.TryGetValue<float>(HoldKey, out var current, EntityManager);
        if (!holding || current < seconds)
            blackboard.SetValue(HoldKey, seconds);

        if (!holding)
            _steering.Unregister(uid);
    }

    #endregion

    #region Smash

    private void UpdateSmash(
        EntityUid uid,
        ContactXenoAIComponent ai,
        HTNComponent htn,
        MapCoordinates pos,
        MapCoordinates? destination,
        TimeSpan now)
    {
        var blackboard = htn.Blackboard;

        // gave up on a door earlier: prying back on
        if (ai.NoPryUntil is { } noPry && now >= noPry)
        {
            ai.NoPryUntil = null;
            SetDoorNav(blackboard, true);;
        }

        // already smashing something
        if (ai.SmashTarget is { } smashing)
        {
            if (!Exists(smashing) || now >= ai.SmashUntil)
                ClearSmash(ai, blackboard);

            return;
        }

        if (destination is not { } dest || dest.MapId != pos.MapId)
        {
            ResetProgress(ai);
            return;
        }

        // new destination (different marine, noise moved...) = start measuring again
        if (ai.LastDestination is not { } lastDest ||
            lastDest.MapId != dest.MapId ||
            (lastDest.Position - dest.Position).Length() > 2f)
        {
            ResetProgress(ai);
            ai.LastDestination = dest;
        }

        // powered pod locks / shutters can't be pried: path around them
        if (ai.NoPryUntil == null && HasClosedDoorAhead(uid, pos, dest, unpryableOnly: true))
        {
            ai.NoPryUntil = now + ai.NoPryTime;
            SetDoorNav(blackboard, false);
            _steering.Unregister(uid);
            return;
        }

        var dist = (dest.Position - pos.Position).Length();
        if (dist < 1.5f)
        {
            ResetProgress(ai);
            return;
        }

        if (dist < ai.BestDistance - 0.5f)
        {
            ai.BestDistance = dist;
            ai.NoProgress = TimeSpan.Zero;
            return;
        }

        ai.NoProgress += UpdateEvery;
        if (ai.NoProgress < ai.StuckTime)
            return;

        ResetProgress(ai);

        // a closed door ahead: steering pries it, don't smash windows / frames around it instead
        if (HasClosedDoorAhead(uid, pos, dest))
            return;

        if (FindObstacle(uid, ai, pos, dest) is not { } obstacle)
            return;

        ai.SmashTarget = obstacle;
        ai.SmashUntil = now + ai.SmashTimeout;
        blackboard.SetValue(SmashTargetKey, obstacle);
        blackboard.SetValue(SmashCoordinatesKey, Transform(obstacle).Coordinates);
        blackboard.SetValue(SmashActiveKey, true);

        // drop the current move so HTN replans into the smash branch right away
        _steering.Unregister(uid);
    }

    /// <summary>
    /// Nearest whitelisted thing that actually blocks movement (hard collision), near the xeno, toward the destination.
    /// Wall mounts (intercoms, screens, posters) and open doors don't collide, so they're never picked. Doors are pried, not smashed.
    /// </summary>
    private EntityUid? FindObstacle(EntityUid uid, ContactXenoAIComponent ai, MapCoordinates pos, MapCoordinates dest)
    {
        var toDest = (dest.Position - pos.Position).Normalized();

        _obstacles.Clear();
        _lookup.GetEntitiesInRange(uid, SmashSearchRange, _obstacles, LookupFlags.Static);

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var ent in _obstacles)
        {
            if (ent == uid ||
                HasComp<DoorComponent>(ent) ||
                !TryComp(ent, out PhysicsComponent? body) || !body.CanCollide || !body.Hard ||
                !HasComp<DamageableComponent>(ent) ||
                !_whitelist.IsWhitelistPass(ai.SmashWhitelist, ent))
            {
                continue;
            }

            var offset = _transform.GetMapCoordinates(ent).Position - pos.Position;
            var dist = offset.Length();

            // must be roughly between us and where we're going
            if (dist > 0.01f && Vector2Dot(offset / dist, toDest) < 0.2f)
                continue;

            if (dist < bestDist)
            {
                best = ent;
                bestDist = dist;
            }
        }

        return best;
    }

    private bool HasClosedDoorAhead(EntityUid uid, MapCoordinates pos, MapCoordinates dest, bool unpryableOnly = false)
    {
        var toDest = (dest.Position - pos.Position).Normalized();

        _obstacles.Clear();
        _lookup.GetEntitiesInRange(uid, unpryableOnly ? 1.5f : SmashSearchRange, _obstacles, LookupFlags.Static);

        EntityUid? nearest = null;
        var nearestDist = float.MaxValue;
        foreach (var ent in _obstacles)
        {
            if (!TryComp(ent, out DoorComponent? door) || door.State is DoorState.Open or DoorState.Opening)
                continue;

            var offset = _transform.GetMapCoordinates(ent).Position - pos.Position;
            var dist = offset.Length();
            if (dist > 0.01f && Vector2Dot(offset / dist, toDest) < 0.2f)
                continue;

            if (dist < nearestDist)
            {
                nearest = ent;
                nearestDist = dist;
            }
        }

        if (nearest is not { } found)
            return false;

        // pod lock check: only if the door it's actually walking into is a powered pod lock / shutter,
        // not a normal door that happens to be next to one
        return !unpryableOnly || HasComp<RMCPodDoorComponent>(found) && _power.IsPowered(found);
    }

    private static void ResetProgress(ContactXenoAIComponent ai)
    {
        ai.BestDistance = float.MaxValue;
        ai.NoProgress = TimeSpan.Zero;
    }

    private void ClearSmash(ContactXenoAIComponent ai, NPCBlackboard blackboard)
    {
        ai.SmashTarget = null;
        blackboard.Remove<bool>(SmashActiveKey);
    }

    /// <summary>
    /// With NavPry off but NavSmash on, the pathfinder routes through the door and steering smashes it,
    /// so both go off together while avoiding a powered pod lock.
    /// </summary>
    private void SetDoorNav(NPCBlackboard blackboard, bool enabled)
    {
        blackboard.SetValue(NPCBlackboard.NavPry, enabled);
        blackboard.SetValue(NPCBlackboard.NavSmash, enabled);
    }

    private static float Vector2Dot(System.Numerics.Vector2 a, System.Numerics.Vector2 b)
    {
        return System.Numerics.Vector2.Dot(a, b);
    }

    #endregion

    #region Abilities

    private void TryUseAbility(EntityUid uid, ContactXenoAIComponent ai, HTNComponent htn, MapCoordinates selfPos, TimeSpan now)
    {
        if (ai.Abilities.Count == 0 || now < ai.NextAbility)
            return;

        if (ai.Target is not { } target || !Exists(target) || _mobState.IsDead(target))
            return;

        var targetPos = _transform.GetMapCoordinates(target);
        if (selfPos.MapId != targetPos.MapId)
            return;

        var dist = (targetPos.Position - selfPos.Position).Length();
        var health = GetHealthFraction(uid);

        foreach (var rule in ai.Abilities)
        {
            if (dist < rule.MinRange || dist > rule.MaxRange)
                continue;

            if (rule.MaxHealth is { } maxHealth && health > maxHealth)
                continue;

            if (rule.TargetHolding != null && !Holds(target, rule.TargetHolding))
                continue;

            if (rule.TargetNotHolding != null && Holds(target, rule.TargetNotHolding))
                continue;

            if (!TryGetAction(uid, ai, rule.Action, out var action))
                continue;

            if (rule.Toggled is { } toggled && action.Comp.Toggled != toggled)
                continue;

            if (!_actions.ValidAction(action))
                continue;

            if (rule.Chance < 1f && !_random.Prob(rule.Chance))
                continue;

            if (_actions.GetEvent(action) is not { } ev)
                continue;

            switch (ev)
            {
                case EntityTargetActionEvent entityEv:
                    entityEv.Target = target;
                    break;
                case WorldTargetActionEvent worldEv:
                    worldEv.Target = rule.Aim == ContactXenoAim.AwayFromTarget
                        ? AwayCoordinates(selfPos, targetPos, rule.AwayDistance)
                        : Transform(target).Coordinates;
                    worldEv.Entity = rule.Aim == ContactXenoAim.Target ? target : null;
                    break;
            }

            // stop first so movement doesn't cancel the wind-up
            if (rule.Hold > 0)
                Hold(uid, htn, rule.Hold);

            // NPCs are server-side, no prediction
            _actions.PerformAction(uid, action, ev, predicted: false);
            // wind-up abilities: no next ability until the hold is over, or it restarts itself
            var delay = Math.Max(ai.AbilityDelay.TotalSeconds, rule.Hold);
            ai.NextAbility = now + TimeSpan.FromSeconds(delay);
            return;
        }
    }

    private bool TryGetAction(EntityUid uid, ContactXenoAIComponent ai, EntProtoId proto, out Entity<ActionComponent> action)
    {
        action = default;

        if (ai.ActionCache.TryGetValue(proto.Id, out var cached) &&
            TryComp(cached, out ActionComponent? cachedComp))
        {
            action = (cached, cachedComp);
            return true;
        }

        foreach (var candidate in _actions.GetActions(uid))
        {
            if (MetaData(candidate.Owner).EntityPrototype?.ID != proto.Id)
                continue;

            ai.ActionCache[proto.Id] = candidate.Owner;
            action = candidate;
            return true;
        }

        return false;
    }

    private EntityCoordinates AwayCoordinates(MapCoordinates self, MapCoordinates target, float distance)
    {
        var dir = self.Position - target.Position;
        if (dir.LengthSquared() < 0.01f)
            dir = _random.NextAngle().ToVec();

        var away = new MapCoordinates(self.Position + dir.Normalized() * distance, self.MapId);
        return _transform.ToCoordinates(away);
    }

    private bool Holds(EntityUid target, EntityWhitelist whitelist)
    {
        foreach (var held in _hands.EnumerateHeld(target))
        {
            if (_whitelist.IsWhitelistPass(whitelist, held))
                return true;
        }

        return false;
    }

    private float GetHealthFraction(EntityUid uid)
    {
        if (!TryComp(uid, out DamageableComponent? damageable) ||
            !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var crit) ||
            crit.Value <= 0)
        {
            return 1f;
        }

        return Math.Clamp(1f - damageable.TotalDamage.Float() / crit.Value.Float(), 0f, 1f);
    }

    #endregion
}