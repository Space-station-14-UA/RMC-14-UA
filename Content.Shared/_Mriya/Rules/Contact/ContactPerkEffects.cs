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
/// </summary>
public sealed partial class ContactPerkCrit : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField(required: true)]
    public float Chance;

    [DataField]
    public float Multiplier = 3f;
}

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
/// RMC xeno armor (protection against xeno melee).
/// </summary>
public sealed partial class ContactPerkXenoArmor : ContactPerkEffect
{
    [DataField(required: true)]
    public int Amount;
}

/// <summary>
/// Gun handling while held. Below 1 = better for Spread / Recoil (0.4 = 60% improvement), above 1 = faster FireRate.
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
/// Heals this fraction of the damage dealt to xenos.
/// </summary>
public sealed partial class ContactPerkLifesteal : ContactPerkEffect
{
    [DataField]
    public ContactPerkWeapon Weapon = ContactPerkWeapon.Any;

    [DataField(required: true)]
    public float Fraction;
}

/// <summary>
/// Refuses to shoot other guns: Allowed Pistol = only pistols, Melee = no guns at all.
/// </summary>
public sealed partial class ContactPerkRestrict : ContactPerkEffect
{
    [DataField(required: true)]
    public ContactPerkWeapon Allowed;
}
