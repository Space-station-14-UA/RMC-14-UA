using Content.Server._RMC14.Nuke;
using Content.Server._RMC14.Announce;
using Content.Server._RMC14.Explosion;
using Content.Server.RoundEnd;
using Content.Shared.Access.Systems;
using Content.Shared._BTP.Nuke;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Construction.Components;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Nuke;
using Content.Shared.Projectiles;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._BTP.Nuke;

public sealed partial class BTPRMCNuclearChargeSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private readonly RMCNukeSystem _rmcNuke = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;

    private static readonly int[] AnnouncementThresholds = [300, 180, 60, 30, 10];
    private static readonly TimeSpan ThemeLeadTime = TimeSpan.FromSeconds(46);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEjectAttempt);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, BTPNukeActivateDoAfterEvent>(OnActivateDoAfter);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<BTPRMCNuclearChargeComponent, EntityTerminatingEvent>(OnTerminating);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BTPRMCNuclearChargeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var charge, out var xform))
        {
            if (charge.Destroyed)
            {
                QueueDel(uid);
                continue;
            }

            if (charge.Detonated)
            {
                if (_timing.CurTime < charge.NukeMapAt)
                    continue;

                _rmcNuke.NukeMap(xform.MapID);
                EndRoundMinorMarineVictory();
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

                Announce($"Nuclear Fission Explosive detonation in {FormatRemaining(threshold)}.");
                AnnounceXenos($"Винищувач-вуликів вибухне за {FormatRemainingUkrainian(threshold)}.");

                if (threshold == 180)
                    StartWarningSiren(charge, xform.MapID);
            }

            if (!charge.ThemeStarted && remaining <= ThemeLeadTime)
            {
                charge.ThemeStarted = true;
                charge.WarheadThemeStream = _audio.PlayGlobal(charge.WarheadThemeSound, Filter.Broadcast(), true, charge.WarheadThemeSound.Params)?.Entity;
            }

            if (remaining > TimeSpan.Zero)
                continue;

            StartDetonation(uid, charge, xform);
        }
    }

    private void OnExamined(Entity<BTPRMCNuclearChargeComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Armed)
        {
            var remaining = ent.Comp.DetonatesAt - _timing.CurTime;
            args.PushMarkup($"[color=red]It is armed. Estimated detonation in {FormatRemaining(Math.Max(0, (int) remaining.TotalSeconds))}.[/color]");
            return;
        }

        if (ent.Comp.Activating)
        {
            args.PushMarkup("[color=yellow]Its activation sequence is being entered.[/color]");
            return;
        }

        if (HasAuthenticationDisk(ent))
            args.PushMarkup("[color=cyan]The charge is decoded, anchored authorization is available, and a nuclear authentication disk is inserted.[/color]");
        else
            args.PushMarkup("[color=cyan]The charge is decoded and ready. Anchor it, insert a nuclear authentication disk, then use it in hand to begin the activation sequence.[/color]");
    }

    private void OnItemSlotInsertAttempt(Entity<BTPRMCNuclearChargeComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.DiskSlotId ||
            !HasComp<NukeDiskComponent>(args.Item) ||
            args.User == null)
        {
            return;
        }

        if (ent.Comp.Armed || ent.Comp.Detonated || ent.Comp.Activating)
        {
            args.Cancelled = true;
            _popup.PopupClient("The authentication port is locked while the nuclear protocol is active.", ent, args.User.Value, PopupType.MediumCaution);
            return;
        }

        if (!Transform(ent).Anchored)
        {
            args.Cancelled = true;
            _popup.PopupClient("Anchor the charge before inserting the nuclear authentication disk.", ent, args.User.Value, PopupType.MediumCaution);
            return;
        }

        if (!_access.IsAllowed(args.User.Value, ent))
        {
            args.Cancelled = true;
            _popup.PopupClient("Officer authorization is required to insert the nuclear authentication disk.", ent, args.User.Value, PopupType.MediumCaution);
        }
    }

    private void OnItemSlotEjectAttempt(Entity<BTPRMCNuclearChargeComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.DiskSlotId)
            return;

        if (ent.Comp.Armed || ent.Comp.Detonated || ent.Comp.Activating)
        {
            args.Cancelled = true;
            if (args.User != null)
                _popup.PopupClient("The authentication port is locked while the nuclear protocol is active.", ent, args.User.Value, PopupType.MediumCaution);
        }
    }

    private void OnInteractHand(Entity<BTPRMCNuclearChargeComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Armed)
        {
            var remaining = ent.Comp.DetonatesAt - _timing.CurTime;
            _popup.PopupClient($"The charge is already armed. Detonation in {FormatRemaining(Math.Max(0, (int) remaining.TotalSeconds))}.", ent, args.User, PopupType.LargeCaution);
            return;
        }

        if (ent.Comp.Activating)
        {
            _popup.PopupClient("The charge is already processing an activation sequence.", ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupClient("Officer authorization is required to start the detonation protocol.", ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!Transform(ent).Anchored)
        {
            _popup.PopupClient("The charge must be anchored before activation.", ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (!HasAuthenticationDisk(ent))
        {
            _popup.PopupClient("Insert a nuclear authentication disk before starting the detonation protocol.", ent, args.User, PopupType.MediumCaution);
            return;
        }

        var ev = new BTPNukeActivateDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.ActivationDelay, ev, ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        ent.Comp.Activating = true;
        _popup.PopupClient("You begin entering the decoded nuclear activation sequence.", ent, args.User, PopupType.LargeCaution);
    }

    private void OnActivateDoAfter(Entity<BTPRMCNuclearChargeComponent> ent, ref BTPNukeActivateDoAfterEvent args)
    {
        ent.Comp.Activating = false;

        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled)
        {
            _popup.PopupClient("The nuclear activation sequence is interrupted.", ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (ent.Comp.Armed || ent.Comp.Detonated)
            return;

        if (!Transform(ent).Anchored || !HasAuthenticationDisk(ent))
        {
            _popup.PopupClient("The nuclear activation sequence fails its final authorization check.", ent, args.User, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Armed = true;
        ent.Comp.DetonatesAt = _timing.CurTime + ent.Comp.DetonationDelay;
        var seconds = Math.Max(0, (int) ent.Comp.DetonationDelay.TotalSeconds);
        Announce($"Nuclear Fission Explosive armed. Estimated detonation in {FormatRemaining(seconds)}. Evacuate the operational area.");
        AnnounceXenos($"Верховна Королева попереджає: Винищувач-вуликів активовано. До вибуху лишається {FormatRemainingUkrainian(seconds)}.");
    }

    private void OnUnanchorAttempt(Entity<BTPRMCNuclearChargeComponent> ent, ref UnanchorAttemptEvent args)
    {
        if (!ent.Comp.Armed)
            return;

        args.Cancel();
        _popup.PopupClient("The armed charge refuses to release its anchor locks.", ent, args.User, PopupType.LargeCaution);
    }

    private void OnBeforeDamageChanged(Entity<BTPRMCNuclearChargeComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (HasComp<ProjectileComponent>(args.Source) ||
            HasComp<XenoProjectileComponent>(args.Source))
        {
            args.Cancelled = true;
        }
    }

    private void OnDamageChanged(Entity<BTPRMCNuclearChargeComponent> ent, ref DamageChangedEvent args)
    {
        if (args.Damageable.TotalDamage.Float() < ent.Comp.DisableDamage)
            return;

        DefuseDestroyedCharge(ent);
    }

    private void OnDestroyed(Entity<BTPRMCNuclearChargeComponent> ent, ref DestructionEventArgs args)
    {
        DefuseDestroyedCharge(ent);
    }

    private void OnTerminating(Entity<BTPRMCNuclearChargeComponent> ent, ref EntityTerminatingEvent args)
    {
        StopWarningSiren(ent.Comp);
        StopWarheadTheme(ent.Comp);
    }

    private void DefuseDestroyedCharge(Entity<BTPRMCNuclearChargeComponent> ent)
    {
        if (ent.Comp.Detonated || ent.Comp.Destroyed)
            return;

        ent.Comp.Destroyed = true;
        ent.Comp.Armed = false;
        ent.Comp.Activating = false;
        StopWarningSiren(ent.Comp);
        StopWarheadTheme(ent.Comp);
        Announce("Фізично неможливо синхронізовано активувати боєзапас у зв'язку зі значними фізичними пошкодженнями систем запуску.");
        AnnounceXenos("Верховна Королева повідомляє: Винищувач-вуликів знешкоджено. Боєголовка більше не становить загрози для Вулика.");
        QueueDel(ent);
    }

    private string FormatRemaining(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
        }

        return $"{seconds} second{(seconds == 1 ? "" : "s")}";
    }

    private string FormatRemainingUkrainian(int seconds)
    {
        return seconds switch
        {
            >= 300 => $"{(int) Math.Ceiling(seconds / 60f)} хвилин",
            >= 120 => $"{(int) Math.Ceiling(seconds / 60f)} хвилини",
            >= 60 => "1 хвилина",
            30 => "30 секунд",
            10 => "10 секунд",
            _ => $"{seconds} секунд",
        };
    }

    private void StartDetonation(EntityUid uid, BTPRMCNuclearChargeComponent charge, TransformComponent xform)
    {
        charge.Detonated = true;
        charge.NukeMapAt = _timing.CurTime + charge.MapKillDelay;

        var coordinates = _transform.GetMapCoordinates(uid, xform);
        Announce("Nuclear Fission Explosive detonation detected. Strategic area denial protocol executing.");
        AnnounceXenos("Верховна Королева попереджає: Винищувач-вуликів детонував. Вулик має покинути приречену зону.");

        StopWarningSiren(charge);
        StopWarheadTheme(charge);
        _audio.PlayGlobal(charge.MapExplosionSound, Filter.BroadcastMap(coordinates.MapId), true);
        _audio.PlayGlobal(charge.FlybyExplosionSound, GetAwayFromMapFilter(coordinates.MapId), true);
        _rmcNuke.NukeMap(coordinates.MapId);

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

    private void StartWarningSiren(BTPRMCNuclearChargeComponent charge, MapId mapId)
    {
        if (charge.WarningSirenStream != null)
            return;

        StopWarningSiren(charge);
        charge.WarningSirenStream = _audio.PlayGlobal(charge.ThirtySecondWarningSound, Filter.BroadcastMap(mapId), true, charge.ThirtySecondWarningSound.Params)?.Entity;
    }

    private void StopWarningSiren(BTPRMCNuclearChargeComponent charge)
    {
        charge.WarningSirenStream = _audio.Stop(charge.WarningSirenStream);
    }

    private void StopWarheadTheme(BTPRMCNuclearChargeComponent charge)
    {
        charge.WarheadThemeStream = _audio.Stop(charge.WarheadThemeStream);
    }

    private bool HasAuthenticationDisk(Entity<BTPRMCNuclearChargeComponent> ent)
    {
        return _itemSlots.TryGetSlot(ent.Owner, ent.Comp.DiskSlotId, out var slot) &&
               slot.HasItem;
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

    private void EndRoundMinorMarineVictory()
    {
        var ended = false;
        var query = EntityQueryEnumerator<CMDistressSignalRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var distress, out _))
        {
            if (distress.Result is not null and not DistressSignalRuleResult.None)
                continue;

            distress.Result = DistressSignalRuleResult.MinorMarineVictory;
            Dirty(uid, distress);
            ended = true;
        }

        if (ended)
            _roundEnd.EndRound();
    }
}
