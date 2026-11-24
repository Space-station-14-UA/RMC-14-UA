using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// The perks a marine has. Networked so movement speed and gun handling predict correctly.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ContactPerksComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<ContactPerkPrototype>> Perks = new();

    /// <summary>
    /// Temporary speed boosts (Wanted Outlaw, ADRENAL), each its own stack.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ContactSpeedStack> SpeedStacks = new();

    /// <summary>
    /// Server: thresholds before any health perk touched them.
    /// </summary>
    [ViewVariables]
    public FixedPoint2? BaseCritical;

    [ViewVariables]
    public FixedPoint2? BaseDead;

    /// <summary>
    /// Server: kills towards the next kill blast.
    /// </summary>
    [ViewVariables]
    public int KillCount;

    /// <summary>
    /// Server: last time a perk-relevant gun was fired (first-shot perks).
    /// </summary>
    [ViewVariables]
    public TimeSpan LastShot;

    /// <summary>
    /// Both sides: was standing still at the last check (guns refresh when it changes).
    /// </summary>
    [ViewVariables]
    public bool StandingStill;

    [ViewVariables]
    public TimeSpan NextRestrictPopup;
}

[DataDefinition, Serializable, NetSerializable]
public partial struct ContactSpeedStack
{
    [DataField]
    public TimeSpan Until;

    [DataField]
    public float Multiplier;

    /// <summary>
    /// Which effect made it, so each one keeps its own stack cap.
    /// </summary>
    [DataField]
    public string Source;

    public ContactSpeedStack(TimeSpan until, float multiplier, string source)
    {
        Until = until;
        Multiplier = multiplier;
        Source = source;
    }
}

/// <summary>
/// Marker on guns held by a perk owner, so their refresh / shot events reach the perk systems.
/// Added on both sides when the gun is picked up.
/// </summary>
[RegisterComponent]
public sealed partial class ContactPerkGunComponent : Component;
