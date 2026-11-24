using Content.Server.Explosion.EntitySystems;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.NightVision;
using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids.Neurotoxin;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Damage;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Server half of Contact perks: damage bonuses, crits, first shots, cleave and lifesteal against Contact xenos,
/// kill effects (blast, heal, credits, speed), the xeno damage cap and hurt speed, no-falloff shots, max health
/// and free store purchases.
/// </summary>
public sealed class ContactPerkCombatSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ContactPerkSystem _perks = default!;
    [Dependency] private readonly ContactRuleSystem _rule = default!;
    [Dependency] private readonly ContactEconomySystem _economy = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedHandheldLightSystem _light = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private static readonly ProtoId<DamageGroupPrototype> DamageGroup = "Brute";

    /// <summary>
    /// A kill counts for whoever hit last within this window.
    /// </summary>
    private static readonly TimeSpan KillCreditWindow = TimeSpan.FromSeconds(5);

    private const string DazedEffect = "Dazed";
    private static readonly TimeSpan CleanseEvery = TimeSpan.FromSeconds(0.2);
    private TimeSpan _nextCleanse;

    public override void Initialize()
    {
        // every Contact AI xeno (wave or admin-spawned)
        SubscribeLocalEvent<ContactXenoAIComponent, DamageModifyEvent>(OnXenoDamageModify);
        SubscribeLocalEvent<ContactXenoAIComponent, MobStateChangedEvent>(OnXenoMobStateChanged, before: [typeof(RMCGibSystem)]);
        SubscribeLocalEvent<ContactPerksComponent, DamageModifyEvent>(OnPerkOwnerDamageModify);
        SubscribeLocalEvent<ContactPerksComponent, NeurotoxinInjectAttemptEvent>(OnNeurotoxinAttempt);
        SubscribeLocalEvent<ContactPerkGunComponent, AmmoShotEvent>(OnAmmoShot);
    }

    /// <summary>
    /// Call after the perk set changed: max health and perk-given night vision.
    /// </summary>
    public void Refresh(EntityUid uid)
    {
        RefreshHealth(uid);
        RefreshNightVision(uid);
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

        // who gets the kill effects if this ends up killing it
        var lastHit = EnsureComp<ContactPerkLastHitComponent>(ent);
        lastHit.Attacker = attacker;
        lastHit.Weapon = used;
        lastHit.At = _timing.CurTime;

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

        foreach (var lowHealth in _perks.GetEffects<ContactPerkLowHealthDamage>(perks))
        {
            if (ContactPerkSystem.Matches(lowHealth.Weapon, used))
                multiplier *= LowHealthMultiplier(attacker, perks, lowHealth);
        }

        // first shot after a pause: guaranteed crit instead of rolling
        if (args.Tool is { } tool && TryComp(tool, out ContactPerkFirstShotComponent? firstShot))
        {
            multiplier *= firstShot.Multiplier;
            ShowCrit(attacker, ent);
        }
        else
        {
            var critChance = 0f;
            var critMultiplier = 1f;
            foreach (var crit in _perks.GetEffects<ContactPerkCrit>(perks))
            {
                if (!ContactPerkSystem.Matches(crit.Weapon, used) ||
                    crit.MinRange is { } critMin && distance < critMin)
                {
                    continue;
                }

                critChance += crit.Chance;
                critMultiplier = Math.Max(critMultiplier, crit.Multiplier);
            }

            if (critChance > 0 && _random.Prob(Math.Min(1f, critChance)))
            {
                multiplier *= critMultiplier;
                ShowCrit(attacker, ent);
            }
        }

        if (multiplier != 1f)
            args.Damage *= multiplier;

        var dealt = args.Damage.GetTotal().Float();

        var steal = 0f;
        foreach (var lifesteal in _perks.GetEffects<ContactPerkLifesteal>(perks))
        {
            if (ContactPerkSystem.Matches(lifesteal.Weapon, used))
                steal += lifesteal.Fraction;
        }

        Heal(attacker, dealt * steal);

        foreach (var cleave in _perks.GetEffects<ContactPerkCleave>(perks))
        {
            if (ContactPerkSystem.Matches(cleave.Weapon, used))
                DamageXenosAround(_transform.GetMapCoordinates(ent), cleave.Radius, dealt * cleave.Fraction, attacker, ent);
        }
    }

    /// <summary>
    /// Projectile from a gun = firearm / pistol; a melee weapon or bare hands = melee.
    /// Anything else (no tool: fire, explosions, perk blasts and cleave) isn't classified and gets no perks.
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

    /// <summary>
    /// Health fraction = 1 - damage / crit threshold; the lowest step it's at or under applies.
    /// </summary>
    private float LowHealthMultiplier(EntityUid uid, ContactPerksComponent perks, ContactPerkLowHealthDamage effect)
    {
        var steps = effect.ComboPerk is { } combo && perks.Perks.Contains(combo) && effect.ComboSteps != null
            ? effect.ComboSteps
            : effect.Steps;

        var fraction = HealthFraction(uid);
        var best = 1f;
        var bestStep = float.MaxValue;
        foreach (var (below, multiplier) in steps)
        {
            if (fraction <= below && below < bestStep)
            {
                best = multiplier;
                bestStep = below;
            }
        }

        return best;
    }

    /// <summary>
    /// Shots from these guns lose their falloff (deferred, so RMC's own shot handlers run first),
    /// and the first shot after a pause is marked for its guaranteed crit.
    /// </summary>
    private void OnAmmoShot(Entity<ContactPerkGunComponent> ent, ref AmmoShotEvent args)
    {
        if (!_perks.TryGetHolder(ent, out _, out var perks))
            return;

        var used = _perks.ClassifyGun(ent);
        var now = _timing.CurTime;
        var idle = now - perks.LastShot;
        perks.LastShot = now;

        var noFalloff = false;
        foreach (var effect in _perks.GetEffects<ContactPerkNoFalloff>(perks))
        {
            if (ContactPerkSystem.Matches(effect.Weapon, used))
                noFalloff = true;
        }

        float? firstShot = null;
        var piercing = false;
        foreach (var effect in _perks.GetEffects<ContactPerkFirstShot>(perks))
        {
            if (!ContactPerkSystem.Matches(effect.Weapon, used) || idle < effect.Idle)
                continue;

            firstShot = Math.Max(firstShot ?? 0f, effect.Multiplier);
            piercing |= effect.Piercing;
        }

        foreach (var projectile in args.FiredProjectiles)
        {
            if (noFalloff)
                RemCompDeferred<RMCProjectileDamageFalloffComponent>(projectile);

            if (firstShot is { } multiplier)
                EnsureComp<ContactPerkFirstShotComponent>(projectile).Multiplier = multiplier;

            if (piercing)
                MakePiercing(projectile);
        }
    }

    #endregion

    #region Kills

    private void OnXenoMobStateChanged(Entity<ContactXenoAIComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var victimPos = _transform.GetMapCoordinates(ent);
        HealAround(victimPos);

        if (!TryComp(ent, out ContactPerkLastHitComponent? lastHit) ||
            _timing.CurTime - lastHit.At > KillCreditWindow ||
            TerminatingOrDeleted(lastHit.Attacker) ||
            _mobState.IsDead(lastHit.Attacker) ||
            !TryComp(lastHit.Attacker, out ContactPerksComponent? perks))
        {
            return;
        }

        var killer = lastHit.Attacker;
        var used = lastHit.Weapon;

        foreach (var heal in _perks.GetEffects<ContactPerkKillHeal>(perks))
        {
            if (!heal.AnyKiller && ContactPerkSystem.Matches(heal.Weapon, used))
                Heal(killer, Roll(heal));
        }

        foreach (var credits in _perks.GetEffects<ContactPerkKillCredits>(perks))
        {
            if (ContactPerkSystem.Matches(credits.Weapon, used))
                _economy.AddPoints(killer, credits.Amount);
        }

        foreach (var speed in _perks.GetEffects<ContactPerkKillSpeed>(perks))
        {
            if (ContactPerkSystem.Matches(speed.Weapon, used))
                _perks.AddSpeedStack(killer, perks, speed.Multiplier, speed.Duration, "kill", speed.MaxStacks);
        }

        if (_perks.HasEffect<ContactPerkKillBlast>(perks))
        {
            perks.KillCount++;
            foreach (var blast in _perks.GetEffects<ContactPerkKillBlast>(perks))
            {
                if (perks.KillCount < blast.Kills)
                    continue;

                perks.KillCount = 0;
                Blast(blast, victimPos, Transform(ent).Coordinates);

                var damage = blast.Damage * (1f + blast.PerBuffLevel * _rule.GetBuffLevel());
                DamageXenosAround(victimPos, blast.Radius, damage, killer, ent);
                break;
            }
        }
    }

    /// <summary>
    /// Any xeno death: perk owners with an AnyKiller heal (Hellion HUD) within its range get healed.
    /// </summary>
    private void HealAround(MapCoordinates victim)
    {
        var query = EntityQueryEnumerator<ContactPerksComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var perks, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            var pos = _transform.GetMapCoordinates(uid, xform);
            if (pos.MapId != victim.MapId)
                continue;

            var distance = (pos.Position - victim.Position).Length();
            foreach (var heal in _perks.GetEffects<ContactPerkKillHeal>(perks))
            {
                if (heal.AnyKiller && (heal.MaxRange is not { } range || distance <= range))
                    Heal(uid, Roll(heal));
            }
        }
    }

    private float Roll(ContactPerkKillHeal heal)
    {
        return heal.Max is { } max && max > heal.Min ? _random.NextFloat(heal.Min, max) : heal.Min;
    }

    /// <summary>
    /// Perk damage to living Contact xenos around a point (blasts, cleave). No tool, so it doesn't trigger perks again.
    /// </summary>
    private void DamageXenosAround(MapCoordinates center, float radius, float amount, EntityUid attacker, EntityUid except)
    {
        if (amount <= 0)
            return;

        var damage = new DamageSpecifier(_prototypes.Index(DamageGroup), FixedPoint2.New(amount));
        foreach (var xeno in _lookup.GetEntitiesInRange<ContactXenoAIComponent>(center, radius))
        {
            if (xeno.Owner == except || _mobState.IsDead(xeno))
                continue;

            _damageable.TryChangeDamage(xeno, damage, origin: attacker);
        }
    }

    #endregion

    #region Taking damage

    /// <summary>
    /// Direct xeno damage (the xeno is the origin: claws, tail, spit, abilities): damage cap, then hurt speed stacks.
    /// </summary>
    private void OnPerkOwnerDamageModify(Entity<ContactPerksComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } origin || !HasComp<XenoComponent>(origin) || !args.Damage.AnyPositive())
            return;

        if (args.Origin == null && IsInImmuneGas(ent))
        {
            args.Damage = DamageSpecifier.GetNegative(args.Damage);
            return;
        }

        float? cap = null;
        foreach (var effect in _perks.GetEffects<ContactPerkDamageCap>(ent.Comp))
        {
            var limit = effect.Cap + effect.PerBuffLevel * _rule.GetBuffLevel();
            cap = cap is { } current ? Math.Min(current, limit) : limit;
        }

        var total = args.Damage.GetTotal().Float();
        if (cap is { } max && total > max && total > 0)
            args.Damage *= max / total;

        foreach (var speed in _perks.GetEffects<ContactPerkHurtSpeed>(ent.Comp))
        {
            _perks.AddSpeedStack(ent, ent.Comp, speed.Multiplier, speed.Duration, "hurt", speed.MaxStacks);
        }
    }

    #endregion

    #region Immunities

    private bool IsInImmuneGas(Entity<ContactPerksComponent> ent)
    {
        if (!HasComp<UserDamageOverTimeComponent>(ent))
            return false;

        foreach (var immunity in _perks.GetEffects<ContactPerkGasImmunity>(ent.Comp))
        {
            foreach (var contact in _physics.GetEntitiesIntersectingBody(ent, (int) (CollisionGroup.MobLayer | CollisionGroup.MobMask)))
            {
                if (HasComp<DamageOverTimeComponent>(contact) &&
                    MetaData(contact).EntityPrototype?.ID is { } proto &&
                    immunity.Gases.Contains(proto))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OnNeurotoxinAttempt(Entity<ContactPerksComponent> ent, ref NeurotoxinInjectAttemptEvent args)
    {
        if (_perks.HasEffect<ContactPerkNeurotoxinImmunity>(ent.Comp))
            args.Cancelled = true;
    }

    /// <summary>
    /// Slows and daze are applied from many places; instead of hooking each, perk owners are cleansed a few times a second.
    /// </summary>
    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCleanse)
            return;

        _nextCleanse = now + CleanseEvery;

        var query = EntityQueryEnumerator<ContactPerksComponent>();
        while (query.MoveNext(out var uid, out var perks))
        {
            if (perks.Perks.Count == 0)
                continue;

            if (_perks.HasEffect<ContactPerkSlowImmunity>(perks))
            {
                if (HasComp<RMCSlowdownComponent>(uid))
                    RemCompDeferred<RMCSlowdownComponent>(uid);

                if (HasComp<RMCSuperSlowdownComponent>(uid))
                    RemCompDeferred<RMCSuperSlowdownComponent>(uid);
            }

            if (_perks.HasEffect<ContactPerkDazeImmunity>(perks) && _statusEffects.HasStatusEffect(uid, DazedEffect))
                _statusEffects.TryRemoveStatusEffect(uid, DazedEffect);

            if (_perks.HasEffect<ContactPerkNoFlashlight>(perks))
                TurnOffLights(uid, uid, 0);
        }
    }

    /// <summary>
    /// Hands, worn items, storage, gun attachments: anything lit gets switched off.
    /// </summary>
    private void TurnOffLights(EntityUid owner, EntityUid uid, int depth)
    {
        if (depth > 4 || !TryComp(uid, out ContainerManagerComponent? manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (TryComp(item, out HandheldLightComponent? light) && light.Activated && _light.TurnOff((item, light)))
                    _popup.PopupEntity(Loc.GetString("mriya-contact-perk-no-flashlight"), owner, owner, PopupType.SmallCaution);

                TurnOffLights(owner, item, depth + 1);
            }
        }
    }

    /// <summary>
    /// Night vision while a perk gives it; only removes what a perk added (goggles / innate NV stay).
    /// </summary>
    private void RefreshNightVision(EntityUid uid)
    {
        if (!TryComp(uid, out ContactPerksComponent? perks))
            return;

        var wants = _perks.HasEffect<ContactPerkNightVision>(perks);
        if (wants && !HasComp<NightVisionComponent>(uid))
        {
            AddComp<NightVisionComponent>(uid);
            perks.GaveNightVision = true;
        }
        else if (!wants && perks.GaveNightVision)
        {
            RemComp<NightVisionComponent>(uid);
            perks.GaveNightVision = false;
        }
    }

    #endregion

    #region Store

    /// <summary>
    /// Called by the store before charging: true = this purchase is free.
    /// </summary>
    public bool RollFreePurchase(EntityUid user)
    {
        if (!TryComp(user, out ContactPerksComponent? perks))
            return false;

        var chance = 0f;
        foreach (var effect in _perks.GetEffects<ContactPerkFreePurchase>(perks))
        {
            chance += effect.Chance;
        }

        return chance > 0 && _random.Prob(Math.Min(1f, chance));
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

    #region Helpers

    /// <summary>
    /// "CRIT!" over the target, shown to the attacker.
    /// </summary>
    private void ShowCrit(EntityUid attacker, EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString("mriya-contact-perk-crit"), target, attacker, PopupType.LargeCaution);
    }

    /// <summary>
    /// Looks and sound of a kill blast. The RMC explosion is small and breaks no tiles; the real damage is DamageXenosAround.
    /// </summary>
    private void Blast(ContactPerkKillBlast blast, MapCoordinates pos, EntityCoordinates coords)
    {
        if (blast.Sound != null)
            _audio.PlayPvs(blast.Sound, coords);

        if (blast.ExplosionType is { } type)
        {
            _explosion.QueueExplosion(pos, type,
                totalIntensity: blast.ExplosionIntensity,
                slope: 1f,
                maxTileIntensity: blast.ExplosionIntensity,
                cause: null,
                tileBreakScale: 0f,
                maxTileBreak: 0,
                canCreateVacuum: false);
        }

        if (blast.Effect is { } effect)
            Spawn(effect, pos);
    }

    /// <summary>
    /// RMC's penetrating projectile with no loss per mob; a wall costs far more range than it has, so walls still stop it.
    /// </summary>
    private void MakePiercing(EntityUid projectile)
    {
        var pierce = EnsureComp<RMCPenetratingProjectileComponent>(projectile);
        pierce.ShotFrom = Transform(projectile).Coordinates;
        pierce.RangeLossPerHit = 0.001f;
        pierce.DamageMultiplierLossPerHit = 0f;
        pierce.WallMultiplier = 100000f;
        pierce.BigXenoMultiplier = 1f;
        Dirty(projectile, pierce);
    }

    private void Heal(EntityUid uid, float amount)
    {
        if (amount <= 0 || _mobState.IsDead(uid))
            return;

        _damageable.TryChangeDamage(uid, new DamageSpecifier(_prototypes.Index(DamageGroup), FixedPoint2.New(-amount)), true);
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
    /// 1 at full health, 0 at the crit threshold.
    /// </summary>
    private float HealthFraction(EntityUid uid)
    {
        if (!TryComp(uid, out DamageableComponent? damageable) ||
            !_thresholds.TryGetThresholdForState(uid, MobState.Critical, out var crit) ||
            crit.Value <= 0)
        {
            return 1f;
        }

        return Math.Clamp(1f - damageable.TotalDamage.Float() / crit.Value.Float(), 0f, 1f);
    }

    #endregion
}

/// <summary>
/// On a Contact xeno: the last perk owner who hit it, for kill effects.
/// </summary>
[RegisterComponent, Access(typeof(ContactPerkCombatSystem))]
public sealed partial class ContactPerkLastHitComponent : Component
{
    [ViewVariables]
    public EntityUid Attacker;

    [ViewVariables]
    public ContactPerkWeapon Weapon;

    [ViewVariables]
    public TimeSpan At;
}

/// <summary>
/// On a projectile fired after a pause (first-shot perks): its hit deals this much more.
/// </summary>
[RegisterComponent, Access(typeof(ContactPerkCombatSystem))]
public sealed partial class ContactPerkFirstShotComponent : Component
{
    [ViewVariables]
    public float Multiplier = 1f;
}
