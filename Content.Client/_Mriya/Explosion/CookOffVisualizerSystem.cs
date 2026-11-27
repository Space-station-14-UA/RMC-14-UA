using System.Numerics;
using Content.Shared._Mriya.Explosion;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Client._Mriya.Explosion;

public sealed class CookOffVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _anim = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const string AnimationKey = "cookoff-swell";

    private readonly Dictionary<EntityUid, Vector2> _originalScale = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CookOffComponent, AfterAutoHandleStateEvent>(OnAutoHandleState);
        SubscribeLocalEvent<CookOffComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnAutoHandleState(Entity<CookOffComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!ent.Comp.Cooking || ent.Comp.Exploded)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var player = EnsureComp<AnimationPlayerComponent>(ent);

        if (_anim.HasRunningAnimation(player, AnimationKey))
            return;

        var remaining = (float)(ent.Comp.CookUntil - _timing.CurTime).TotalSeconds;
        if (remaining <= 0f)
            remaining = 0.5f;

        _originalScale[ent.Owner] = sprite.Scale;

        var anim = new Animation
        {
            Length = TimeSpan.FromSeconds(remaining),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(sprite.Scale, 0f),
                        new AnimationTrackProperty.KeyFrame(sprite.Scale * ent.Comp.SwellAmount, remaining),
                    },
                    InterpolationMode = AnimationInterpolationMode.Linear,
                },
            },
        };
        _anim.Play((ent.Owner, player), anim, AnimationKey);
    }

    private void OnShutdown(Entity<CookOffComponent> ent, ref ComponentShutdown args)
    {
        if (!_originalScale.Remove(ent.Owner, out var original))
            return;

        if (_anim.HasRunningAnimation(ent.Owner, AnimationKey))
            _anim.Stop(ent.Owner, AnimationKey);

        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetScale((ent.Owner, sprite), original);
    }
}
