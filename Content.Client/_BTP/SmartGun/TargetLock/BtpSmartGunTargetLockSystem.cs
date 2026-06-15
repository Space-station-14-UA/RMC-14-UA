using Content.Client.Gameplay;
using Content.Shared._BTP.SmartGun.TargetLock;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged.Ammo;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;

namespace Content.Client._BTP.SmartGun.TargetLock;

public sealed class BtpSmartGunTargetLockSystem : SharedBtpSmartGunTargetLockSystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IStateManager _state = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BtpSmartGunTargetLockComponent, UniqueActionEvent>(OnUniqueAction,
            after: new[] { typeof(AttachableHolderSystem) },
            before: new[] { typeof(GunToggleableAmmoSystem) });

        SubscribeLocalEvent<RequestBtpSmartGunTargetLockEvent>(OnTargetLockRequest);

        if (!_overlay.HasOverlay<BtpSmartGunTargetLockOverlay>())
            _overlay.AddOverlay(new BtpSmartGunTargetLockOverlay());
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<BtpSmartGunTargetLockOverlay>();
    }

    private void OnUniqueAction(Entity<BtpSmartGunTargetLockComponent> ent, ref UniqueActionEvent args)
    {
        if (!Timing.IsFirstTimePredicted || args.Handled)
            return;

        if (_player.LocalEntity == null || !ent.Comp.Activated)
            return;

        NetEntity? target = null;
        if (ent.Comp.Target == null)
        {
            var mousePos = _eye.PixelToMap(_input.MouseScreenPosition);
            if (_state.CurrentState is GameplayStateBase screen)
                target = GetNetEntity(screen.GetClickedEntity(mousePos));

            if (target == null)
                return;
        }

        RaisePredictiveEvent(new RequestBtpSmartGunTargetLockEvent
        {
            Gun = GetNetEntity(ent.Owner),
            User = GetNetEntity(args.UserUid),
            Target = target,
        });

        args.Handled = true;
    }

    private void OnTargetLockRequest(RequestBtpSmartGunTargetLockEvent ev, EntitySessionEventArgs args)
    {
        TargetLockRequested(ev.Gun, ev.User, ev.Target);
    }
}
