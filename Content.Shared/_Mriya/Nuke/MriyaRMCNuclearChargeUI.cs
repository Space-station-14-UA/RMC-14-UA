using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Nuke;

[Serializable, NetSerializable]
public enum MriyaRMCNuclearChargeUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class MriyaRMCNuclearChargeBuiState : BoundUserInterfaceState
{
    public readonly string Status;
    public readonly bool CanStart;
    public readonly bool CanAbort;
    public readonly TimeSpan? DetonatesAt;

    public MriyaRMCNuclearChargeBuiState(string status, bool canStart, bool canAbort, TimeSpan? detonatesAt)
    {
        Status = status;
        CanStart = canStart;
        CanAbort = canAbort;
        DetonatesAt = detonatesAt;
    }
}

[Serializable, NetSerializable]
public sealed class MriyaRMCNuclearChargeStartBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class MriyaRMCNuclearChargeAbortBuiMsg : BoundUserInterfaceMessage;
