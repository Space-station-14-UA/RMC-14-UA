using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Armor;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Clothing;
using Content.Shared.Inventory;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Predicted half of Contact perks: movement speed, gun handling, xeno armor, weapon restrictions.
/// Damage, crits, caps and health live in the server's ContactPerkCombatSystem.
/// </summary>
public sealed class ContactPerkSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private static readonly ProtoId<TagPrototype> PistolTag = "Sidearm";
    private static readonly TimeSpan RestrictPopupDelay = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        SubscribeLocalEvent<ContactPerksComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<ContactPerksComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ContactPerksComponent, CMGetArmorEvent>(OnGetArmor);
        SubscribeLocalEvent<ContactPerksComponent, DidEquipHandEvent>(OnEquipHand);
        SubscribeLocalEvent<ContactPerksComponent, DidUnequipHandEvent>(OnUnequipHand);
        SubscribeLocalEvent<ContactPerksComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<ContactPerkGunComponent, GunRefreshModifiersEvent>(OnGunRefresh);
    }

    #region API

    /// <summary>
    /// Server: replace the perk set and refresh everything predicted. Health is done by ContactPerkCombatSystem.
    /// </summary>
    public void SetPerks(EntityUid uid, IEnumerable<ProtoId<ContactPerkPrototype>> perks)
    {
        var comp = EnsureComp<ContactPerksComponent>(uid);
        comp.Perks = new HashSet<ProtoId<ContactPerkPrototype>>(perks);
        Dirty(uid, comp);
        RefreshAll(uid);
    }

    public IEnumerable<T> GetEffects<T>(ContactPerksComponent perks) where T : ContactPerkEffect
    {
        foreach (var id in perks.Perks)
        {
            if (!_prototypes.TryIndex(id, out var perk))
                continue;

            foreach (var effect in perk.Effects)
            {
                if (effect is T typed)
                    yield return typed;
            }
        }
    }

    public ContactPerkWeapon ClassifyGun(EntityUid gun)
    {
        return _tag.HasTag(gun, PistolTag) ? ContactPerkWeapon.Pistol : ContactPerkWeapon.Firearm;
    }

    public static bool Matches(ContactPerkWeapon filter, ContactPerkWeapon used)
    {
        return filter switch
        {
            ContactPerkWeapon.Any => true,
            ContactPerkWeapon.Firearm => used is ContactPerkWeapon.Firearm or ContactPerkWeapon.Pistol,
            _ => filter == used,
        };
    }

    /// <summary>
    /// Whoever holds / wears this gun, if they have perks (hands, suit storage, back...).
    /// </summary>
    public bool TryGetHolder(EntityUid gun, out EntityUid holder, [NotNullWhen(true)] out ContactPerksComponent? perks)
    {
        holder = default;
        perks = null;
        if (!_container.TryGetContainingContainer((gun, null, null), out var container))
            return false;

        holder = container.Owner;
        return TryComp(holder, out perks);
    }

    #endregion

    private void RefreshAll(EntityUid uid)
    {
        _speed.RefreshMovementSpeedModifiers(uid);
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            RefreshGun(held);
        }
    }

    private void RefreshGun(EntityUid item)
    {
        if (!HasComp<GunComponent>(item))
            return;

        EnsureComp<ContactPerkGunComponent>(item);
        _gun.RefreshModifiers(item);
    }

    private void OnAfterState(Entity<ContactPerksComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshAll(ent);
    }

    private void OnRefreshSpeed(Entity<ContactPerksComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var multiplier = 1f;
        float? slowdown = null;
        foreach (var speed in GetEffects<ContactPerkSpeed>(ent.Comp))
        {
            if (speed.MinArmorSlowdown != null || speed.MaxArmorSlowdown != null)
            {
                slowdown ??= GetArmorSlowdown(ent);
                if (speed.MinArmorSlowdown is { } min && slowdown < min ||
                    speed.MaxArmorSlowdown is { } max && slowdown >= max)
                {
                    continue;
                }
            }

            multiplier *= speed.Multiplier;
        }

        if (multiplier != 1f)
            args.ModifySpeed(multiplier, multiplier);
    }

    /// <summary>
    /// How much worn clothing slows: 0.275 for light armor (0.725), 0.36 for heavy (0.64).
    /// </summary>
    private float GetArmorSlowdown(EntityUid uid)
    {
        var modifier = 1f;
        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.MoveNext(out var container))
        {
            if (container.ContainedEntity is { } item && TryComp(item, out ClothingSpeedModifierComponent? clothing))
                modifier *= clothing.SprintModifier;
        }

        return 1f - modifier;
    }

    private void OnGetArmor(Entity<ContactPerksComponent> ent, ref CMGetArmorEvent args)
    {
        foreach (var armor in GetEffects<ContactPerkXenoArmor>(ent.Comp))
        {
            args.Melee += armor.Amount;
        }
    }

    private void OnEquipHand(Entity<ContactPerksComponent> ent, ref DidEquipHandEvent args)
    {
        RefreshGun(args.Equipped);
    }

    private void OnUnequipHand(Entity<ContactPerksComponent> ent, ref DidUnequipHandEvent args)
    {
        if (HasComp<ContactPerkGunComponent>(args.Unequipped))
            _gun.RefreshModifiers(args.Unequipped);
    }

    private void OnShotAttempted(Entity<ContactPerksComponent> ent, ref ShotAttemptedEvent args)
    {
        var used = ClassifyGun(args.Used);
        foreach (var restrict in GetEffects<ContactPerkRestrict>(ent.Comp))
        {
            var blocked = restrict.Allowed switch
            {
                ContactPerkWeapon.Pistol => used != ContactPerkWeapon.Pistol,
                ContactPerkWeapon.Melee => true,
                _ => false,
            };

            if (!blocked)
                continue;

            args.Cancel();

            if (_timing.CurTime >= ent.Comp.NextRestrictPopup)
            {
                ent.Comp.NextRestrictPopup = _timing.CurTime + RestrictPopupDelay;
                _popup.PopupClient(Loc.GetString("mriya-contact-perk-restricted"), ent, ent, PopupType.SmallCaution);
            }

            return;
        }
    }

    private void OnGunRefresh(Entity<ContactPerkGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!TryGetHolder(ent, out _, out var perks))
            return;

        var used = ClassifyGun(ent);
        foreach (var stats in GetEffects<ContactPerkGunStats>(perks))
        {
            if (!Matches(stats.Weapon, used))
                continue;

            args.FireRate *= stats.FireRate;
            args.CameraRecoilScalar *= stats.Recoil;
            args.AngleIncrease = new Angle(args.AngleIncrease.Theta * stats.Spread);
            args.MaxAngle = new Angle(args.MaxAngle.Theta * stats.Spread);
            args.MinAngle = new Angle(args.MinAngle.Theta * stats.Spread);
        }
    }
}
