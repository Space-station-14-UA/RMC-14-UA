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

    private float? _savedPreflight;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLanded);
        SubscribeLocalEvent<ContactTerminalComponent, ActivateInWorldEvent>(OnTerminalActivate);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
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

        if (time >= comp.NextWaveAt)
            StartWave(comp, time);
    }

    private void StartWave(ContactRuleComponent comp, TimeSpan time)
    {
        comp.Wave++;
        comp.NextWaveAt = time + comp.WaveInterval;

        // TODO Mriya: own spawn markers; reusing planet xeno spawn points for now
        var points = new List<EntityUid>();
        var pointQuery = AllEntityQuery<XenoSpawnPointComponent>();
        while (pointQuery.MoveNext(out var point, out _))
        {
            points.Add(point);
        }

        if (points.Count == 0 || comp.WaveSpawns.Count == 0)
        {
            Log.Error($"Contact wave {comp.Wave}: no spawn points or spawns configured");
            return;
        }

        var hive = GetDistressHive();
        var count = comp.BaseWaveSize + (int) (comp.Wave * comp.WaveSizePerWave);
        for (var i = 0; i < count; i++)
        {
            var xeno = SpawnAtPosition(_random.Pick(comp.WaveSpawns), _random.Pick(points).ToCoordinates());
            if (hive != null)
                _hive.SetHive(xeno, hive.Value);

            // TODO Mriya: NPC/HTN so they actually hunt marines
        }

        _marineAnnounce.AnnounceARESStaging(default, (Loc.GetString("mriya-contact-wave-started", ("wave", comp.Wave))));
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
}