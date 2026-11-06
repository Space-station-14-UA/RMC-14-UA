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

    // Blackboard keys read by MRContactXenoCompound
    public const string HoldKey = "MRContactHold";
    public const string SmashTargetKey = "MRContactSmashTarget";
    public const string SmashCoordinatesKey = "MRContactSmashCoordinates";
    public const string NoiseKey = "MRContactNoise";
    public const string HuntKey = "MRContactHunt";

    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(0.5);

    /// <summary>A gun counts as "heard" for this long after its last shot.</summary>
    private static readonly TimeSpan LoudWindow = TimeSpan.FromSeconds(1.5);

    /// <summary>How far around itself a blocked xeno looks for something to smash.</summary>
    private const float SmashSearchRange = 2f;

    private readonly List<(EntityUid Uid, MapCoordinates Pos, EntityCoordinates Coords, bool Loud)> _marines = new();
    private readonly HashSet<EntityUid> _obstacles = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<ContactXenoAIComponent, MapInitEvent>(OnMapInit);
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

            var pos = _transform.GetMapCoordinates(uid, xform);
            var destination = UpdatePerception(uid, ai, htn, pos);

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

            _marines.Add((uid, _transform.GetMapCoordinates(uid, xform), xform.Coordinates, IsLoud(uid, now)));
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
    /// Updates the seen target and the noise/hunt blackboard keys.
    /// Returns where the xeno is currently trying to go (seen > heard > hunt).
    /// </summary>
    private MapCoordinates? UpdatePerception(EntityUid uid, ContactXenoAIComponent ai, HTNComponent htn, MapCoordinates pos)
    {
        EntityUid? seen = null;
        var seenDist = float.MaxValue;
        (EntityCoordinates Coords, MapCoordinates Pos)? heard = null;
        var heardDist = float.MaxValue;
        (EntityCoordinates Coords, MapCoordinates Pos)? hunt = null;
        var huntDist = float.MaxValue;

        foreach (var marine in _marines)
        {
            if (marine.Pos.MapId != pos.MapId)
                continue;

            var dist = (marine.Pos.Position - pos.Position).Length();

            if (dist <= ai.VisionRange && dist < seenDist &&
                _interaction.InRangeUnobstructed(uid, marine.Uid, ai.VisionRange))
            {
                seen = marine.Uid;
                seenDist = dist;
            }

            if (marine.Loud && dist <= ai.HearingRange && dist < heardDist)
            {
                heard = (marine.Coords, marine.Pos);
                heardDist = dist;
            }

            if (ai.HuntRange > 0 && dist <= ai.HuntRange && dist < huntDist)
            {
                hunt = (marine.Coords, marine.Pos);
                huntDist = dist;
            }
        }

        ai.Target = seen;
        ai.TargetDistance = seenDist;

        var blackboard = htn.Blackboard;
        if (heard is { } noise)
            blackboard.SetValue(NoiseKey, noise.Coords);
        else
            blackboard.Remove<EntityCoordinates>(NoiseKey);

        if (hunt is { } huntTarget)
            blackboard.SetValue(HuntKey, huntTarget.Coords);
        else
            blackboard.Remove<EntityCoordinates>(HuntKey);

        if (seen is { } seenUid)
            return _transform.GetMapCoordinates(seenUid);

        return heard?.Pos ?? hunt?.Pos;
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

        if (ai.TargetDistance <= range && ai.TargetDistance > ai.HoldMinRange)
            Hold(uid, htn, 1f);
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

        if (FindObstacle(uid, ai, pos, dest) is not { } obstacle)
            return;

        ai.SmashTarget = obstacle;
        ai.SmashUntil = now + ai.SmashTimeout;
        blackboard.SetValue(SmashTargetKey, obstacle);
        blackboard.SetValue(SmashCoordinatesKey, Transform(obstacle).Coordinates);

        // drop the current move so HTN replans into the smash branch right away
        _steering.Unregister(uid);
    }

    private static void ResetProgress(ContactXenoAIComponent ai)
    {
        ai.BestDistance = float.MaxValue;
        ai.NoProgress = TimeSpan.Zero;
    }

    private void ClearSmash(ContactXenoAIComponent ai, NPCBlackboard blackboard)
    {
        ai.SmashTarget = null;
        blackboard.Remove<EntityUid>(SmashTargetKey);
        blackboard.Remove<EntityCoordinates>(SmashCoordinatesKey);
    }

    /// <summary>
    /// Nearest whitelisted, damageable, anchored thing near the xeno that lies toward the destination.
    /// </summary>
    private EntityUid? FindObstacle(EntityUid uid, ContactXenoAIComponent ai, MapCoordinates pos, MapCoordinates dest)
    {
        var toDest = (dest.Position - pos.Position).Normalized();

        _obstacles.Clear();
        _lookup.GetEntitiesInRange(pos, SmashSearchRange, _obstacles, LookupFlags.Static);

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        foreach (var ent in _obstacles)
        {
            if (ent == uid ||
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
            ai.NextAbility = now + ai.AbilityDelay;
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