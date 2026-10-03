using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Hunter.Components;

/// <summary>
/// Component for the hunter teleporter device.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HunterTeleporterComponent : Component
{
    /// <summary>
    /// Time (in seconds) the user must stand still before being teleported.
    /// </summary>
    [DataField]
    public float TeleportDelay = 4.0f;

    /// <summary>
    /// Whether the teleporter mode is set to ship (true) or planet map (false).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool TeleportToShipMode = false;
}

/// <summary>
/// Temporary tag component added to the player entity when they have the teleporter UI open,
/// enabling map click targeting.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HunterTeleportingComponent : Component
{
}

/// <summary>
/// Marker placed on an entity on the Leviathan grid to designate where hunters
/// arrive when returning from the planet via the handheld teleporter.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HunterReturnPointComponent : Component
{
}

