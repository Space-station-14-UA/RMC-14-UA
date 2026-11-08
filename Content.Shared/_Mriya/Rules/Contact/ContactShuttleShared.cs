using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

[Serializable, NetSerializable]
public enum ContactShuttleState : byte
{
    /// <summary>First drop: at the ship, nobody aboard yet.</summary>
    WaitingForBoarding,

    /// <summary>First drop: someone boarded, counting down to launch.</summary>
    Countdown,

    FlyingDown,

    /// <summary>Landed on the colony, marines getting off.</summary>
    Unloading,

    Returning,

    Refueling,

    /// <summary>At the ship, refueled; launches when the colony calls reinforcements.</summary>
    Ready,
}

/// <summary>
/// Broadcast on every shuttle state change and periodically. EndsAt is server CurTime.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactShuttleHudEvent(ContactShuttleState state, TimeSpan? endsAt) : EntityEventArgs
{
    public readonly ContactShuttleState State = state;
    public readonly TimeSpan? EndsAt = endsAt;
}