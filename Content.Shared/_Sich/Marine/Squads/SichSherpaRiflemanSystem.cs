using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.Shared._Mriya.Marine.Squads;

public sealed class MriyaSherpaRiflemanSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MriyaSherpaItemComponent, BeingEquippedAttemptEvent>(OnBeingEquippedAttempt);
    }

    private void OnBeingEquippedAttempt(Entity<MriyaSherpaItemComponent> item, ref BeingEquippedAttemptEvent args)
    {
        if (HasComp<MriyaSherpaRiflemanComponent>(args.EquipTarget))
            return;

        if (args.EquipTarget == args.Equipee)
            _popup.PopupClient(Loc.GetString("rmc-bulky-backpack-user-unable"), args.Equipee, args.Equipee, PopupType.MediumCaution);
        else
            _popup.PopupEntity(Loc.GetString("rmc-bulky-backpack-target-unable", ("target", args.EquipTarget)), args.Equipee, args.Equipee, PopupType.MediumCaution);

        args.Cancel();
    }
}
