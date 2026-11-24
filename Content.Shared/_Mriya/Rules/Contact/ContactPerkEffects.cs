using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// What the hit / shot was made with. Pistol = a gun tagged Sidearm (pistols and revolvers).
/// </summary>
public enum ContactPerkWeapon : byte
{
    Any,

    /// <summary>
    /// Any gun, pistols included.
    /// </summary>
    Firearm,

    Pistol,
    Melee,
}

/// <summary>
/// Building blocks for perks, combined in YAML. Ranges are in tiles (4 Creedbreaker studs = 1 tile).
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class ContactPerkEffect;

#region Dealing damage

/// <summary>
/// Damage to Contact xenos, multiplied. Optional range band, "while hurt" and credit conditions.
/// </summary>
public sealed partial class ContactPerkDamage : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public float? MinRange;

    [DataField]
    public float? MaxRange;

    /// <summary>
    /// Only while the user has taken any damage.
    /// </summary>
    [DataField]
    public bool BelowMaxHealth;

    /// <summary>
    /// Only while the user has at least this many є-бали.
    /// </summary>
    [DataField]
    public int MinBalance;
}

/// <summary>
/// Chance per hit to multiply its damage. Chances from several perks add up.
/// MinRange: only hits from at least this far (Big Iron: past the pistol falloff range).
/// </summary>
public sealed partial class ContactPerkCrit : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField(required: true)]
    public float Chance;

    [DataField]
    public float Multiplier = 3f;

    [DataField]
    public float? MinRange;
}

/// <summary>
/// Damage multiplier by the user's remaining health (fraction of the crit threshold).
/// The step with the lowest Below that the health is at or under applies.
/// </summary>
public sealed partial class ContactPerkLowHealthDamage : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Melee;

    /// <summary>
    /// Health fraction (0..1) -> multiplier, e.g. 0.75: 1.25, 0.5: 2.5, 0.25: 5.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<float, float> Steps = new();

    /// <summary>
    /// With this perk too, ComboSteps are used instead (Boundary of Death + Royal Flush).
    /// </summary>
    [DataField]
    public ProtoId<ContactPerkPrototype>? ComboPerk;

    [DataField]
    public Dictionary<float, float>? ComboSteps;
}

/// <summary>
/// First shot after not firing for Idle seconds is a guaranteed hit for Multiplier damage.
/// </summary>
public sealed partial class ContactPerkFirstShot : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Firearm;

    [DataField]
    public TimeSpan Idle = TimeSpan.FromSeconds(1);

    [DataField]
    public float Multiplier = 2.4f;

    /// <summary>
    /// The shot passes through every mob it hits without losing damage (walls still stop it).
    /// </summary>
    [DataField]
    public bool Piercing;
}

/// <summary>
/// A hit also deals Fraction of its damage to other xenos within Radius of the target.
/// </summary>
public sealed partial class ContactPerkCleave : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Melee;

    [DataField(required: true)]
    public float Fraction;

    [DataField]
    public float Radius = 5f;
}

/// <summary>
/// Heals this fraction of the damage dealt to xenos.
/// </summary>
public sealed partial class ContactPerkLifesteal : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField(required: true)]
    public float Fraction;
}

#endregion

#region Kills

/// <summary>
/// Every Kills kills: a blast at the victim, Damage (+PerBuffLevel per Contact buff level, as a fraction)
/// to other xenos within Radius. The damage only hurts xenos; the optional RMC explosion is just for looks
/// (small intensity, it does scratch whoever stands right there).
/// </summary>
public sealed partial class ContactPerkKillBlast : ContactPerkEffect
{
    [DataField]
    public int Kills = 3;

    [DataField]
    public float Damage = 300f;

    [DataField]
    public float PerBuffLevel = 0.2f;

    [DataField]
    public float Radius = 5f;

    [DataField]
    public SoundSpecifier? Sound = new SoundCollectionSpecifier("Explosion");

    /// <summary>
    /// Explosion prototype for the visual (RMC). Null = sound only.
    /// </summary>
    [DataField]
    public string? ExplosionType;

    [DataField]
    public float ExplosionIntensity = 4f;

    /// <summary>
    /// Extra visual entity spawned at the victim. Null = none.
    /// </summary>
    [DataField]
    public string? Effect;
}

/// <summary>
/// Heals between Min and Max when a xeno dies. AnyKiller: any death within MaxRange counts (Hellion HUD);
/// otherwise only own kills with Weapon (Turn and River).
/// </summary>
public sealed partial class ContactPerkKillHeal : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField(required: true)]
    public float Min;

    [DataField]
    public float? Max;

    [DataField]
    public float? MaxRange;

    [DataField]
    public bool AnyKiller;
}

/// <summary>
/// Extra є-бали for own kills with Weapon.
/// </summary>
public sealed partial class ContactPerkKillCredits : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField(required: true)]
    public int Amount;
}

/// <summary>
/// Own kills with Weapon add a speed stack for Duration.
/// </summary>
public sealed partial class ContactPerkKillSpeed : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField]
    public float Multiplier = 1.1f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(5);

    [DataField]
    public int MaxStacks = 5;
}

#endregion

#region Taking damage

/// <summary>
/// No single hit from a xeno (melee, spit, ability) deals more than Cap + PerBuffLevel * Contact buff level.
/// </summary>
public sealed partial class ContactPerkDamageCap : ContactPerkEffect
{
    [DataField(required: true)]
    public float Cap;

    [DataField]
    public float PerBuffLevel;
}

/// <summary>
/// Taking direct xeno damage adds a speed stack for Duration.
/// </summary>
public sealed partial class ContactPerkHurtSpeed : ContactPerkEffect
{
    [DataField]
    public float Multiplier = 1.1f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(1);

    [DataField]
    public int MaxStacks = 5;
}

#endregion

#region Stats

/// <summary>
/// Movement speed. Optional armor condition: slowdown from worn clothing (1 - product of their sprint modifiers),
/// MinArmorSlowdown inclusive, MaxArmorSlowdown exclusive.
/// </summary>
public sealed partial class ContactPerkSpeed : ContactPerkEffect
{
    [DataField(required: true)]
    public float Multiplier;

    [DataField]
    public float? MinArmorSlowdown;

    [DataField]
    public float? MaxArmorSlowdown;
}

/// <summary>
/// Max health: Bonus is added to the crit (and dead) threshold; SetTo replaces it (lowest SetTo wins).
/// </summary>
public sealed partial class ContactPerkHealth : ContactPerkEffect
{
    [DataField]
    public float Bonus;

    [DataField]
    public float? SetTo;
}

/// <summary>
/// RMC melee armor, which is what marines resist xeno claws with.
/// </summary>
public sealed partial class ContactPerkXenoArmor : ContactPerkEffect
{
    [DataField(required: true)]
    public int Amount;
}

/// <summary>
/// Gun handling while held. Below 1 = better for Spread / Recoil (0.4 = 60% improvement), above 1 = faster FireRate.
/// StandingStill / Unwielded: only then (refreshed when it changes).
/// </summary>
public sealed partial class ContactPerkGunStats : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Firearm;

    [DataField]
    public float FireRate = 1f;

    [DataField]
    public float Spread = 1f;

    [DataField]
    public float Recoil = 1f;

    [DataField]
    public bool StandingStill;

    [DataField]
    public bool Unwielded;
}

/// <summary>
/// Projectiles from these guns don't lose damage with distance.
/// </summary>
public sealed partial class ContactPerkNoFalloff : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Firearm;
}

/// <summary>
/// Refuses to shoot other guns: Allowed Pistol = only pistols, Melee = no guns at all.
/// </summary>
public sealed partial class ContactPerkRestrict : ContactPerkEffect
{
    [DataField(required: true)]
    public ContactPerkWeapon Allowed;
}

/// <summary>
/// Chance that a store purchase costs nothing.
/// </summary>
public sealed partial class ContactPerkFreePurchase : ContactPerkEffect
{
    [DataField(required: true)]
    public float Chance;
}

/// <summary>
/// Do-afters take Multiplier as long. SetSeconds: at most this long instead.
/// Whitelist: only do-afters whose used item or target passes it (h.WORLD: the tablet).
/// </summary>
public sealed partial class ContactPerkDoAfterSpeed : ContactPerkEffect
{
    [DataField]
    public float Multiplier = 1f;

    [DataField]
    public float? SetSeconds;

    [DataField]
    public EntityWhitelist? Whitelist;
}

/// <summary>
/// Xeno slows (RMC slowdown and superslowdown) are removed as soon as they land. Roots still work.
/// </summary>
public sealed partial class ContactPerkSlowImmunity : ContactPerkEffect;

/// <summary>
/// Daze (tail trip, abduct, neuro) is removed as soon as it lands.
/// </summary>
public sealed partial class ContactPerkDazeImmunity : ContactPerkEffect;

/// <summary>
/// Boiler / neurotoxin gas doesn't inject neurotoxin.
/// </summary>
public sealed partial class ContactPerkNeurotoxinImmunity : ContactPerkEffect;

/// <summary>
/// RMC night vision while the perk is owned.
/// </summary>
public sealed partial class ContactPerkNightVision : ContactPerkEffect;

#region Headhunter / Boundary

/// <summary>
/// Client overlay: outlines living xenos between MinRange and MaxRange tiles, through walls.
/// </summary>
public sealed partial class ContactPerkHighlight : ContactPerkEffect
{
    [DataField]
    public float MinRange = 10f;

    [DataField]
    public float MaxRange = 20f;

    [DataField]
    public Color Color = Color.Red;
}

/// <summary>
/// Any handheld light carried (flashlight, helmet/armor light, gun attachment) is turned off.
/// </summary>
public sealed partial class ContactPerkNoFlashlight : ContactPerkEffect;

/// <summary>
/// No damage from these damage-over-time gas prototypes.
/// </summary>
public sealed partial class ContactPerkGasImmunity : ContactPerkEffect
{
    [DataField(required: true)]
    public List<EntProtoId> Gases = new();
}

/// <summary>
/// The loadout weapon is replaced with this one.
/// </summary>
public sealed partial class ContactPerkReplaceWeapon : ContactPerkEffect
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField]
    public List<EntProtoId> Extras = new();
}

#endregion

#endregion
