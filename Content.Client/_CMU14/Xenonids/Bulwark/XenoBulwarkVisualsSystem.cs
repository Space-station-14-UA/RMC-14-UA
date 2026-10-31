using Content.Shared._CMU14.Xenonids.Bulwark;
using Robust.Client.GameObjects;

namespace Content.Client._CMU14.Xenonids.Bulwark;

public sealed partial class XenoBulwarkVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenoBulwarkComponent, ComponentStartup>(OnBulwarkStartup);
        SubscribeLocalEvent<XenoBulwarkComponent, AfterAutoHandleStateEvent>(OnBulwarkState);
    }

    private void OnBulwarkStartup(Entity<XenoBulwarkComponent> xeno, ref ComponentStartup args)
    {
        UpdateShield(xeno);
    }

    private void OnBulwarkState(Entity<XenoBulwarkComponent> xeno, ref AfterAutoHandleStateEvent args)
    {
        UpdateShield(xeno);
    }

    private void UpdateShield(Entity<XenoBulwarkComponent> xeno)
    {
        if (!TryComp(xeno, out SpriteComponent? sprite) ||
            !_sprite.LayerMapTryGet((xeno, sprite), XenoBulwarkVisualLayers.Shield, out var layer, false))
        {
            return;
        }

        _sprite.LayerSetVisible((xeno, sprite), layer, xeno.Comp.Reflecting);
    }
}
