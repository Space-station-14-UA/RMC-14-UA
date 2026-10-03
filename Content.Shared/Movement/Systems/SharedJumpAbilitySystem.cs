using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Throwing;
using Content.Shared.Physics;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.Movement.Systems;

public sealed partial class SharedJumpAbilitySystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JumpAbilityComponent, GravityJumpEvent>(OnGravityJump);
        SubscribeLocalEvent<JumpAbilityComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnGravityJump(Entity<JumpAbilityComponent> entity, ref GravityJumpEvent args)
    {
        if (_gravity.IsWeightless(args.Performer))
            return;

        var xform = Transform(args.Performer);
        var throwing = xform.LocalRotation.ToWorldVec() * entity.Comp.JumpDistance;
        var direction = xform.Coordinates.Offset(throwing); // to make the character jump in the direction he's looking

        _throwing.TryThrow(args.Performer, direction, entity.Comp.JumpThrowSpeed);

        _audio.PlayPredicted(entity.Comp.JumpSound, args.Performer, args.Performer);
        args.Handled = true;
    }

    private void OnPreventCollide(Entity<JumpAbilityComponent> entity, ref PreventCollideEvent args)
    {
        if (!HasComp<ThrownItemComponent>(entity.Owner))
            return;

        // If the obstacle is MidImpassable or HighImpassable but NOT Impassable (which are walls), we can jump over it.
        // We also want to allow jumping over BarricadeImpassable and BarbedBarricade.
        if (TryComp<PhysicsComponent>(args.OtherEntity, out var otherPhysics))
        {
            var mask = otherPhysics.CollisionLayer;
            // Impassable indicates hard walls. We only want to jump over tables, window frames, barriers, barricades.
            if ((mask & (int) CollisionGroup.Impassable) != 0)
                return;

            if ((mask & (int) (CollisionGroup.MidImpassable | CollisionGroup.HighImpassable | CollisionGroup.BarricadeImpassable | CollisionGroup.BarbedBarricade)) != 0)
            {
                args.Cancelled = true;
            }
        }
    }
}
