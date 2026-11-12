using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

[Serializable, NetSerializable]
public enum ContactExtractionState : byte
{
    None,

    /// <summary>After every Nth wave: break before the next wave; anchors offline; gather at an LZ to start.</summary>
    Available,

    /// <summary>More than half the colony is at one LZ: short countdown, cancels if they scatter.</summary>
    Countdown,

    /// <summary>Evacuation started: defend the LZ until the shuttle is called.</summary>
    Holding,

    /// <summary>Shuttle called (after refuel if needed), flying to the LZ.</summary>
    ShuttleInbound,

    /// <summary>Shuttle on the LZ, boarding until takeoff.</summary>
    Boarding,

    Departing,

    Done,
}

/// <summary>
/// Broadcast while an extraction is offered or running. EndsAt is server CurTime.
/// Zones: every LZ while Available, only the chosen one afterwards.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactExtractionHudEvent(
    ContactExtractionState state,
    List<NetCoordinates> zones,
    float radius,
    TimeSpan? endsAt,
    int inZone,
    int needed)
    : EntityEventArgs
{
    public readonly ContactExtractionState State = state;
    public readonly List<NetCoordinates> Zones = zones;
    public readonly float Radius = radius;
    public readonly TimeSpan? EndsAt = endsAt;
    public readonly int InZone = inZone;
    public readonly int Needed = needed;
}