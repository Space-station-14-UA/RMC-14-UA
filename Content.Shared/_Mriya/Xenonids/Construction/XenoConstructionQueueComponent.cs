using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Construction;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class XenoConstructionQueueComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<QueuedXenoConstruction> Queue = new();

    [DataField]
    public int MaxQueued = 50;
}

[DataDefinition, Serializable, NetSerializable]
public partial struct QueuedXenoConstruction
{
    [DataField]
    public NetCoordinates Coordinates;

    [DataField]
    public EntProtoId StructureId;

    [DataField]
    public NetEntity? Marker;

    public QueuedXenoConstruction(NetCoordinates coordinates, EntProtoId structureId, NetEntity? marker = null)
    {
        Coordinates = coordinates;
        StructureId = structureId;
        Marker = marker;
    }
}
