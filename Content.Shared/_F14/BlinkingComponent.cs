using Robust.Shared.GameStates;

namespace Content.Shared._F14;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlinkingComponent : Component
{
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsBlinking = false;

    [DataField]
    public float BlinkDuration = 0.5f;
    [ViewVariables]
    public float BlinkTimer;

    [ViewVariables, AutoNetworkedField]
    public bool InAngelAura;

    [DataField]
    public float ForcedBlinkInterval = 3f;

    [ViewVariables]
    public float ForcedBlinkTimer;
}