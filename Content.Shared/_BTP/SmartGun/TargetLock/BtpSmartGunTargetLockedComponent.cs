using Robust.Shared.GameStates;

namespace Content.Shared._BTP.SmartGun.TargetLock;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BtpSmartGunTargetLockedComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<EntityUid> LockedBy = new();
}
