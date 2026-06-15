using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BTP.SmartGun.TargetLock;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BtpSmartGunTargetLockComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "BTPActionSmartGunTargetLock";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public bool Activated;

    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    [DataField, AutoNetworkedField]
    public EntityUid? User;

    [DataField, AutoNetworkedField]
    public float Range = 15f;

    [DataField, AutoNetworkedField]
    public float ProjectileSpeed = 90f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/_BTP/Weapons/SmartGun/target_locked.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/_RMC14/Machines/click.ogg");
}
