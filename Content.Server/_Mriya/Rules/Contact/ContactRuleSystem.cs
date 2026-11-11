using System.Linq;
using Content.Server._CMU14.Announce;
using Content.Server._RMC14.Marines;
using Content.Server._RMC14.Rules.DistressSignal;
using Content.Server._RMC14.Xenonids.Hive;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RoundEnd;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Examine;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact: low-pop PvE mode. Runs alongside MRContactDistress, which handles planet, ship spawning and dropships.
/// This system owns the field timer, anchors, waves and (in the .Extraction partial) evacuation.
/// HUD state is sent on every change, plus a snapshot to each player when they get attached to an entity.
/// </summary>
public sealed partial class ContactRuleSystem : GameRuleSystem<ContactRuleComponent>
{
    [Dependency] private readonly XenoHiveSystem _hive = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ContactEconomySystem _economy = default!;
    [Dependency] private readonly GeneralAnnounceSystem _announce = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distress = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    private float? _savedPreflight;
    private readonly List<MapCoordinates> _playerPositions = new();
    private readonly List<MapCoordinates> _anchors = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLanded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ContactWaveMobComponent, GetMeleeDamageEvent>(OnWaveMobMeleeDamage);
    }

    private Entity<ContactRuleComponent>? GetActiveRule()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out _))
        {
            return (uid, comp);
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

    /// <summary>
    /// Picks a Contact planet and forces it onto Distress (same as ForcePlanetMapCommand).
    /// Overrides the lobby vote, which only knows rotation maps.
    /// </summary>
    private void ForcePlanet(ContactRuleComponent comp)
    {
        if (comp.Planets.Count == 0)
            return;

        var pool = _rmcPlanet.GetAllPlanets()
            .Where(p => comp.Planets.Contains(p.Proto.ID))
            .ToList();

        if (pool.Count == 0)
        {
            Log.Error("Contact: none of the configured planets exist");
            return;
        }

        _distress.SetPlanet(_random.Pick(pool));
    }

    #region Round flow

    protected override void Started(EntityUid uid, ContactRuleComponent comp, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, comp, gameRule, args);
        ForcePlanet(comp);

        _savedPreflight ??= _config.GetCVar(RMCCVars.RMCDropshipInitialDelayMinutes);
        _config.SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, comp.DropshipPreflightMinutes);

        SendHud(comp);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        if (_savedPreflight is not { } saved)
            return;

        _config.SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, saved);
        _savedPreflight = null;
    }

    private void OnDropshipLanded(ref DropshipLandedOnPlanetEvent ev)
    {
        if (GetActiveRule() is not { } rule || rule.Comp.Phase != ContactPhase.Staging)
            return;

        var comp = rule.Comp;
        var time = Timing.CurTime;
        comp.Phase = ContactPhase.Deployed;
        comp.FieldEndsAt = time + comp.FieldStart;
        comp.NextWaveAt = time + comp.WaveInterval;
        comp.NextMarineCheck = time + comp.MarineCheckEvery;

        AnnounceBig(comp, Loc.GetString("mriya-contact-deployed",
            ("seconds", (int) comp.FieldStart.TotalSeconds)));

        SendHud(comp);
    }

    protected override void ActiveTick(EntityUid uid, ContactRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        if (comp.Phase != ContactPhase.Deployed)
            return;

        var time = Timing.CurTime;

        // frozen (FieldEndsAt null) during an extraction offer / evacuation
        if (!comp.FieldUnavailable && time >= comp.FieldEndsAt)
        {
            EndContact(comp, ContactResult.FieldCollapsed);
            return;
        }

        if (time >= comp.NextMarineCheck)
        {
            comp.NextMarineCheck = time + comp.MarineCheckEvery;

            // departing: everyone aboard just left the colony on purpose; the extraction decides the result
            var departing = IsEvacuating(uid, out var exState) && exState == ContactExtractionState.Departing;
            if (!departing && !AnyMarineAliveOnPlanet())
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

        UpdateExtraction(uid, comp, time);
        if (comp.Phase != ContactPhase.Deployed)
            return;

        if (time >= comp.NextWaveAt)
            StartWave(uid, comp, time);
    }

    private void EndContact(ContactRuleComponent comp, ContactResult result)
    {
        if (comp.Phase == ContactPhase.Ended)
            return;

        comp.Phase = ContactPhase.Ended;
        comp.Result = result;
        SendHud(comp);
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

    #endregion

    #region Anchors

    /// <summary>
    /// Called by ContactTerminalSystem when a player finishes activating an anchor.
    /// Fails before landing, after the round, and while the field is unavailable (extraction offer / evacuation).
    /// </summary>
    public bool TryActivateTerminal(out TimeSpan bonus)
    {
        bonus = default;
        if (GetActiveRule() is not { } rule)
            return false;

        var comp = rule.Comp;
        if (comp.Phase != ContactPhase.Deployed || comp.FieldUnavailable)
            return false;

        comp.FieldEndsAt += comp.TerminalFieldBonus;
        comp.NextWaveAt -= comp.TerminalWaveReduction;
        bonus = comp.TerminalFieldBonus;

        SendHud(comp);
        return true;
    }

    /// <summary>
    /// Read-only check so the anchor do-after doesn't start when it can't succeed.
    /// </summary>
    public bool CanActivateTerminal()
    {
        return GetActiveRule() is { Comp: { Phase: ContactPhase.Deployed, FieldUnavailable: false } };
    }

    #endregion

    #region Waves

    private void StartWave(EntityUid uid, ContactRuleComponent comp, TimeSpan time)
    {
        // Extraction offered between waves: no next wave until it's taken or skipped.
        if (TryComp(uid, out ContactExtractionComponent? offer) &&
            offer.State is ContactExtractionState.Available or ContactExtractionState.Countdown)
        {
            return;
        }

        // evacuation: waves keep coming (faster) but don't count, so exfil at wave 10 stays wave 10
        var counting = !IsEvacuating(uid, out _);

        // wave 10 is over and 11 is due: offer extraction instead
        if (counting && TryOfferExtraction(uid, comp, time))
            return;

        if (counting)
            comp.Wave++;

        comp.NextWaveAt = time + GetWaveInterval(uid, comp);

        if (!_prototypes.TryIndex(comp.Waves, out var waves))
        {
            Log.Error($"Contact: no waves prototype {comp.Waves}");
            SendHud(comp);
            return;
        }

        var buff = GetBuff(waves, comp.Wave);
        if (buff != null && buff.Wave != comp.LastBuffWave)
        {
            comp.LastBuffWave = buff.Wave;
            AnnounceBuff(comp, buff);
        }

        CollectSpawnAnchors();
        if (_anchors.Count == 0)
        {
            Log.Warning($"Contact wave {comp.Wave}: no living marines on the planet to spawn around");
        }
        else
        {
            var hive = GetDistressHive();
            for (var i = 0; i < waves.Spawns.Count; i++)
            {
                var entry = waves.Spawns[i];
                if (comp.Wave < entry.StartWave)
                    continue;

                if (entry.EndWave > 0 && comp.Wave > entry.EndWave)
                    continue;

                if (entry.Every > 0 && (comp.Wave - entry.StartWave) % entry.Every != 0)
                    continue;

                var rate = entry.Rate + entry.RatePerWave * (comp.Wave - entry.StartWave);
                var acc = comp.SpawnAccumulators.GetValueOrDefault(i) + rate;
                var count = (int) acc;
                comp.SpawnAccumulators[i] = acc - count;

                if (entry.MaxAlive >= 0)
                    count = Math.Min(count, entry.MaxAlive - CountAliveWaveMobs(entry.Proto));

                for (var j = 0; j < count; j++)
                {
                    if (TryFindSpawnTile(comp, out var coords))
                        SpawnWaveMob(entry, coords, hive, buff);
                }
            }
        }

        if (counting)
            AnnounceScreen(comp, Loc.GetString("mriya-contact-wave-started", ("wave", comp.Wave)));

        SendHud(comp);
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

    private void AnnounceBuff(ContactRuleComponent comp, ContactWaveBuffEntry buff)
    {
        var msg = buff.Announcement is { } custom
            ? Loc.GetString(custom)
            : Loc.GetString("mriya-contact-buff",
                ("health", (int) MathF.Round((buff.Health - 1f) * 100)),
                ("damage", (int) MathF.Round((buff.Damage - 1f) * 100)));

        AnnounceBig(comp, msg);
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

    /// <summary>
    /// Living marines on the colony; waves spawn around them.
    /// </summary>
    private void CollectSpawnAnchors()
    {
        _anchors.Clear();
        var query = EntityQueryEnumerator<MarineComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState, out var xform))
        {
            if (_mobState.IsDead(uid, mobState) || !_rmcPlanet.IsOnPlanet(xform))
                continue;

            _anchors.Add(_transform.GetMapCoordinates(uid, xform));
        }
    }

    /// <summary>
    /// Random free planet tile SpawnMinRange..SpawnMaxRange from a random marine and at least SpawnMinRange from all of them.
    /// </summary>
    private bool TryFindSpawnTile(ContactRuleComponent comp, out EntityCoordinates coords)
    {
        coords = default;
        EntityCoordinates? visibleFallback = null;
        var minSq = comp.SpawnMinRange * comp.SpawnMinRange;

        for (var attempt = 0; attempt < comp.SpawnAttempts; attempt++)
        {
            var anchor = _random.Pick(_anchors);
            var distance = _random.NextFloat(comp.SpawnMinRange, comp.SpawnMaxRange);
            var pos = anchor.Position + _random.NextAngle().ToVec() * distance;
            var map = new MapCoordinates(pos, anchor.MapId);

            if (!_mapManager.TryFindGridAt(map, out var gridUid, out var grid))
                continue;

            var indices = _mapSystem.WorldToTile(gridUid, grid, pos);
            if (!_mapSystem.TryGetTileRef(gridUid, grid, indices, out var tile) || tile.Tile.IsEmpty)
                continue;

            if (_turf.IsTileBlocked(gridUid, indices, CollisionGroup.Impassable, grid))
                continue;

            var candidate = _mapSystem.GridTileToLocal(gridUid, grid, indices);

            // planet only (not the dropship), and not on top of any marine
            if (!_rmcPlanet.IsOnPlanet(candidate))
                continue;

            var candidateMap = _transform.ToMapCoordinates(candidate);
            var tooClose = false;
            var seen = false;
            foreach (var marine in _anchors)
            {
                if (marine.MapId != candidateMap.MapId)
                    continue;

                if ((marine.Position - candidateMap.Position).LengthSquared() < minSq)
                {
                    tooClose = true;
                    break;
                }

                if (!seen && _examine.InRangeUnOccluded(marine, candidateMap, comp.SpawnSightRange, null))
                    seen = true;
            }

            if (tooClose)
                continue;

            if (seen)
            {
                visibleFallback ??= candidate;
                continue;
            }

            coords = candidate;
            return true;
        }

        // open maps: nothing hidden found, spawn in view rather than not at all
        if (visibleFallback is { } fallback)
        {
            coords = fallback;
            return true;
        }

        return false;
    }

    private void SpawnWaveMob(ContactWaveSpawnEntry entry, EntityCoordinates coords, EntityUid? hive, ContactWaveBuffEntry? buff)
    {
        var mob = SpawnAtPosition(entry.Proto, coords);
        if (hive != null)
            _hive.SetHive(mob, hive.Value);

        var waveMob = EnsureComp<ContactWaveMobComponent>(mob);
        waveMob.Proto = entry.Proto;
        waveMob.Reward = entry.Reward;

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

    #endregion

    #region Despawn

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

    #endregion

    #region HUD

    private ContactHudStateEvent MakeHudEvent(ContactRuleComponent comp)
    {
        var phase = comp.Phase switch
        {
            ContactPhase.Staging => ContactHudPhase.Staging,
            ContactPhase.Deployed => ContactHudPhase.Deployed,
            _ => ContactHudPhase.Ended,
        };

        return new ContactHudStateEvent(phase, comp.FieldEndsAt, comp.NextWaveAt, comp.Wave, comp.FieldUnavailable);
    }

    private void SendHud(ContactRuleComponent comp)
    {
        RaiseNetworkEvent(MakeHudEvent(comp));
    }

    /// <summary>
    /// Joining, ghosting or reconnecting: send this player the current HUD state once.
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (GetActiveRule() is not { } rule)
            return;

        RaiseNetworkEvent(MakeHudEvent(rule.Comp), ev.Player);

        if (TryComp(rule, out ContactExtractionComponent? ex) &&
            ex.State is not (ContactExtractionState.None or ContactExtractionState.Done))
        {
            RaiseNetworkEvent(MakeExtractionHudEvent(ex), ev.Player);
        }

        _shuttle.SendHudTo(ev.Player);
        _economy.SendBalanceTo(ev.Player);
    }

    #endregion

    #region Announcements

    /// <summary>
    /// For other Contact systems (terminals, shuttle): on-screen announcement with the active rule's preset, no chat line.
    /// </summary>
    public void Announce(string message)
    {
        if (GetActiveRule() is { } rule)
            AnnounceScreen(rule.Comp, message);
    }

    /// <summary>
    /// Important moments: big on-screen announcement + the usual ARES chat line (so it stays in the log).
    /// </summary>
    private void AnnounceBig(ContactRuleComponent comp, string message)
    {
        _marineAnnounce.AnnounceARESStaging(default, message);
        _announce.Announce(comp.AnnouncementPreset, message);
    }

    /// <summary>
    /// On-screen only, nothing in chat.
    /// </summary>
    private void AnnounceScreen(ContactRuleComponent comp, string message)
    {
        _announce.Announce(comp.AnnouncementPreset, message);
    }

    #endregion
}