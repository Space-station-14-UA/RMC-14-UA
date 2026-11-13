using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Monitoring;

[Serializable, NetSerializable]
public enum MRMonitoringUI
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MRMonitoringSelectMarineMsg(NetEntity marine) : BoundUserInterfaceMessage
{
    public readonly NetEntity Marine = marine;
}
