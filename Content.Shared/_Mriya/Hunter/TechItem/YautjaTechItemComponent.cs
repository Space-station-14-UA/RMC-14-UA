using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sich.Hunter.TechItem;

[RegisterComponent, NetworkedComponent]
public sealed partial class YautjaTechItemComponent : Component
{
    [DataField]
    public float DamageMultiplier = 1.5f;

    [DataField]
    public bool BlockPickup = true;

    [DataField]
    public bool BlockUse = true;

    [DataField]
    public bool BlockMelee = true;

    [DataField]
    public bool BlockThrow = true;

    [DataField]
    public bool BlockShoot = true;
}

[Serializable, NetSerializable]
public enum YautjaTechMisuseKind : byte
{
    Pickup,
    Use,
    Melee,
    Throw,
    Shoot,
}

[ByRefEvent]
public record struct YautjaTechMisusedEvent(EntityUid User, EntityUid Item, YautjaTechMisuseKind Kind);
