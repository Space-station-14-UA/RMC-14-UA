using System.Numerics;
using Content.Shared._BTP.SmartGun.TargetLock;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._BTP.SmartGun.TargetLock;

public sealed class BtpSmartGunTargetLockOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;

    public BtpSmartGunTargetLockOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var localEntity = _player.LocalEntity;
        if (localEntity == null)
            return;

        var texture = _sprite.GetFrame(
            new SpriteSpecifier.Rsi(new ResPath("/Textures/_BTP/Effects/smartgun_target_lock.rsi"), "target_lock"),
            default);

        var query = _entity.EntityQueryEnumerator<BtpSmartGunTargetLockedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var locked, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            if (!IsLockedByLocalOperator(locked, localEntity.Value))
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            var size = Vector2.One;
            if (_entity.TryGetComponent(uid, out SpriteComponent? sprite))
            {
                var bounds = sprite.Bounds;
                var side = MathF.Max(bounds.Width, bounds.Height) + 0.35f;
                size = new Vector2(side, side);
            }

            args.WorldHandle.DrawTextureRect(texture, Box2.CenteredAround(worldPos, size));
        }
    }

    private bool IsLockedByLocalOperator(BtpSmartGunTargetLockedComponent locked, EntityUid localEntity)
    {
        foreach (var gun in locked.LockedBy)
        {
            if (_entity.TryGetComponent(gun, out BtpSmartGunTargetLockComponent? lockComp) &&
                lockComp.User == localEntity)
            {
                return true;
            }
        }

        return false;
    }
}
