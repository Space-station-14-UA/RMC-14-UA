using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

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
    /// Server: thresholds before any health perk touched them.
    /// </summary>
    [ViewVariables]
    public FixedPoint2? BaseCritical;

    [ViewVariables]
    public FixedPoint2? BaseDead;

    [ViewVariables]
    public TimeSpan NextRestrictPopup;
}

/// <summary>
/// Marker on guns held by a perk owner, so their refresh / shot events reach the perk systems.
/// Added on both sides when the gun is picked up.
/// </summary>
[RegisterComponent]
public sealed partial class ContactPerkGunComponent : Component;
