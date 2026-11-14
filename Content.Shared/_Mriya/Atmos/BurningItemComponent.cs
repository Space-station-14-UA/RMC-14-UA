using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mriya.Atmos;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class BurningItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public ItemBurnState State = ItemBurnState.Igniting;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan IgnitedAt;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan BurnUntil;

    [DataField, AutoNetworkedField]
    public bool BurntOut;

    [DataField, AutoNetworkedField]
    public bool SpawnedTileFire;

    [DataField, AutoNetworkedField]
    public float FuelLeft = 1f;
}

[Serializable, NetSerializable]
public enum ItemBurnState : byte
{
    NotBurning = 0,
    Igniting = 1,
    Burning = 2,
    Smouldering = 3,
    BurntOut = 4,
}
