using Content.Shared._Mriya.Rules.Contact;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact shuttle cycle. Lives on the MRContactRule entity.
/// First drop: half of the marines aboard starts a countdown, then automatic launch.
/// After that: land, unload, return, refuel, wait for the reinforcement terminal (or a manual flight).
/// Evacuation reserves it: no reinforcements, and the evac flight stays on the LZ until the rule calls takeoff.
/// </summary>
[RegisterComponent, Access(typeof(ContactShuttleSystem))]
public sealed partial class ContactShuttleComponent : Component
{
    /// <summary>
    /// The automated transport: the dropship grid that has an entity of this prototype on it.
    /// </summary>
    [DataField]
    public EntProtoId TransportMarker = "CMAlamoCockpit";

    /// <summary>
    /// HUD name for the other dropship, left to the players.
    /// </summary>
    [DataField]
    public EntProtoId GunshipMarker = "CMNormandyCockpit";

    /// <summary>
    /// First drop: countdown once at least half of the living marines are aboard.
    /// </summary>
    [DataField]
    public TimeSpan BoardingCountdown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time on the colony before flying back.
    /// </summary>
    [DataField]
    public TimeSpan UnloadTime = TimeSpan.FromSeconds(90);

    [DataField]
    public TimeSpan RefuelTime = TimeSpan.FromSeconds(150);

    [DataField]
    public TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    #region Runtime

    [ViewVariables]
    public ContactShuttleState State = ContactShuttleState.WaitingForBoarding;

    [ViewVariables]
    public TimeSpan? StateEndsAt;

    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>
    /// Reserved for evacuation: no reinforcement calls.
    /// </summary>
    [ViewVariables]
    public bool Extraction;

    /// <summary>
    /// The current flight is the evacuation: no auto-return after landing.
    /// </summary>
    [ViewVariables]
    public bool ExtractionFlight;

    /// <summary>
    /// The transport dropship grid.
    /// </summary>
    [ViewVariables]
    public EntityUid? Dropship;

    /// <summary>
    /// The other dropship (HUD only).
    /// </summary>
    [ViewVariables]
    public EntityUid? Gunship;

    /// <summary>
    /// Where it docks at the ship.
    /// </summary>
    [ViewVariables]
    public EntityUid? ShipDock;

    /// <summary>
    /// Where reinforcements land: the LZ of the last drop (set to the evac LZ for evacuation).
    /// </summary>
    [ViewVariables]
    public EntityUid? LandingZone;

    [ViewVariables]
    public int LastAboard = -1;

    [ViewVariables]
    public int LastNeeded = -1;

    #endregion
}

/// <summary>
/// On the colony: sends the refueled shuttle down when marines are aboard.
/// </summary>
[RegisterComponent, Access(typeof(ContactShuttleSystem))]
public sealed partial class ContactReinforcementTerminalComponent : Component;