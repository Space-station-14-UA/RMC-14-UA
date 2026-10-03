using Content.Server.Administration.Logs;
using Content.Shared._Sich.Hunter;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sich.Hunter;

public sealed partial class HunterRitualSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HunterRitualDuelComponent, ExaminedEvent>(OnRitualExamined);
        SubscribeLocalEvent<HunterRitualDuelComponent, MobStateChangedEvent>(OnRitualPreyMobStateChanged);
        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyMobStateChanged);
    }

    private void OnRitualExamined(Entity<HunterRitualDuelComponent> ent, ref ExaminedEvent args)
    {
        if (Deleted(ent.Comp.Hunter))
            return;

        var message = ent.Comp.State == HunterRitualState.DuelActive
            ? "hunter-ritual-examine-duel"
            : "hunter-ritual-examine-captive";

        args.PushMarkup(Loc.GetString(message, ("hunter", HunterDisplayName(ent.Comp.Hunter))));
    }

    private void OnRitualPreyMobStateChanged(Entity<HunterRitualDuelComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (ent.Comp.State == HunterRitualState.DuelActive &&
            !Deleted(ent.Comp.Hunter) &&
            HasComp<HunterComponent>(ent.Comp.Hunter))
        {
            // TODO: Implement trophy system
            // _trophy.RecordRitualDuelWin(ent.Comp.Hunter, ent.Owner);
            _popup.PopupEntity(Loc.GetString("hunter-ritual-duel-complete", ("target", ent.Owner)), ent.Comp.Hunter, ent.Comp.Hunter);
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(ent.Comp.Hunter):hunter} completed a Hunter ritual duel against {ToPrettyString(ent.Owner):target}");
        }

        RemCompDeferred<HunterRitualDuelComponent>(ent);
    }

    private void OnAnyMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !HasComp<HunterComponent>(args.Target))
            return;

        var query = EntityQueryEnumerator<HunterRitualDuelComponent>();
        while (query.MoveNext(out var uid, out var ritual))
        {
            if (ritual.Hunter == args.Target)
            {
                RemCompDeferred<HunterRitualDuelComponent>(uid);
            }
        }
    }

    public bool TryClaimCaptive(EntityUid hunter, EntityUid target, bool bypassControlRequirement = false)
    {
        if (!CanClaimCaptive(hunter, target, bypassControlRequirement, true))
            return false;

        if (TryComp(target, out HunterRitualDuelComponent? existing))
        {
            if (existing.Hunter == hunter)
                return true;

            _popup.PopupEntity(Loc.GetString("hunter-ritual-already-claimed"), hunter, hunter, PopupType.SmallCaution);
            return false;
        }

        var ritual = EnsureComp<HunterRitualDuelComponent>(target);
        ritual.Hunter = hunter;
        ritual.State = HunterRitualState.Captive;
        ritual.CapturedAt = _timing.CurTime;
        ritual.DuelStartedAt = TimeSpan.Zero;

        _audio.PlayPvs(ritual.ClaimSound, target);
        _popup.PopupEntity(Loc.GetString("hunter-ritual-captive-claimed", ("target", target)), hunter, hunter);
        _popup.PopupEntity(Loc.GetString("hunter-ritual-captive-target", ("hunter", HunterDisplayName(hunter))), target, target, PopupType.MediumCaution);
        PopupToWitnesses(
            hunter,
            target,
            Loc.GetString("hunter-ritual-captive-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):hunter} claimed {ToPrettyString(target):target} as a Hunter ritual captive");
        return true;
    }

    public bool TryBeginDuel(EntityUid hunter, EntityUid target)
    {
        if (!TryComp(target, out HunterRitualDuelComponent? ritual) ||
            ritual.Hunter != hunter ||
            ritual.State != HunterRitualState.Captive ||
            !_mob.IsAlive(target))
        {
            return false;
        }

        ritual.State = HunterRitualState.DuelActive;
        ritual.DuelStartedAt = _timing.CurTime;

        if (TryComp(target, out PullableComponent? pullable) && pullable.Puller == hunter)
            _pulling.TryStopPull(target, pullable, hunter);

        _audio.PlayPvs(ritual.DuelSound, target);
        _popup.PopupEntity(Loc.GetString("hunter-ritual-duel-started", ("target", target)), hunter, hunter);
        _popup.PopupEntity(Loc.GetString("hunter-ritual-duel-target", ("hunter", HunterDisplayName(hunter))), target, target, PopupType.MediumCaution);
        PopupToWitnesses(
            hunter,
            target,
            Loc.GetString("hunter-ritual-duel-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            PopupType.LargeCaution);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(hunter):hunter} began a Hunter ritual duel with {ToPrettyString(target):target}");
        return true;
    }

    public bool TryReleaseCaptive(EntityUid hunter, EntityUid target)
    {
        if (!TryComp(target, out HunterRitualDuelComponent? ritual) ||
            ritual.Hunter != hunter)
        {
            return false;
        }

        _audio.PlayPvs(ritual.ReleaseSound, target);
        RemCompDeferred<HunterRitualDuelComponent>(target);
        _popup.PopupEntity(Loc.GetString("hunter-ritual-released", ("target", target)), hunter, hunter);
        PopupToWitnesses(
            hunter,
            target,
            Loc.GetString("hunter-ritual-release-others", ("hunter", HunterDisplayName(hunter)), ("target", target)),
            PopupType.MediumCaution);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(hunter):hunter} released Hunter ritual captive {ToPrettyString(target):target}");
        return true;
    }

    public bool CanClaimCaptive(EntityUid hunter, EntityUid target, bool bypassControlRequirement, bool popup)
    {
        if (Deleted(hunter) ||
            Deleted(target) ||
            hunter == target ||
            !HasComp<HunterComponent>(hunter) ||
            HasComp<HunterComponent>(target) ||
            !TryComp<MobStateComponent>(target, out var mob) ||
            !_mob.IsAlive(target, mob) ||
            (!HasComp<HumanoidAppearanceComponent>(target) && !HasComp<XenoComponent>(target)))
        {
            return false;
        }

        if (bypassControlRequirement || IsPulling(hunter, target))
            return true;

        if (popup)
            _popup.PopupEntity(Loc.GetString("hunter-ritual-requires-control"), hunter, hunter, PopupType.SmallCaution);

        return false;
    }

    private bool IsPulling(EntityUid hunter, EntityUid target)
    {
        return TryComp<PullerComponent>(hunter, out var puller) && puller.Pulling == target;
    }

    private void PopupToWitnesses(EntityUid hunter, EntityUid target, string message, PopupType type)
    {
        var filter = Filter.Pvs(target, entityManager: EntityManager)
            .RemoveWhereAttachedEntity(attached => attached == hunter || attached == target);
        _popup.PopupEntity(message, target, filter, true, type);
    }

    private string HunterDisplayName(EntityUid hunter)
    {
        return HasComp<HunterComponent>(hunter)
            ? Loc.GetString("hunter-identity-unknown")
            : Name(hunter);
    }
}