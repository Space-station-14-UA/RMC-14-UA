using Content.Shared.Actions;
using Content.Shared.Camera;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._BTP.SmartGun.Optics;

public sealed class BtpSmartGunOpticsSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BtpSmartGunOpticsComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<BtpSmartGunOpticsComponent, BtpSmartGunOpticsActionEvent>(OnToggleOptics);
        SubscribeLocalEvent<BtpSmartGunOpticsComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<BtpSmartGunOpticsComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<BtpSmartGunOpticsComponent, InventoryRelayedEvent<GetEyePvsScaleRelayedEvent>>(OnGetPvsScale);
    }

    private void OnGetActions(Entity<BtpSmartGunOpticsComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || !_inventory.InSlotWithFlags((ent, null, null), ent.Comp.Slots))
            return;

        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnToggleOptics(Entity<BtpSmartGunOpticsComponent> ent, ref BtpSmartGunOpticsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);

        _actions.SetToggled(ent.Comp.Action, ent.Comp.Enabled);
        if (ent.Comp.Enabled)
        {
            _eye.SetMaxZoom(args.Performer, ent.Comp.Zoom);
            _eye.SetZoom(args.Performer, ent.Comp.Zoom);
        }
        else
        {
            _eye.ResetZoom(args.Performer);
        }

        _eye.UpdatePvsScale(args.Performer);
    }

    private void OnEquipped(Entity<BtpSmartGunOpticsComponent> ent, ref GotEquippedEvent args)
    {
        if (args.SlotFlags != ent.Comp.Slots)
            return;

        _eye.UpdatePvsScale(args.Equipee);
    }

    private void OnUnequipped(Entity<BtpSmartGunOpticsComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.SlotFlags != ent.Comp.Slots)
            return;

        ent.Comp.Enabled = false;
        Dirty(ent);

        _actions.SetToggled(ent.Comp.Action, false);
        _eye.ResetZoom(args.Equipee);
        _eye.UpdatePvsScale(args.Equipee);
    }

    private void OnGetPvsScale(Entity<BtpSmartGunOpticsComponent> ent, ref InventoryRelayedEvent<GetEyePvsScaleRelayedEvent> args)
    {
        if (!ent.Comp.Enabled)
            return;

        args.Args.Scale += ent.Comp.PvsIncrease;
    }
}
