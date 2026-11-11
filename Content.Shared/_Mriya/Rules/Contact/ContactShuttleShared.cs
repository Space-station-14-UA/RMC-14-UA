using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

[Serializable, NetSerializable]
public enum ContactShuttleState : byte
{
    WaitingForBoarding,
    Countdown,
    FlyingDown,
    Unloading,
    Returning,
    Refueling,
    Ready,
}

[Serializable, NetSerializable]
public sealed class ContactShuttleHudEvent(
    ContactShuttleState state,
    TimeSpan? endsAt,
    string? transport,
    string? gunship,
    int aboard,
    int needed) : EntityEventArgs
{
    public readonly ContactShuttleState State = state;
    public readonly TimeSpan? EndsAt = endsAt;

    /// <summary>
    /// Automated transport dropship name.
    /// </summary>
    public readonly string? Transport = transport;

    /// <summary>
    /// Free (player-controlled) dropship name, null if the map has none.
    /// </summary>
    public readonly string? Gunship = gunship;

    public readonly int Aboard = aboard;

    /// <summary>
    /// 0 = don't show boarding numbers.
    /// </summary>
    public readonly int Needed = needed;
}

[Serializable, NetSerializable]
public enum ContactReinforcementTerminalVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum ContactReinforcementTerminalState : byte
{
    /// <summary>Shuttle busy / refueling / nobody aboard.</summary>
    Blocked,

    /// <summary>Refueled with marines aboard: a call will launch it.</summary>
    Active,

    /// <summary>Reserved for the evacuation.</summary>
    Evacuation,
}