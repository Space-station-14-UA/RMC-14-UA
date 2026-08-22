using System.Diagnostics.CodeAnalysis;
using Content.Shared.Inventory;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.GameStates;

namespace Content.Shared._Sich.Hunter.Caster;

public sealed class HunterCasterAmmoProviderSystem : EntitySystem
{
    [Dependency] private readonly HunterEnergySystem _hunterEnergy = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterCasterAmmoProviderComponent, GetAmmoCountEvent>(OnBatteryAmmoCount);
        SubscribeLocalEvent<HunterCasterAmmoProviderComponent, TakeAmmoEvent>(OnBatteryTakeAmmo);
        SubscribeLocalEvent<HunterCasterAmmoProviderComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<HunterCasterAmmoProviderComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnGetState(EntityUid uid, HunterCasterAmmoProviderComponent component, ref ComponentGetState args)
    {
        args.State = new HunterCasterAmmoProviderComponentState(component.Shots, component.Capacity, component.FireCost);
    }

    private void OnHandleState(EntityUid uid, HunterCasterAmmoProviderComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not HunterCasterAmmoProviderComponentState state)
            return;

        component.Shots = state.Shots;
        component.Capacity = state.Capacity;
        component.FireCost = state.FireCost;
    }

    private void OnBatteryAmmoCount(EntityUid uid, HunterCasterAmmoProviderComponent component, ref GetAmmoCountEvent args)
    {
        if (TryGetHunterEnergy(uid, out var energy))
        {
            component.Capacity = (int) (energy.MaxEnergy.Float() / component.FireCost);
            component.Shots = (int) (energy.Energy.Float() / component.FireCost);
            Dirty(uid, component);
        }

        args.Count = component.Shots;
        args.Capacity = component.Capacity;
    }

    private void OnBatteryTakeAmmo(EntityUid uid, HunterCasterAmmoProviderComponent component, TakeAmmoEvent args)
    {
        // Recalculate shots from the actual hunter energy before checking
        if (TryGetHunterEnergy(uid, out var energyComp, out var energyOwner, args.User))
        {
            component.Shots = (int) (energyComp.Energy.Float() / component.FireCost);
            component.Capacity = (int) (energyComp.MaxEnergy.Float() / component.FireCost);
        }

        var shots = Math.Min(args.Shots, component.Shots);

        if (shots == 0)
            return;

        for (var i = 0; i < shots; i++)
        {
            if (component.Prototype == null)
                continue;

            var ent = Spawn(component.Prototype.Value, args.Coordinates);
            args.Ammo.Add((ent, EnsureShootable(ent)));
        }

        component.Shots -= shots;

        if (energyComp != null)
        {
            var cost = component.FireCost * shots;
            _hunterEnergy.TryUseEnergy((energyOwner, energyComp), cost);
        }
        
        Dirty(uid, component);
    }

    private IShootable EnsureShootable(EntityUid uid)
    {
        if (TryComp<CartridgeAmmoComponent>(uid, out var cartridge))
            return cartridge;

        return EnsureComp<AmmoComponent>(uid);
    }

    private bool TryGetHunterEnergy(EntityUid casterUid, [NotNullWhen(true)] out HunterEnergyComponent? energy)
    {
        return TryGetHunterEnergy(casterUid, out energy, out _);
    }

    private bool TryGetHunterEnergy(EntityUid casterUid, [NotNullWhen(true)] out HunterEnergyComponent? energy, out EntityUid energyUid, EntityUid? user = null)
    {
        energy = null;
        energyUid = default;

        if (user != null && TryComp<HunterEnergyComponent>(user.Value, out energy))
        {
            energyUid = user.Value;
            return true;
        }

        var parent = Transform(casterUid).ParentUid;
        if (parent.IsValid() && TryComp<HunterEnergyComponent>(parent, out energy))
        {
            energyUid = parent;
            return true;
        }

        return false;
    }
}

