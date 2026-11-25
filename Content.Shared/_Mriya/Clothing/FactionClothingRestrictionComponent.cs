using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.Clothing;

[RegisterComponent, NetworkedComponent]
public sealed partial class FactionClothingRestrictionComponent : Component
{
    [DataField(required: true)]
    public List<ProtoId<NpcFactionPrototype>> Factions = new();

    [DataField]
    public LocId DenyReason = "faction-clothing-restriction-deny";
}
