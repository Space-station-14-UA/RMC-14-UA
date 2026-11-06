using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Contact AI xeno: perception (sight, hearing, hunting), obstacle smashing, holding position
/// and data-driven ability usage. Movement and melee are done by HTN (MRContactXenoCompound).
/// </summary>
[RegisterComponent]
public sealed partial class ContactXenoAIComponent : Component
{
    /// <summary>
    /// Sees marines within this range with line of sight. Abilities only target seen marines.
    /// </summary>
    [DataField]
    public float VisionRange = 12f;

    /// <summary>
    /// Hears marines shooting within this range and goes to investigate.
    /// </summary>
    [DataField]
    public float HearingRange = 25f;

    /// <summary>
    /// With nothing seen or heard, drifts toward the nearest marine within this range. 0 = never.
    /// </summary>
    [DataField]
    public float HuntRange = 150f;

    /// <summary>
    /// Checked top to bottom; the first rule that passes is used.
    /// </summary>
    [DataField]
    public List<ContactXenoAbilityRule> Abilities = new();

    /// <summary>
    /// Minimum time between two abilities.
    /// </summary>
    [DataField]
    public TimeSpan AbilityDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Stand still while a seen target is within this range (and farther than HoldMinRange). Null = never.
    /// For ranged castes like the boiler.
    /// </summary>
    [DataField]
    public float? HoldRange;

    /// <summary>
    /// Closer than this, stop holding and close in.
    /// </summary>
    [DataField]
    public float HoldMinRange;

    /// <summary>
    /// What the xeno may claw through when it can't get closer. Walls aren't in here on purpose.
    /// </summary>
    [DataField]
    public EntityWhitelist SmashWhitelist = new()
    {
        Components = new[] { "Barricade", "XenoCrusherChargable", "Climbable" },
    };

    /// <summary>
    /// No progress toward the destination for this long = blocked, find something to smash.
    /// </summary>
    [DataField]
    public TimeSpan StuckTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Give up on a smash target after this long.
    /// </summary>
    [DataField]
    public TimeSpan SmashTimeout = TimeSpan.FromSeconds(15);

    #region Runtime

    [ViewVariables]
    public TimeSpan NextAbility;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public float TargetDistance;

    /// <summary>
    /// Action prototype id -> action entity on this xeno.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, EntityUid> ActionCache = new();

    [ViewVariables]
    public MapCoordinates? LastDestination;

    [ViewVariables]
    public float BestDistance = float.MaxValue;

    [ViewVariables]
    public TimeSpan NoProgress;

    [ViewVariables]
    public EntityUid? SmashTarget;

    [ViewVariables]
    public TimeSpan SmashUntil;

    #endregion
}

[DataDefinition]
public sealed partial class ContactXenoAbilityRule
{
    [DataField(required: true)]
    public EntProtoId Action;

    [DataField]
    public float MinRange;

    [DataField]
    public float MaxRange = 2f;

    /// <summary>
    /// Only when own health is at or below this fraction (1 = full health).
    /// </summary>
    [DataField]
    public float? MaxHealth;

    /// <summary>
    /// Only when the action's toggled state matches (crest lowered, invisible, charging...).
    /// </summary>
    [DataField]
    public bool? Toggled;

    /// <summary>
    /// Only when the target holds an item passing this whitelist.
    /// </summary>
    [DataField]
    public EntityWhitelist? TargetHolding;

    /// <summary>
    /// Only when the target does NOT hold an item passing this whitelist.
    /// </summary>
    [DataField]
    public EntityWhitelist? TargetNotHolding;

    [DataField]
    public ContactXenoAim Aim = ContactXenoAim.Target;

    /// <summary>
    /// For Aim: AwayFromTarget, how far away to aim.
    /// </summary>
    [DataField]
    public float AwayDistance = 5f;

    [DataField]
    public float Chance = 1f;

    /// <summary>
    /// Stand still for this many seconds when using it (abilities with a wind-up / do-after).
    /// </summary>
    [DataField]
    public float Hold;
}

public enum ContactXenoAim : byte
{
    /// <summary>Entity/world target actions aim at the target.</summary>
    Target,

    /// <summary>World target actions aim directly away from the target (retreat jumps).</summary>
    AwayFromTarget,
}