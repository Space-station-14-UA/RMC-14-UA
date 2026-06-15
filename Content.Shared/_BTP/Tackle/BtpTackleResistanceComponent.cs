using Robust.Shared.GameStates;

namespace Content.Shared._BTP.Tackle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(Content.Shared._RMC14.Tackle.TackleSystem))]
public sealed partial class BtpTackleResistanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float ChanceMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float StunMultiplier = 1f;
}
