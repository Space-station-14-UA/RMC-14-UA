using Content.Shared._Mriya.CCVar; // Mriya
using Content.Shared._RMC14.Xenonids.ForTheHive;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration; // Mriya

namespace Content.Client._RMC14.Xenonids.ForTheHive;

public sealed class XenoForTheHiveVisualizerSystem : VisualizerSystem<ForTheHiveComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    [Dependency] private readonly IConfigurationManager _config = default!; // Mriya

    private bool _onOldSpriteChange; // Mriya
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForTheHiveComponent, ForTheHiveActivatedEvent>(OnForTheHiveAdded);
        SubscribeLocalEvent<ForTheHiveComponent, ForTheHiveCancelledEvent>(OnForTheHiveRemoved);

        Subs.CVar(_config, MriyaCVars.MRUseOldSprites, v => _onOldSpriteChange = v, true); // Mriya
    }


    private void OnForTheHiveAdded(Entity<ForTheHiveComponent> xeno, ref ForTheHiveActivatedEvent args)
    {
        // Mriya start. Наш/офіційний спрайт в залежності від налаштувань
        var forTheHiveSprite = _onOldSpriteChange
            ? xeno.Comp.ActiveOldSprite
            : xeno.Comp.ActiveSprite;
        // Mriya end

        if (!TryComp<SpriteComponent>(xeno, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((xeno, sprite), ForTheHiveVisualLayers.Base, out var layer, false) ||
            !_sprite.TryGetLayer((xeno, sprite), layer, out var spriteLayer, false))
            return;

        if (forTheHiveSprite != null) // Mriya. xeno.Comp.ActiveSprite в оригіналі
        {
            _sprite.SetAutoAnimateSync(sprite, spriteLayer, 0);
            sprite.LayerSetRSI(layer, forTheHiveSprite); // Mriya. xeno.Comp.ActiveSprite в оригіналі
        }
    }

    private void OnForTheHiveRemoved(Entity<ForTheHiveComponent> xeno, ref ForTheHiveCancelledEvent args)
    {
        // Mriya start. Наш/офіційний спрайт в залежності від налаштувань
        var BaseSprite = _onOldSpriteChange
            ? xeno.Comp.BaseOldSprite
            : xeno.Comp.BaseSprite;
        // Mriya end

        if (!TryComp<SpriteComponent>(xeno, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((xeno, sprite), ForTheHiveVisualLayers.Base, out var layer, false) ||
            !_sprite.TryGetLayer((xeno, sprite), layer, out var spriteLayer, false))
            return;

        if (BaseSprite != null) // Mriya. xeno.Comp.BaseSprite
        {
            _sprite.SetAutoAnimateSync(sprite, spriteLayer, 0);
            sprite.LayerSetRSI(layer, BaseSprite); // Mriya. xeno.Comp.BaseSprite
        }
    }

    protected override void OnAppearanceChange(EntityUid xeno, ForTheHiveComponent component, ref AppearanceChangeEvent args)
    {
        return; // TODO RMC14 make the animation speed up overtime

        var sprite = args.Sprite;

        if (!HasComp<ActiveForTheHiveComponent>(xeno))
            return;

        if (sprite == null || !AppearanceSystem.TryGetData<float>(xeno, ForTheHiveVisuals.Time, out var ratio, args.Component))
            return;

        if (!_sprite.LayerMapTryGet((xeno, sprite), ForTheHiveVisualLayers.Base, out var layer, false))
            return;

        if (ratio >= 0)
            sprite.LayerSetAnimationTime(layer, (float)(component.AnimationTimeBase.TotalSeconds * ratio));

    }
}
