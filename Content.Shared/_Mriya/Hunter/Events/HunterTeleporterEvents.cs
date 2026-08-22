using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Hunter.Events;

/// <summary>
/// UI Key for the Hunter Teleporter BUI.
/// </summary>
[Serializable, NetSerializable]
public enum HunterTeleporterUiKey : byte
{
    Key
}

/// <summary>
/// Client-to-server request message sent when the player clicks a point on the map.
/// </summary>
[Serializable, NetSerializable]
public sealed class HunterTeleportRequestMsg(Vector2i position) : BoundUserInterfaceMessage
{
    public readonly Vector2i Position = position;
}

/// <summary>
/// DoAfter event raised when the stand-still channel completes (teleport to planet position).
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HunterTeleportDoAfterEvent : SimpleDoAfterEvent
{
    public readonly Vector2i TargetPosition;

    public HunterTeleportDoAfterEvent(Vector2i targetPosition)
    {
        TargetPosition = targetPosition;
    }
}

/// <summary>
/// DoAfter event raised when the hunter activates the teleporter while on the planet,
/// triggering a return to the Leviathan of the Shadows.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class HunterReturnToShipDoAfterEvent : SimpleDoAfterEvent
{
}
