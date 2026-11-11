using Content.Shared.Mobs;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Monitoring;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class MRMonitoringComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<MRMonitoringMarine> Marines = new();

    [DataField, AutoNetworkedField]
    public NetEntity? Selected;

    [DataField, AutoNetworkedField]
    public NetEntity? User;

    [DataField, AutoNetworkedField]
    public float BatteryPercent = 100f;

    [DataField, AutoNetworkedField]
    public float Distance = -1f;

    [DataField, AutoNetworkedField]
    public float DirectionAngle;
}

[Serializable, NetSerializable]
public readonly record struct MRMonitoringMarine(
    NetEntity Id,
    string Name,
    MobState State,
    string Area
);
