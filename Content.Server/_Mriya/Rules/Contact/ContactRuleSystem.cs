using Content.Server._RMC14.Xenonids.Hive;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Spawners;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Coordinates;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Server._RMC14.Marines;
using Content.Shared._RMC14.Marines;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._RMC14.CCVar;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact: low-pop PvE mode. Runs alongside MRContactDistress, which handles planet, ship spawning and dropships.
/// This system owns the field timer, terminals and waves.
/// </summary>
public sealed class ContactRuleSystem : GameRuleSystem<ContactRuleComponent>
{
    [Dependency] private readonly XenoHiveSystem _hive = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;

    private float? _savedPreflight;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLanded);
        SubscribeLocalEvent<ContactTerminalComponent, ActivateInWorldEvent>(OnTerminalActivate);
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

    private void OnTerminalActivate(Entity<ContactTerminalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        var rule = GetActiveRule();
        if (rule == null || rule.Phase != ContactPhase.Deployed)
            return;

        args.Handled = true;

        if (ent.Comp.Activated)
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-used"), ent, args.User);
            return;
        }

        ent.Comp.Activated = true;
        rule.FieldEndsAt += rule.TerminalFieldBonus;
        rule.NextWaveAt -= rule.TerminalWaveReduction;

        _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-activated",
            ("seconds", (int) rule.TerminalFieldBonus.TotalSeconds)), ent, args.User);
    }

    protected override void ActiveTick(EntityUid uid, ContactRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (!component.AresGreetingDone && announcementTime >= component.AresGreetingDelay)
        {
            component.AresGreetingDone = true;

            if (component.StartARESAnnouncements)
                _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("rmc-distress-signal-ares-online"), component.AresGreetingAudio,"rmc-announcement-ares-online");
        }

        if (!component.AresMapDone && announcementTime >= component.AresMapDelay)
        {
            component.AresMapDone = true;

            if (SelectedPlanetMap != null &&
                component.StartARESAnnouncements &&
                SelectedPlanetMap.Value.Comp.Announcement is { } announcement)
            {
                _marineAnnounce.AnnounceARESStaging(default, announcement, announcement: "rmc-announcement-ares-map");
            }
        }

        if (!component.AresPreflightDone && announcementTime >= _dropshipPreflight)
        {
            component.AresPreflightDone = true;

            if (_aresCore.TryGetARES(component.MarineFaction, out var ares))
            {
                _marineAnnounce.AnnounceRadio(ares.Value,
                    Loc.GetString("rmc-distress-signal-preflight-complete"),
                    component.AllClearChannel);
            }
        }

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

        // TODO Mriya: own spawn markers; reusing planet xeno spawn points for now
        var points = new List<EntityUid>();
        var pointQuery = AllEntityQuery<XenoSpawnPointComponent>();
        while (pointQuery.MoveNext(out var point, out _))
        {
            points.Add(point);
        }

        if (points.Count == 0)
        {
            Log.Error($"Contact wave {comp.Wave}: no spawn points");
            return;
        }

        var hive = GetDistressHive();
        var buff = GetBuff(waves, comp.Wave);

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
        _playerPositions.Clear();
        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (_mobState.IsDead(uid, mobState) || HasComp<ContactWaveMobComponent>(uid))
                continue;

            _playerPositions.Add(_transform.GetMapCoordinates(uid, xform));
        }

        var rangeSq = comp.DespawnRange * comp.DespawnRange;
        var mobs = EntityQueryEnumerator<ContactWaveMobComponent, TransformComponent>();
        while (mobs.MoveNext(out var uid, out var mob, out var xform))
        {
            var pos = _transform.GetMapCoordinates(uid, xform);
            var near = false;
            foreach (var player in _playerPositions)
            {
                if (player.MapId == pos.MapId &&
                    (player.Position - pos.Position).LengthSquared() <= rangeSq)
                {
                    near = true;
                    break;
                }
            }

            if (near)
            {
                mob.LonelySince = null;
                continue;
            }

            mob.LonelySince ??= time;
            if (time - mob.LonelySince >= comp.DespawnDelay)
                QueueDel(uid);
        }
    }
}