using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Dataset;

namespace Content.Shared._Mriya.Vehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class MRVehicleNicknameComponent : Component
{
    [DataField(required: true)]
    public ProtoId<LocalizedDatasetPrototype> Dataset;
}
