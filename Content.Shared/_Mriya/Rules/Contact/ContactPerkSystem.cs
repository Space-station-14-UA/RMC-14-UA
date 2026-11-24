using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.Armor;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Shared.Whitelist;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Predicted half of Contact perks: movement speed (and speed stacks), gun handling, melee armor, weapon restrictions.
/// Damage, crits, kills, caps and health live in the server's ContactPerkCombatSystem.
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
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private static readonly ProtoId<TagPrototype> PistolTag = "Sidearm";
    private static readonly TimeSpan RestrictPopupDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(0.1);

    /// <summary>
    /// Below this speed counts as standing still.
    /// </summary>
    private const float StillSpeed = 0.1f;

    private TimeSpan _nextCheck;

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
        comp.SpeedStacks.Clear();
        comp.KillCount = 0;
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

    public bool HasEffect<T>(ContactPerksComponent perks) where T : ContactPerkEffect
    {
        foreach (var _ in GetEffects<T>(perks))
        {
            return true;
        }

        return false;
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

    /// <summary>
    /// Server: one more speed stack from this source; at MaxStacks the oldest one is replaced.
    /// </summary>
    public void AddSpeedStack(EntityUid uid, ContactPerksComponent perks, float multiplier, TimeSpan duration, string source, int maxStacks)
    {
        var count = 0;
        var oldest = -1;
        for (var i = 0; i < perks.SpeedStacks.Count; i++)
        {
            if (perks.SpeedStacks[i].Source != source)
                continue;

            count++;
            if (oldest == -1 || perks.SpeedStacks[i].Until < perks.SpeedStacks[oldest].Until)
                oldest = i;
        }

        if (count >= maxStacks && oldest != -1)
            perks.SpeedStacks.RemoveAt(oldest);

        perks.SpeedStacks.Add(new ContactSpeedStack(_timing.CurTime + duration, multiplier, source));
        Dirty(uid, perks);
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    #endregion

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckEvery;

        var query = EntityQueryEnumerator<ContactPerksComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var perks, out var physics))
        {
            // speed stacks running out
            if (perks.SpeedStacks.RemoveAll(s => s.Until <= now) > 0)
            {
                Dirty(uid, perks);
                _speed.RefreshMovementSpeedModifiers(uid);
            }

            // standing still / moving: guns with StandingStill bonuses refresh
            var still = physics.LinearVelocity.Length() < StillSpeed;
            if (still == perks.StandingStill)
                continue;

            perks.StandingStill = still;
            if (HasStandingStillStats(perks))
                RefreshHeldGuns(uid);
        }
    }

    private bool HasStandingStillStats(ContactPerksComponent perks)
    {
        foreach (var stats in GetEffects<ContactPerkGunStats>(perks))
        {
            if (stats.StandingStill)
                return true;
        }

        return false;
    }

    private void RefreshAll(EntityUid uid)
    {
        _speed.RefreshMovementSpeedModifiers(uid);
        RefreshHeldGuns(uid);
    }

    private void RefreshHeldGuns(EntityUid uid)
    {
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

        var now = _timing.CurTime;
        foreach (var stack in ent.Comp.SpeedStacks)
        {
            if (stack.Until > now)
                multiplier *= stack.Multiplier;
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

    /// <summary>
    /// Marines resist xeno claws with RMC's Melee armor (XenoArmor only counts on xenos).
    /// </summary>
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
        var wielded = TryComp(ent, out WieldableComponent? wieldable) && wieldable.Wielded;
        foreach (var stats in GetEffects<ContactPerkGunStats>(perks))
        {
            if (!Matches(stats.Weapon, used) ||
                stats.StandingStill && !perks.StandingStill ||
                stats.Unwielded && wielded)
            {
                continue;
            }

            args.FireRate *= stats.FireRate;
            args.CameraRecoilScalar *= stats.Recoil;
            args.AngleIncrease = new Angle(args.AngleIncrease.Theta * stats.Spread);
            args.MaxAngle = new Angle(args.MaxAngle.Theta * stats.Spread);
            args.MinAngle = new Angle(args.MinAngle.Theta * stats.Spread);
        }
    }

    /// <summary>
    /// Do-after length after perks. Called by Mriya systems that start do-afters perks should affect
    /// (anchor activation). Whitelist = only when the used item or target passes it.
    /// </summary>
    public TimeSpan ModifyDoAfterDelay(EntityUid user, TimeSpan delay, EntityUid? used, EntityUid? target)
    {
        if (!TryComp(user, out ContactPerksComponent? perks))
            return delay;

        TimeSpan? cap = null;
        foreach (var speed in GetEffects<ContactPerkDoAfterSpeed>(perks))
        {
            if (speed.Whitelist is { } whitelist &&
                !(used is { } u && _whitelist.IsWhitelistPass(whitelist, u)) &&
                !(target is { } t && _whitelist.IsWhitelistPass(whitelist, t)))
            {
                continue;
            }

            delay *= speed.Multiplier;
            if (speed.SetSeconds is { } seconds)
            {
                var limit = TimeSpan.FromSeconds(seconds);
                cap = cap is { } current && current < limit ? current : limit;
            }
        }

        return cap is { } max && delay > max ? max : delay;
    }
}
