using Robust.Shared.GameStates;

namespace Content.Shared._BTP.SmartGun.TargetLock;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BtpSmartGunTargetLockProjectileComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Target;
}
