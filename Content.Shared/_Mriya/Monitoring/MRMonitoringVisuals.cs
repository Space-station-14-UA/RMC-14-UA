using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Monitoring;

[Serializable, NetSerializable]
public enum MRMonitoringVisuals : byte
{
    Enabled,
}

[Serializable, NetSerializable]
public enum MRMonitoringVisualLayers : byte
{
    Active,
}
