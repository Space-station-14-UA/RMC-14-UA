using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.Nuke;

public sealed class MriyaRMCNuclearChargeSharedSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> MriyaNukeDiskTag = "MRNukeDisk";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MriyaRMCNuclearChargeMarkerComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeMarkerComponent, GettingPickedUpAttemptEvent>(OnGettingPickedUpAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeMarkerComponent, ItemSlotInsertAttemptEvent>(OnItemSlotInsertAttempt);
        SubscribeLocalEvent<MriyaRMCNuclearChargeMarkerComponent, PowerLoaderGrabEvent>(OnPowerLoaderGrab);
    }

    public void SetActiveLocked(Entity<MriyaRMCNuclearChargeMarkerComponent> ent, bool activeLocked)
    {
        if (ent.Comp.ActiveLocked == activeLocked)
            return;

        ent.Comp.ActiveLocked = activeLocked;
        Dirty(ent);
    }

    private void OnPullAttempt(Entity<MriyaRMCNuclearChargeMarkerComponent> ent, ref PullAttemptEvent args)
    {
        if (args.PulledUid == ent.Owner)
            args.Cancelled = true;
    }

    private void OnGettingPickedUpAttempt(Entity<MriyaRMCNuclearChargeMarkerComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        if (ent.Comp.ActiveLocked)
        {
            args.Cancel();
            return;
        }

        if (HasComp<PowerLoaderComponent>(args.User))
            return;

        args.Cancel();
    }

    private void OnPowerLoaderGrab(Entity<MriyaRMCNuclearChargeMarkerComponent> ent, ref PowerLoaderGrabEvent args)
    {
        if (!ent.Comp.ActiveLocked)
            return;

        args.Handled = true;
        args.ToGrab = null;

        foreach (var buckled in args.Buckled)
        {
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-armed-anchor-locked"), ent, buckled, PopupType.MediumCaution);
        }
    }

    private void OnItemSlotInsertAttempt(Entity<MriyaRMCNuclearChargeMarkerComponent> ent, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Slot.ID != ent.Comp.DiskSlotId ||
            args.User == null)
        {
            return;
        }

        if (!_tag.HasTag(args.Item, MriyaNukeDiskTag))
        {
            args.Cancelled = true;
            _popup.PopupClient(Loc.GetString("mriya-nuke-popup-wrong-disk"), ent, args.User.Value, PopupType.MediumCaution);
            return;
        }

        if (Transform(ent).Anchored)
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("mriya-nuke-popup-anchor-before-disk"), ent, args.User.Value, PopupType.MediumCaution);
    }
}
