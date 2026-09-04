using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Explosion;
using Content.Shared.Access.Systems;
using Content.Shared._Mriya.Nuke;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.Nuke;

public sealed partial class MriyaRMCNuclearChargeSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private readonly MriyaRMCNukeSystem _mriyaNuke = default!;
    [Dependency] private readonly MriyaRMCNuclearChargeSharedSystem _mriyaNukeShared = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;

    private static readonly int[] AnnouncementThresholds = [300, 180, 60, 30, 10];
    // The RMC explosion visual is queued after the server detonation tick, so start the theme
    // slightly later to align its 46 second cue with the visible blast.
    private static readonly TimeSpan ThemeStartLeadTime = TimeSpan.FromSeconds(44);
    private readonly HashSet<MapId> _finalizedNukedMaps = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, MriyaNukeActivateDoAfterEvent>(OnActivateDoAfter);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<MriyaRMCNuclearChargeComponent, EntityTerminatingEvent>(OnTerminating);

        Subs.BuiEvents<MriyaRMCNuclearChargeComponent>(MriyaRMCNuclearChargeUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<MriyaRMCNuclearChargeStartBuiMsg>(OnUiStart);
            subs.Event<MriyaRMCNuclearChargeAbortBuiMsg>(OnUiAbort);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MriyaRMCNuclearChargeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var charge, out var xform))
        {
            if (charge.Destroyed)
            {
                QueueDel(uid);
                continue;
            }

            UpdateMarkerLock(uid, charge);
            UpdateUi((uid, charge));

            if (charge.Detonated)
            {
                if (_timing.CurTime < charge.NukeMapAt)
                    continue;

                FinalizeNuclearDetonation(xform.MapID);
                QueueDel(uid);
                continue;
            }

            if (!charge.Armed)
                continue;

            var remaining = charge.DetonatesAt - _timing.CurTime;
            foreach (var threshold in AnnouncementThresholds)
            {
                if (remaining.TotalSeconds > threshold ||
                    !charge.AnnouncedAtSeconds.Add(threshold))
                {
                    continue;
                }

                Announce(Loc.GetString("mriya-nuke-detonation-countdown", ("remaining", FormatRemaining(threshold))));
                AnnounceXenos(Loc.GetString("mriya-nuke-xeno-detonation-countdown", ("remaining", FormatRemainingUkrainian(threshold))));

                if (threshold == 180)
                    StartWarningSiren(charge, xform.MapID);
            }

            if (!charge.ThemeStarted && remaining <= ThemeStartLeadTime)
            {
                charge.ThemeStarted = true;
                charge.WarheadThemeStream = _audio.PlayGlobal(charge.WarheadThemeSound, Filter.Broadcast(), true, charge.WarheadThemeSound.Params)?.Entity;
            }

            if (remaining > TimeSpan.Zero)
                continue;

            StartDetonation(uid, charge, xform);
        }
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _finalizedNukedMaps.Clear();
    }

    private void OnExamined(Entity<MriyaRMCNuclearChargeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Armed)
        {
            var remaining = ent.Comp.DetonatesAt - _timing.CurTime;
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-armed", ("remaining", FormatRemaining(Math.Max(0, (int) remaining.TotalSeconds)))));
            return;
        }

        if (ent.Comp.Activating)
        {
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-activating"));
            return;
        }

        if (HasAuthenticationDisk(ent))
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-disk-inserted"));
        else
            args.PushMarkup(Loc.GetString("mriya-nuke-examine-ready"));
    }

    private void OnItemSlotInsertAttempt(Entity<MriyaRMCNuclearChargeComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.DiskSlotId ||
            args.User == null)
        {
            return;
        }

        if (ent.Comp.Armed || ent.Comp.Detonated || ent.Comp.Activating)
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-port-locked"), ent, args.User.Value, PopupType.MediumCaution);
            return;
        }

        if (!_access.IsAllowed(args.User.Value, ent))
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-officer-disk-required"), ent, args.User.Value, PopupType.MediumCaution);
        }
    }

    private void OnItemSlotEjectAttempt(Entity<MriyaRMCNuclearChargeComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.DiskSlotId)
            return;

        if (ent.Comp.Armed || ent.Comp.Detonated || ent.Comp.Activating)
        {
            args.Cancelled = true;
            if (args.User != null)
                _popup.PopupClient(Loc.GetString("mriya-nuke-popup-port-locked"), ent, args.User.Value, PopupType.MediumCaution);
        }
    }

    private void OnUiOpened(Entity<MriyaRMCNuclearChargeComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnUiStart(Entity<MriyaRMCNuclearChargeComponent> ent, ref MriyaRMCNuclearChargeStartBuiMsg args)
    {
        TryStartActivation(ent, args.Actor);
    }

    private void OnUiAbort(Entity<MriyaRMCNuclearChargeComponent> ent, ref MriyaRMCNuclearChargeAbortBuiMsg args)
    {
        AbortLaunch(ent, args.Actor);
    }

    private void OnInteractHand(Entity<MriyaRMCNuclearChargeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-officer-activation-required"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        _ui.OpenUi(ent.Owner, MriyaRMCNuclearChargeUiKey.Key, args.User);
        UpdateUi(ent);
    }

    private bool TryStartActivation(Entity<MriyaRMCNuclearChargeComponent> ent, EntityUid user)
    {
        if (!ValidateActivation(ent, user))
            return false;

        var ev = new MriyaNukeActivateDoAfterEvent
        {
            Sequence = ++ent.Comp.ActivationSequence,
        };

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.ActivationDelay, ev, ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        ent.Comp.Activating = true;
        EnsureLaunchAnchor(ent);
        UpdateMarkerLock(ent.Owner, ent.Comp);
        UpdateUi(ent);
        _popup.PopupClient(Loc.GetString("mriya-nuke-popup-activation-started"), ent, user, PopupType.LargeCaution);
        return true;
    }

    private bool ValidateActivation(Entity<MriyaRMCNuclearChargeComponent> ent, EntityUid user)
    {
        if (ent.Comp.Armed)
        {
            var remaining = ent.Comp.DetonatesAt - _timing.CurTime;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-already-armed", ("remaining", FormatRemaining(Math.Max(0, (int) remaining.TotalSeconds)))), ent, user, PopupType.LargeCaution);
            return false;
        }

        if (ent.Comp.Activating)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-already-activating"), ent, user, PopupType.MediumCaution);
            return false;
        }

        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-officer-activation-required"), ent, user, PopupType.MediumCaution);
            return false;
        }

        if (!Transform(ent).Anchored)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-anchor-before-activation"), ent, user, PopupType.MediumCaution);
            return false;
        }

        if (!HasAuthenticationDisk(ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-disk-before-activation"), ent, user, PopupType.MediumCaution);
            return false;
        }

        if (!IsChargeAuthorizationComplete())
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-decryption-required"), ent, user, PopupType.MediumCaution);
            return false;
        }

        if (!IsChargeOnOperationalMap(ent.Owner))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-operational-map-required"), ent, user, PopupType.MediumCaution);
            return false;
        }

        return true;
    }

    private void OnActivateDoAfter(Entity<MriyaRMCNuclearChargeComponent> ent, ref MriyaNukeActivateDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Sequence != ent.Comp.ActivationSequence)
            return;

        ent.Comp.Activating = false;
        UpdateMarkerLock(ent.Owner, ent.Comp);
        UpdateUi(ent);

        if (args.Cancelled)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-activation-interrupted"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.Armed || ent.Comp.Detonated)
            return;

        if (!IsChargeAuthorizationComplete())
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-decryption-required"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!IsChargeOnOperationalMap(ent.Owner))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-operational-map-required"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!Transform(ent).Anchored || !HasAuthenticationDisk(ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-final-check-failed"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Armed = true;
        ent.Comp.ThemeStarted = false;
        ent.Comp.AnnouncedAtSeconds.Clear();
        ent.Comp.DetonatesAt = _timing.CurTime + ent.Comp.DetonationDelay;
        EnsureLaunchAnchor(ent);
        UpdateMarkerLock(ent.Owner, ent.Comp);
        UpdateUi(ent);
        var seconds = Math.Max(0, (int) ent.Comp.DetonationDelay.TotalSeconds);
        Announce(Loc.GetString("mriya-nuke-armed", ("remaining", FormatRemaining(seconds))));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-armed", ("remaining", FormatRemainingUkrainian(seconds))));
    }

    private void AbortLaunch(Entity<MriyaRMCNuclearChargeComponent> ent, EntityUid user)
    {
        if (!_access.IsAllowed(user, ent))
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-officer-activation-required"), ent, user, PopupType.MediumCaution);
            return;
        }

        if (!ent.Comp.Armed && !ent.Comp.Activating)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-not-active"), ent, user, PopupType.MediumCaution);
            return;
        }

        ent.Comp.ActivationSequence++;
        ent.Comp.Armed = false;
        ent.Comp.Activating = false;
        ent.Comp.ThemeStarted = false;
        ent.Comp.DetonatesAt = default;
        ent.Comp.AnnouncedAtSeconds.Clear();
        StopWarningSiren(ent.Comp);
        StopWarheadTheme(ent.Comp);
        UpdateMarkerLock(ent.Owner, ent.Comp);
        UpdateUi(ent);

        _popup.PopupClient(Loc.GetString("mriya-nuke-popup-aborted"), ent, user, PopupType.LargeCaution);
        Announce(Loc.GetString("mriya-nuke-aborted"));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-aborted"));
    }

    private void OnUnanchorAttempt(Entity<MriyaRMCNuclearChargeComponent> ent, ref UnanchorAttemptEvent args)
    {
        if (!ent.Comp.Armed && !ent.Comp.Activating && !ent.Comp.Detonated)
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("mriya-nuke-popup-armed-anchor-locked"), ent, args.User, PopupType.LargeCaution);
    }

    private void OnBeforeDamageChanged(Entity<MriyaRMCNuclearChargeComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (HasComp<ProjectileComponent>(args.Source) ||
            HasComp<XenoProjectileComponent>(args.Source))
        {
            args.Cancelled = true;
            return;
        }

        if (ent.Comp.Detonated || ent.Comp.Destroyed ||
            !TryComp(ent, out DamageableComponent? damageable))
        {
            return;
        }

        var currentDamage = damageable.TotalDamage.Float();
        var incomingDamage = args.Damage.GetTotal().Float();
        if (currentDamage < ent.Comp.DisableDamage &&
            (incomingDamage <= 0 || currentDamage + incomingDamage < ent.Comp.DisableDamage))
        {
            return;
        }

        args.Cancelled = true;
        DefuseDestroyedCharge(ent);
    }

    private void OnDamageChanged(Entity<MriyaRMCNuclearChargeComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Damageable.TotalDamage.Float() < ent.Comp.DisableDamage)
            return;

        DefuseDestroyedCharge(ent);
    }

    private void OnDestroyed(Entity<MriyaRMCNuclearChargeComponent> ent, ref DestructionEventArgs args)
    {
        DefuseDestroyedCharge(ent);
    }

    private void OnTerminating(Entity<MriyaRMCNuclearChargeComponent> ent, ref EntityTerminatingEvent args)
    {
        StopWarningSiren(ent.Comp);
        if (!ent.Comp.Detonated)
            StopWarheadTheme(ent.Comp);
    }

    private void DefuseDestroyedCharge(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        if (ent.Comp.Detonated || ent.Comp.Destroyed)
            return;

        ent.Comp.Destroyed = true;
        ent.Comp.Armed = false;
        ent.Comp.Activating = false;
        ent.Comp.ActivationSequence++;
        StopWarningSiren(ent.Comp);
        StopWarheadTheme(ent.Comp);
        UpdateMarkerLock(ent.Owner, ent.Comp);
        UpdateUi(ent);
        Announce(Loc.GetString("mriya-nuke-defused"));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-defused"));
        QueueDel(ent);
    }

    private string FormatRemaining(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return Loc.GetString(minutes == 1 ? "mriya-nuke-time-minute" : "mriya-nuke-time-minutes", ("minutes", minutes));
        }

        return Loc.GetString(seconds == 1 ? "mriya-nuke-time-second" : "mriya-nuke-time-seconds", ("seconds", seconds));
    }

    private string FormatRemainingUkrainian(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return Loc.GetString("mriya-nuke-time-ukrainian", ("value", minutes), ("unit", GetUkrainianPlural(minutes, "mriya-nuke-time-ukrainian-minute-one", "mriya-nuke-time-ukrainian-minute-few", "mriya-nuke-time-ukrainian-minute-many")));
        }

        return Loc.GetString("mriya-nuke-time-ukrainian", ("value", seconds), ("unit", GetUkrainianPlural(seconds, "mriya-nuke-time-ukrainian-second-one", "mriya-nuke-time-ukrainian-second-few", "mriya-nuke-time-ukrainian-second-many")));
    }

    private string GetUkrainianPlural(int value, string oneKey, string fewKey, string manyKey)
    {
        var mod100 = value % 100;
        if (mod100 is >= 11 and <= 14)
            return Loc.GetString(manyKey);

        var key = (value % 10) switch
        {
            1 => oneKey,
            >= 2 and <= 4 => fewKey,
            _ => manyKey,
        };
        return Loc.GetString(key);
    }

    private void StartDetonation(EntityUid uid, MriyaRMCNuclearChargeComponent charge, TransformComponent xform)
    {
        charge.Detonated = true;
        charge.Activating = false;
        charge.ActivationSequence++;
        charge.NukeMapAt = _timing.CurTime + charge.MapKillDelay;
        EnsureLaunchAnchor((uid, charge));
        UpdateMarkerLock(uid, charge);
        UpdateUi((uid, charge));

        var coordinates = _transform.GetMapCoordinates(uid, xform);
        MarkNuclearDetonationStarted();
        Announce(Loc.GetString("mriya-nuke-detonated"));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-detonated"));

        StopWarningSiren(charge);
        charge.WarheadThemeStream = null;
        _audio.PlayGlobal(charge.MapExplosionSound, Filter.BroadcastMap(coordinates.MapId), true);
        _audio.PlayGlobal(charge.FlybyExplosionSound, GetAwayFromMapFilter(coordinates.MapId), true);
        _mriyaNuke.DamageMap(coordinates.MapId);
        Timer.Spawn(charge.MapKillDelay, () => FinalizeNuclearDetonation(coordinates.MapId));

        _rmcExplosion.QueueExplosion(
            coordinates,
            charge.ExplosionType,
            charge.ExplosionTotalIntensity,
            charge.ExplosionSlope,
            charge.ExplosionMaxTileIntensity,
            uid,
            tileBreakScale: 1,
            maxTileBreak: int.MaxValue,
            canCreateVacuum: false);
    }

    private void StartWarningSiren(MriyaRMCNuclearChargeComponent charge, MapId mapId)
    {
        if (charge.WarningSirenStream != null)
            return;

        StopWarningSiren(charge);
        charge.WarningSirenStream = _audio.PlayGlobal(charge.ThirtySecondWarningSound, Filter.BroadcastMap(mapId), true, charge.ThirtySecondWarningSound.Params)?.Entity;
    }

    private void StopWarningSiren(MriyaRMCNuclearChargeComponent charge)
    {
        charge.WarningSirenStream = _audio.Stop(charge.WarningSirenStream);
    }

    private void StopWarheadTheme(MriyaRMCNuclearChargeComponent charge)
    {
        charge.WarheadThemeStream = _audio.Stop(charge.WarheadThemeStream);
    }

    private void UpdateUi(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, MriyaRMCNuclearChargeUiKey.Key))
            return;

        _ui.SetUiState(ent.Owner, MriyaRMCNuclearChargeUiKey.Key, GetUiState(ent));
    }

    private MriyaRMCNuclearChargeBuiState GetUiState(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        return new MriyaRMCNuclearChargeBuiState(
            GetUiStatus(ent),
            CanStartActivation(ent),
            ent.Comp.Armed || ent.Comp.Activating,
            ent.Comp.Armed ? ent.Comp.DetonatesAt : null);
    }

    private string GetUiStatus(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        if (ent.Comp.Destroyed)
            return Loc.GetString("mriya-nuke-ui-status-destroyed");

        if (ent.Comp.Detonated)
            return Loc.GetString("mriya-nuke-ui-status-detonated");

        if (ent.Comp.Armed)
        {
            var remaining = ent.Comp.DetonatesAt - _timing.CurTime;
            return Loc.GetString("mriya-nuke-ui-status-armed",
                ("remaining", FormatRemaining(Math.Max(0, (int) remaining.TotalSeconds))));
        }

        if (ent.Comp.Activating)
            return Loc.GetString("mriya-nuke-ui-status-activating");

        if (!Transform(ent).Anchored)
            return Loc.GetString("mriya-nuke-ui-status-not-anchored");

        if (!HasAuthenticationDisk(ent))
            return Loc.GetString("mriya-nuke-ui-status-no-disk");

        if (!IsChargeAuthorizationComplete())
            return Loc.GetString("mriya-nuke-ui-status-no-decryption");

        if (!IsChargeOnOperationalMap(ent.Owner))
            return Loc.GetString("mriya-nuke-ui-status-wrong-map");

        return Loc.GetString("mriya-nuke-ui-status-ready");
    }

    private bool CanStartActivation(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        return !ent.Comp.Destroyed &&
               !ent.Comp.Detonated &&
               !ent.Comp.Armed &&
               !ent.Comp.Activating &&
               Transform(ent).Anchored &&
               HasAuthenticationDisk(ent) &&
               IsChargeAuthorizationComplete() &&
               IsChargeOnOperationalMap(ent.Owner);
    }

    private void UpdateMarkerLock(EntityUid uid, MriyaRMCNuclearChargeComponent charge)
    {
        if (!TryComp(uid, out MriyaRMCNuclearChargeMarkerComponent? marker))
            return;

        var activeLocked = charge.Armed || charge.Activating || charge.Detonated;
        _mriyaNukeShared.SetActiveLocked((uid, marker), activeLocked);
    }

    private void EnsureLaunchAnchor(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.Anchored)
            return;

        _transform.AnchorEntity((ent.Owner, xform));
    }

    private bool HasAuthenticationDisk(Entity<MriyaRMCNuclearChargeComponent> ent)
    {
        return _itemSlots.TryGetSlot(ent.Owner, ent.Comp.DiskSlotId, out var slot) &&
               slot.HasItem;
    }

    private bool IsChargeOnOperationalMap(EntityUid uid)
    {
        return TryComp(uid, out TransformComponent? xform) &&
               _rmcPlanet.IsOnPlanet(xform);
    }

    private bool IsChargeAuthorizationComplete()
    {
        var query = EntityQueryEnumerator<MriyaIntelNukeObjectiveComponent>();
        while (query.MoveNext(out var objective))
        {
            if (objective.Stage == MriyaIntelNukeStage.ChargeAuthorized)
                return true;
        }

        return false;
    }

    private void Announce(string message)
    {
        _marineAnnounce.AnnounceARESStaging(null, message);
    }

    private void AnnounceXenos(string message)
    {
        _xenoAnnounce.AnnounceQueenMother(message);
    }

    private Filter GetAwayFromMapFilter(MapId mapId)
    {
        return Filter.Empty().AddWhereAttachedEntity(ent => IsAwayFromMap(ent, mapId));
    }

    private bool IsAwayFromMap(EntityUid ent, MapId mapId)
    {
        return TryComp(ent, out TransformComponent? xform) &&
               xform.MapID != mapId;
    }

    private void MarkNuclearDetonationStarted()
    {
        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var distress, out _))
        {
            distress.MriyaNuclearDetonationStarted = true;
            Dirty(uid, distress);
        }
    }

    private void RequestRoundEndCheck()
    {
        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var distress, out _))
        {
            distress.NextCheck = _timing.CurTime;
            Dirty(uid, distress);
        }
    }

    private void FinalizeNuclearDetonation(MapId mapId)
    {
        if (!_finalizedNukedMaps.Add(mapId))
            return;

        _mriyaNuke.NukeMap(mapId);
        RequestRoundEndCheck();
    }
}
