using Content.Shared._Mriya.Rules.Contact;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact shuttle cycle. Lives on the MRContactRule entity.
/// First drop: boarding starts a countdown, then automatic launch.
/// After that: land, unload, return, refuel, wait for the reinforcement terminal.
/// </summary>
[RegisterComponent, Access(typeof(ContactShuttleSystem))]
public sealed partial class ContactShuttleComponent : Component
{
    /// <summary>
    /// First drop: countdown after the first marine boards.
    /// </summary>
    [DataField]
    public TimeSpan BoardingCountdown = TimeSpan.FromMinutes(2);

    /// <summary>
    /// First drop: countdown is cut to this once every living marine is aboard.
    /// </summary>
    [DataField]
    public TimeSpan FullBoardCountdown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Time on the colony before flying back.
    /// </summary>
    [DataField]
    public TimeSpan UnloadTime = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan RefuelTime = TimeSpan.FromSeconds(150);

    [DataField]
    public TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan HudSyncEvery = TimeSpan.FromSeconds(5);

    #region Runtime

    [ViewVariables]
    public ContactShuttleState State = ContactShuttleState.WaitingForBoarding;

    [ViewVariables]
    public TimeSpan? StateEndsAt;

    [ViewVariables]
    public TimeSpan NextCheck;

    [ViewVariables]
    public TimeSpan NextHudSync;

    /// <summary>
    /// The dropship grid.
    /// </summary>
    [ViewVariables]
    public EntityUid? Dropship;

    /// <summary>
    /// Where it docks at the ship (taken on the first launch).
    /// </summary>
    [ViewVariables]
    public EntityUid? ShipDock;

    /// <summary>
    /// Where it lands on the colony (picked on the first launch, reused after).
    /// </summary>
    [ViewVariables]
    public EntityUid? LandingZone;

    #endregion
}

/// <summary>
/// On the colony: sends the refueled shuttle down when marines are aboard.
/// </summary>
[RegisterComponent, Access(typeof(ContactShuttleSystem))]
public sealed partial class ContactReinforcementTerminalComponent : Component;