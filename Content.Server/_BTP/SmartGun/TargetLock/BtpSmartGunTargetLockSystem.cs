using Content.Shared._BTP.SmartGun.TargetLock;

namespace Content.Server._BTP.SmartGun.TargetLock;

public sealed class BtpSmartGunTargetLockSystem : SharedBtpSmartGunTargetLockSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBtpSmartGunTargetLockEvent>(OnTargetLockRequest);
    }

    private void OnTargetLockRequest(RequestBtpSmartGunTargetLockEvent ev, EntitySessionEventArgs args)
    {
        TargetLockRequested(ev.Gun, ev.User, ev.Target);
    }
}
