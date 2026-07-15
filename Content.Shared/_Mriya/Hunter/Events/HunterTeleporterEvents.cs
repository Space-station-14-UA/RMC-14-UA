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
/// DoAfter event raised when the stand-still channel completes.
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
