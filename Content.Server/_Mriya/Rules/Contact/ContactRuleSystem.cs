using Content.Server._RMC14.Xenonids.Hive;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Spawners;
using Content.Shared.Coordinates;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Server._RMC14.Marines;
using Content.Shared._RMC14.Marines;
using Content.Shared.Mobs.Components;
using Content.Shared._RMC14.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Player;
using System.Linq;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact: low-pop PvE mode. Runs alongside MRContactDistress, which handles planet, ship spawning and dropships.
/// This system owns the field timer, terminals and waves.
/// </summary>
public sealed class ContactRuleSystem : GameRuleSystem<ContactRuleComponent>
{
    [Dependency] private readonly XenoHiveSystem _hive = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private float? _savedPreflight;

    private readonly List<MapCoordinates> _playerPositions = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLanded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<ContactWaveMobComponent, GetMeleeDamageEvent>(OnWaveMobMeleeDamage);
    }

    private ContactRuleComponent? GetActiveRule()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var comp, out _))
        {
            return comp;
        }

        return null;
    }

    private EntityUid? GetDistressHive()
    {
        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out _, out var distress, out _))
        {
            if (Exists(distress.Hive))
                return distress.Hive;
        }

        return null;
    }

    private void OnDropshipLanded(ref DropshipLandedOnPlanetEvent ev)
    {
        var rule = GetActiveRule();
        if (rule == null || rule.Phase != ContactPhase.Staging)
            return;

        var time = Timing.CurTime;
        rule.Phase = ContactPhase.Deployed;
        rule.FieldEndsAt = time + rule.FieldStart;
        rule.NextWaveAt = time + rule.WaveInterval;
        rule.NextMarineCheck = time + rule.MarineCheckEvery;
        _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("mriya-contact-deployed",
            ("seconds", (int) rule.FieldStart.TotalSeconds)));
    }

    public bool TryActivateTerminal(out TimeSpan bonus)
    {
        bonus = default;
        var rule = GetActiveRule();
        if (rule == null || rule.Phase != ContactPhase.Deployed)
            return false;

        rule.FieldEndsAt += rule.TerminalFieldBonus;
        rule.NextWaveAt -= rule.TerminalWaveReduction;
        bonus = rule.TerminalFieldBonus;
        return true;
    }

    protected override void ActiveTick(EntityUid uid, ContactRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.Phase != ContactPhase.Deployed)
            return;

        var time = Timing.CurTime;
        if (time >= comp.FieldEndsAt)
        {
            EndContact(comp, ContactResult.FieldCollapsed);
            return;
        }

        if (time >= comp.NextMarineCheck)
        {
            comp.NextMarineCheck = time + comp.MarineCheckEvery;
            if (!AnyMarineAliveOnPlanet())
            {
                EndContact(comp, ContactResult.MarinesLost);
                return;
            }
        }

        if (comp.NextDespawnCheck == null || time >= comp.NextDespawnCheck)
        {
            comp.NextDespawnCheck = time + comp.DespawnCheckEvery;
            UpdateWaveMobDespawn(comp, time);
        }

        if (time >= comp.NextWaveAt)
            StartWave(comp, time);
    }

    private void StartWave(ContactRuleComponent comp, TimeSpan time)
    {
        comp.Wave++;
        comp.NextWaveAt = time + comp.WaveInterval;

        if (!_prototypes.TryIndex(comp.Waves, out var waves))
        {
            Log.Error($"Contact: no waves prototype {comp.Waves}");
            return;
        }

        var buff = GetBuff(waves, comp.Wave);
        if (buff != null && buff.Wave != comp.LastBuffWave)
        {
            comp.LastBuffWave = buff.Wave;
            AnnounceBuff(buff);
        }

        CollectPlayerPositions();
        // TODO Mriya: own spawn markers; reusing planet xeno spawn points for now
        var points = new List<EntityUid>();
        var pointQuery = AllEntityQuery<XenoSpawnPointComponent, TransformComponent>();
        while (pointQuery.MoveNext(out var point, out _, out var xform))
        {
            if (IsNearPlayer(_transform.GetMapCoordinates(point, xform), comp.SpawnRange))
                points.Add(point);
        }

        if (points.Count == 0)
        {
            Log.Warning($"Contact wave {comp.Wave}: no spawn points within {comp.SpawnRange} tiles of players");
            return;
        }

        var hive = GetDistressHive();

        for (var i = 0; i < waves.Spawns.Count; i++)
        {
            var entry = waves.Spawns[i];
            if (comp.Wave < entry.StartWave)
                continue;

            var rate = entry.Rate + entry.RatePerWave * (comp.Wave - entry.StartWave);
            var acc = comp.SpawnAccumulators.GetValueOrDefault(i) + rate;
            var count = (int) acc;
            comp.SpawnAccumulators[i] = acc - count;

            if (entry.MaxAlive >= 0)
                count = Math.Min(count, entry.MaxAlive - CountAliveWaveMobs(entry.Proto));

            for (var j = 0; j < count; j++)
            {
                SpawnWaveMob(entry.Proto, _random.Pick(points), hive, buff);
            }
        }

        _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("mriya-contact-wave-started", ("wave", comp.Wave)));
    }

    private void AnnounceBuff(ContactWaveBuffEntry buff)
    {
        var msg = buff.Announcement is { } custom
            ? Loc.GetString(custom)
            : Loc.GetString("mriya-contact-buff",
                ("health", (int) MathF.Round((buff.Health - 1f) * 100)),
                ("damage", (int) MathF.Round((buff.Damage - 1f) * 100)));

        _marineAnnounce.AnnounceARESStaging(default, msg);
    }

    private static ContactWaveBuffEntry? GetBuff(ContactWavesPrototype waves, int wave)
    {
        ContactWaveBuffEntry? best = null;
        foreach (var buff in waves.Buffs)
        {
            if (buff.Wave <= wave && (best == null || buff.Wave > best.Wave))
                best = buff;
        }

        return best;
    }

    private int CountAliveWaveMobs(EntProtoId proto)
    {
        var count = 0;
        var query = EntityQueryEnumerator<ContactWaveMobComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var mob, out var mobState))
        {
            if (mob.Proto == proto && !_mobState.IsDead(uid, mobState))
                count++;
        }

        return count;
    }

    private void SpawnWaveMob(EntProtoId proto, EntityUid point, EntityUid? hive, ContactWaveBuffEntry? buff)
    {
        var mob = SpawnAtPosition(proto, point.ToCoordinates());
        if (hive != null)
            _hive.SetHive(mob, hive.Value);

        var waveMob = EnsureComp<ContactWaveMobComponent>(mob);
        waveMob.Proto = proto;

        if (buff == null)
            return;

        waveMob.DamageMultiplier = buff.Damage;

        if (buff.Health != 1f && TryComp<MobThresholdsComponent>(mob, out var thresholds))
        {
            foreach (var (threshold, state) in thresholds.Thresholds.ToArray())
            {
                if (state == MobState.Alive)
                    continue;

                _mobThreshold.SetMobStateThreshold(mob, threshold * buff.Health, state, thresholds);
            }
        }
    }

    private void OnWaveMobMeleeDamage(Entity<ContactWaveMobComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.DamageMultiplier != 1f)
            args.Damage *= ent.Comp.DamageMultiplier;
    }

    private void EndContact(ContactRuleComponent comp, ContactResult result)
    {
        if (comp.Phase == ContactPhase.Ended)
            return;

        comp.Phase = ContactPhase.Ended;
        comp.Result = result;
        _roundEnd.EndRound();
    }

    protected override void AppendRoundEndText(EntityUid uid,
        ContactRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, component, gameRule, ref args);

        args.AddLine(Loc.GetString($"mriya-contact-result-{component.Result.ToString().ToLower()}"));
        args.AddLine(Loc.GetString("mriya-contact-round-end-wave", ("wave", component.Wave)));
        args.AddLine(string.Empty);
    }

    private bool AnyMarineAliveOnPlanet()
    {
        var marines = EntityQueryEnumerator<MarineComponent, MobStateComponent, TransformComponent>();
        while (marines.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (_mobState.IsDead(uid, mobState))
                continue;

            if (_rmcPlanet.IsOnPlanet(xform.Coordinates))
                return true;
        }

        return false;
    }

    protected override void Started(EntityUid uid, ContactRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);

        _savedPreflight ??= _config.GetCVar(RMCCVars.RMCDropshipInitialDelayMinutes);
        _config.SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, comp.DropshipPreflightMinutes);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (_savedPreflight is not { } saved)
            return;

        _config.SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, saved);
        _savedPreflight = null;
    }

    private void UpdateWaveMobDespawn(ContactRuleComponent comp, TimeSpan time)
    {
        CollectPlayerPositions();

        var mobs = EntityQueryEnumerator<ContactWaveMobComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out var mob, out var xform))
        {
            if (IsNearPlayer(_transform.GetMapCoordinates(uid, xform), comp.DespawnRange))
            {
                mob.LonelySince = null;
                continue;
            }

            mob.LonelySince ??= time;
            if (time - mob.LonelySince >= comp.DespawnDelay)
                QueueDel(uid);
        }
    }

    private void CollectPlayerPositions()
    {
        _playerPositions.Clear();
        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (_mobState.IsDead(uid, mobState) || HasComp<ContactWaveMobComponent>(uid))
                continue;

            _playerPositions.Add(_transform.GetMapCoordinates(uid, xform));
        }
    }

    private bool IsNearPlayer(MapCoordinates pos, float range)
    {
        var rangeSq = range * range;
        foreach (var player in _playerPositions)
        {
            if (player.MapId == pos.MapId &&
                (player.Position - pos.Position).LengthSquared() <= rangeSq)
            {
                return true;
            }
        }

        return false;
    }

    public bool CanActivateTerminal()
    {
        return GetActiveRule() is { Phase: ContactPhase.Deployed };
    }
}