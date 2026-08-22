using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Hunter.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HunterClawModuleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ClawPrototype = "RMCHunterClaws";

    [DataField, AutoNetworkedField]
    public EntProtoId ToggleAction = "ActionHunterClawsToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [DataField, AutoNetworkedField]
    public List<EntityUid> ActiveClaws = new();

    [DataField, AutoNetworkedField]
    public SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/_Mriya/Hunter/wristblades_on.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier RetractSound = new SoundPathSpecifier("/Audio/_Mriya/Hunter/wristblades_off.ogg");
}


