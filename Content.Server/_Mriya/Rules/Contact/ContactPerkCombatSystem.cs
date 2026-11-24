using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Server half of Contact perks: damage bonuses, crits and lifesteal against wave xenos,
/// the xeno damage cap, no-falloff shots and max health.
/// </summary>
public sealed class ContactPerkCombatSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ContactPerkSystem _perks = default!;
    [Dependency] private readonly ContactRuleSystem _rule = default!;
    [Dependency] private readonly ContactEconomySystem _economy = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<DamageGroupPrototype> HealGroup = "Brute";

    public override void Initialize()
    {
        SubscribeLocalEvent<ContactXenoAIComponent, DamageModifyEvent>(OnXenoDamageModify);
        SubscribeLocalEvent<ContactPerksComponent, DamageModifyEvent>(OnPerkOwnerDamageModify);
        SubscribeLocalEvent<ContactPerkGunComponent, AmmoShotEvent>(OnAmmoShot);
    }

    #region Dealing damage

    private void OnXenoDamageModify(Entity<ContactXenoAIComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } attacker ||
            !TryComp(attacker, out ContactPerksComponent? perks) ||
            perks.Perks.Count == 0 ||
            !TryClassify(attacker, args.Tool, out var used))
        {
            return;
        }

        var distance = Distance(attacker, ent);
        var multiplier = 1f;

        foreach (var bonus in _perks.GetEffects<ContactPerkDamage>(perks))
        {
            if (!ContactPerkSystem.Matches(bonus.Weapon, used) ||
                bonus.MinRange is { } min && distance < min ||
                bonus.MaxRange is { } max && distance > max ||
                bonus.BelowMaxHealth && !IsHurt(attacker) ||
                bonus.MinBalance > 0 && _economy.GetBalance(attacker) < bonus.MinBalance)
            {
                continue;
            }

            multiplier *= bonus.Multiplier;
        }

        var critChance = 0f;
        var critMultiplier = 1f;
        foreach (var crit in _perks.GetEffects<ContactPerkCrit>(perks))
        {
            if (!ContactPerkSystem.Matches(crit.Weapon, used))
                continue;

            critChance += crit.Chance;
            critMultiplier = Math.Max(critMultiplier, crit.Multiplier);
        }

        if (critChance > 0 && _random.Prob(Math.Min(1f, critChance)))
            multiplier *= critMultiplier;

        if (multiplier != 1f)
            args.Damage *= multiplier;

        var steal = 0f;
        foreach (var lifesteal in _perks.GetEffects<ContactPerkLifesteal>(perks))
        {
            if (ContactPerkSystem.Matches(lifesteal.Weapon, used))
                steal += lifesteal.Fraction;
        }

        var heal = args.Damage.GetTotal().Float() * steal;
        if (heal > 0)
            _damageable.TryChangeDamage(attacker, new DamageSpecifier(_prototypes.Index(HealGroup), -heal), true);
    }

    /// <summary>
    /// Projectile from a gun = firearm / pistol; a melee weapon or bare hands = melee.
    /// Anything else (no tool: fire, explosions...) isn't classified and gets no perks.
    /// </summary>
    private bool TryClassify(EntityUid attacker, EntityUid? tool, out ContactPerkWeapon used)
    {
        used = default;
        if (tool is not { } item)
            return false;

        if (TryComp(item, out ProjectileComponent? projectile))
        {
            if (projectile.Weapon is not { } gun || !HasComp<GunComponent>(gun))
                return false;

            used = _perks.ClassifyGun(gun);
            return true;
        }

        if (item == attacker || HasComp<MeleeWeaponComponent>(item))
        {
            used = ContactPerkWeapon.Melee;
            return true;
        }

        return false;
    }

    private float Distance(EntityUid a, EntityUid b)
    {
        var posA = _transform.GetMapCoordinates(a);
        var posB = _transform.GetMapCoordinates(b);
        return posA.MapId == posB.MapId ? (posA.Position - posB.Position).Length() : float.MaxValue;
    }

    private bool IsHurt(EntityUid uid)
    {
        return TryComp(uid, out DamageableComponent? damageable) && damageable.TotalDamage > 0;
    }

    /// <summary>
    /// Shots from these guns lose their falloff. Deferred, so RMC's own shot handlers run first.
    /// </summary>
    private void OnAmmoShot(Entity<ContactPerkGunComponent> ent, ref AmmoShotEvent args)
    {
        if (!_perks.TryGetHolder(ent, out _, out var perks))
            return;

        var used = _perks.ClassifyGun(ent);
        var noFalloff = false;
        foreach (var effect in _perks.GetEffects<ContactPerkNoFalloff>(perks))
        {
            if (ContactPerkSystem.Matches(effect.Weapon, used))
                noFalloff = true;
        }

        if (!noFalloff)
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            RemCompDeferred<RMCProjectileDamageFalloffComponent>(projectile);
        }
    }

    #endregion

    #region Taking damage

    /// <summary>
    /// Damage cap against direct xeno damage (the xeno is the origin: claws, tail, spit, abilities).
    /// </summary>
    private void OnPerkOwnerDamageModify(Entity<ContactPerksComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } origin || !HasComp<XenoComponent>(origin))
            return;

        float? cap = null;
        foreach (var effect in _perks.GetEffects<ContactPerkDamageCap>(ent.Comp))
        {
            var limit = effect.Cap + effect.PerBuffLevel * _rule.GetBuffLevel();
            cap = cap is { } current ? Math.Min(current, limit) : limit;
        }

        if (cap is not { } max)
            return;

        var total = args.Damage.GetTotal().Float();
        if (total > max && total > 0)
            args.Damage *= max / total;
    }

    #endregion

    #region Health

    /// <summary>
    /// Re-applies max health perks from the thresholds the body had before any perk.
    /// Bonus adds to the crit threshold, SetTo replaces it; the dead threshold keeps its gap to crit.
    /// </summary>
    public void RefreshHealth(EntityUid uid)
    {
        if (!TryComp(uid, out ContactPerksComponent? perks) || !TryComp(uid, out MobThresholdsComponent? thresholds))
            return;

        if (perks.BaseCritical == null || perks.BaseDead == null)
        {
            if (!_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit, thresholds) ||
                !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead, thresholds))
            {
                return;
            }

            perks.BaseCritical = crit;
            perks.BaseDead = dead;
        }

        var baseCrit = perks.BaseCritical.Value;
        var gap = perks.BaseDead.Value - baseCrit;

        var bonus = 0f;
        float? setTo = null;
        foreach (var health in _perks.GetEffects<ContactPerkHealth>(perks))
        {
            bonus += health.Bonus;
            if (health.SetTo is { } value)
                setTo = setTo is { } current ? Math.Min(current, value) : value;
        }

        var newCrit = setTo is { } fixedMax ? FixedPoint2.New(fixedMax) : baseCrit + FixedPoint2.New(bonus);
        var newDead = newCrit + gap;

        // never let crit pass dead while moving them
        _thresholds.TryGetThresholdForState(uid, MobState.Critical, out var oldCrit, thresholds);
        if (oldCrit is { } previous && newCrit > previous)
        {
            _thresholds.SetMobStateThreshold(uid, newDead, MobState.Dead, thresholds);
            _thresholds.SetMobStateThreshold(uid, newCrit, MobState.Critical, thresholds);
        }
        else
        {
            _thresholds.SetMobStateThreshold(uid, newCrit, MobState.Critical, thresholds);
            _thresholds.SetMobStateThreshold(uid, newDead, MobState.Dead, thresholds);
        }
    }

    #endregion
}
