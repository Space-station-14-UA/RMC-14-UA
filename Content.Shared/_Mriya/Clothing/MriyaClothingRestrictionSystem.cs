using Content.Shared.Inventory.Events;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;

namespace Content.Shared._Mriya.Clothing;

public sealed class MriyaClothingRestrictionSystem : EntitySystem
{
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionClothingRestrictionComponent, BeingEquippedAttemptEvent>(OnBeingEquippedAttempt);
    }

    private void OnBeingEquippedAttempt(Entity<FactionClothingRestrictionComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (_faction.IsMemberOfAny(args.EquipTarget, ent.Comp.Factions))
            return;

        args.Cancel();
        args.Reason = ent.Comp.DenyReason;

        var denyReason = Loc.GetString(ent.Comp.DenyReason);
        _popup.PopupClient(denyReason, args.EquipTarget, args.EquipTarget, PopupType.SmallCaution);
    }
}
