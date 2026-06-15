using Robust.Shared.Serialization;

namespace Content.Shared._BTP.SmartGun.TargetLock;

[Serializable, NetSerializable]
public sealed class RequestBtpSmartGunTargetLockEvent : EntityEventArgs
{
    public NetEntity Gun;
    public NetEntity User;
    public NetEntity? Target;
}
