using Robust.Shared.GameStates;

namespace Content.Shared._Mriya.Atmos;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlammabilityComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Flammability = 0.5f;

    [DataField, AutoNetworkedField]
    public float BurnSupport = 0.5f;

    [DataField, AutoNetworkedField]
    public float BurnDurationBonus = 10f;

    [DataField, AutoNetworkedField]
    public float BurnDamageMultiplier = 1f;
}
