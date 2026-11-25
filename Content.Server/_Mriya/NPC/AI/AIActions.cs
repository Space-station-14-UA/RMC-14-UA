using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.NPC.AI;

internal static class AISteering
{
    public static NPCSteeringComponent Steer(EntityUid owner, EntityCoordinates destination, IEntityManager entMan, float range)
    {
        if (entMan.TryGetComponent<NPCSteeringComponent>(owner, out var steering)
            && steering.Status == SteeringStatus.NoPath)
        {
            steering.Status = SteeringStatus.Moving;
            steering.FailedPathCount = 0;
            steering.CurrentPath.Clear();
        }

        var registered = entMan.System<NPCSteeringSystem>().Register(owner, destination);
        registered.Range = range;
        return registered;
    }

    public static EntityCoordinates FromWorldOffset(EntityUid owner, Vector2 worldOffset, IEntityManager entMan)
    {
        var transform = entMan.System<SharedTransformSystem>();
        var xform = entMan.GetComponent<TransformComponent>(owner);
        var worldPos = transform.GetWorldPosition(xform) + worldOffset;

        if (xform.ParentUid == xform.MapUid || xform.ParentUid == EntityUid.Invalid)
            return new EntityCoordinates(xform.ParentUid, worldPos);

        var parentXform = entMan.GetComponent<TransformComponent>(xform.ParentUid);
        var localPos = Vector2.Transform(worldPos, transform.GetInvWorldMatrix(parentXform));
        return new EntityCoordinates(xform.ParentUid, localPos);
    }
}

internal static class AICombat
{
    public static NPCRangedCombatComponent ConfigureRanged(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        var ranged = entMan.EnsureComponent<NPCRangedCombatComponent>(owner);
        ranged.BurstSize = comp.BurstSize;
        ranged.BurstPause = comp.BurstPause;
        ranged.BurstPauseJitter = comp.BurstPauseJitter;

        if (ranged.Target != comp.Target)
        {
            ranged.BurstCounter = 0;
            ranged.NextBurst = TimeSpan.Zero;
        }

        ranged.Target = comp.Target;
        return ranged;
    }
}

public sealed partial class AIEquipWeaponAction : AIAction
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;

    public AIEquipWeaponAction()
    {
        Name = "EquipWeapon";
        Cost = 0.4f;
        Effects[AIKeys.WeaponEquipped] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return AIEquipment.TryFindGun(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;

        if (!AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            return;

        var hands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

        foreach (var held in hands.EnumerateHeld(owner).ToList())
        {
            if (held == gun)
                continue;

            AIEquipment.TryStowItem(owner, held, entMan, AIEquipment.MeleeStowSlots);
        }

        AIEquipment.TryHoldItem(owner, gun, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (_timing.CurTime - _start < TimeSpan.FromSeconds(0.15f))
            return AIActionStatus.Continuing;

        if (!AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            return AIActionStatus.Failed;

        var hands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

        return hands.IsHolding(owner, gun, out _) && hands.GetActiveItem(owner) == gun
            ? AIActionStatus.Finished
            : AIActionStatus.Failed;
    }
}

public sealed partial class AIWieldWeaponAction : AIAction
{
    private const float WieldRetry = 0.8f;
    private const float WieldTimeout = 4f;

    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;
    private TimeSpan _nextAttempt;

    public AIWieldWeaponAction()
    {
        Name = "WieldWeapon";
        Cost = 0.2f;
        Preconditions[AIKeys.WeaponEquipped] = AIValues.True;
        Effects[AIKeys.WeaponWielded] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return AIEquipment.TryFindGun(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
        _nextAttempt = _timing.CurTime;

        if (!AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            return;

        var hands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

        foreach (var held in hands.EnumerateHeld(owner).ToList())
        {
            if (held == gun)
                continue;

            AIEquipment.TryStowItem(owner, held, entMan, AIEquipment.MeleeStowSlots);
        }

        AIEquipment.TryHoldItem(owner, gun, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            return AIActionStatus.Failed;

        if (!entMan.TryGetComponent<WieldableComponent>(gun, out var wieldable))
            return AIActionStatus.Finished;

        if (wieldable.Wielded)
            return AIActionStatus.Finished;

        if (_timing.CurTime >= _nextAttempt)
        {
            _nextAttempt = _timing.CurTime + TimeSpan.FromSeconds(WieldRetry);
            entMan.System<SharedWieldableSystem>().TryWield(gun, wieldable, owner);

            if (wieldable.Wielded)
                return AIActionStatus.Finished;
        }

        return _timing.CurTime - _start >= TimeSpan.FromSeconds(WieldTimeout)
            ? AIActionStatus.Failed
            : AIActionStatus.Continuing;
    }
}

public sealed partial class AIDrawKnifeAction : AIAction
{
    public AIDrawKnifeAction()
    {
        Name = "DrawKnife";
        Cost = 0.25f;
        Effects[AIKeys.MeleeArmed] = AIValues.True;
        Effects[AIKeys.WeaponEquipped] = AIValues.False;
        Effects[AIKeys.WeaponWielded] = AIValues.False;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return AIEquipment.TryFindMelee(owner, entMan, out _);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!AIEquipment.TryFindMelee(owner, entMan, out var knife))
            return;

        var hands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

        foreach (var held in hands.EnumerateHeld(owner).ToList())
        {
            if (!entMan.HasComponent<GunComponent>(held))
                continue;

            AIEquipment.TryStowItem(owner, held, entMan, AIEquipment.GunStowSlots);
        }

        AIEquipment.TryHoldItem(owner, knife, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!AIEquipment.TryFindMelee(owner, entMan, out var knife))
            return AIActionStatus.Failed;

        if (!comp.MeleeArmed)
            return AIEquipment.TryHoldItem(owner, knife, entMan)
                ? AIActionStatus.Continuing
                : AIActionStatus.Failed;

        return AIActionStatus.Finished;
    }
}

public sealed partial class AIAdvanceAction : AIAction
{
    private const float RetargetInterval = 0.75f;
    private const float Timeout = 7f;

    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;
    private TimeSpan _nextRetarget;

    public AIAdvanceAction()
    {
        Name = "Advance";
        Cost = 1f;
        Preconditions[AIKeys.WeaponEquipped] = AIValues.True;
        Effects[AIKeys.InRange] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget && AIEquipment.TryFindGun(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
        _nextRetarget = _timing.CurTime;
        MoveTowardsTarget(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Failed;

        if (comp.TargetVisible && comp.TargetDistance <= comp.RangedRange)
            return AIActionStatus.Finished;

        if (!comp.TargetVisible)
        {
            var transform = entMan.System<SharedTransformSystem>();
            var ownerPos = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));

            if (Vector2.Distance(ownerPos, comp.LastKnownTargetPosition) <= 1.5f)
                return AIActionStatus.Finished;
        }

        if (_timing.CurTime >= _nextRetarget)
        {
            _nextRetarget = _timing.CurTime + TimeSpan.FromSeconds(RetargetInterval);
            MoveTowardsTarget(owner, comp, entMan);
        }

        return _timing.CurTime - _start >= TimeSpan.FromSeconds(Timeout)
            ? AIActionStatus.Failed
            : AIActionStatus.Continuing;
    }

    private void MoveTowardsTarget(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        var transform = entMan.System<SharedTransformSystem>();
        var ownerPos = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));

        var destination = comp.TargetVisible
            ? entMan.GetComponent<TransformComponent>(comp.Target).Coordinates
            : AISteering.FromWorldOffset(owner, comp.LastKnownTargetPosition - ownerPos, entMan);

        AISteering.Steer(owner, destination, entMan, 1f);
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);
    }
}

public sealed partial class AICloseToMeleeAction : AIAction
{
    private const float RetargetInterval = 0.5f;
    private const float Timeout = 10f;

    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;
    private TimeSpan _nextRetarget;

    public AICloseToMeleeAction()
    {
        Name = "CloseToMelee";
        Cost = 3f;
        Effects[AIKeys.InMeleeZone] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget && AIEquipment.TryFindMelee(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
        _nextRetarget = _timing.CurTime;
        MoveTowardsTarget(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Failed;

        if (comp.TargetDistance <= comp.MeleeZoneRadius)
            return AIActionStatus.Finished;

        if (_timing.CurTime >= _nextRetarget)
        {
            _nextRetarget = _timing.CurTime + TimeSpan.FromSeconds(RetargetInterval);
            MoveTowardsTarget(owner, comp, entMan);
        }

        return _timing.CurTime - _start >= TimeSpan.FromSeconds(Timeout)
            ? AIActionStatus.Failed
            : AIActionStatus.Continuing;
    }

    private void MoveTowardsTarget(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (comp.TargetVisible &&
            entMan.TryGetComponent<TransformComponent>(comp.Target, out var targetXform))
        {
            AISteering.Steer(owner, targetXform.Coordinates, entMan, comp.MeleeZoneRadius);
            return;
        }

        var transform = entMan.System<SharedTransformSystem>();
        var ownerPos = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));
        var destination = AISteering.FromWorldOffset(owner, comp.LastKnownTargetPosition - ownerPos, entMan);

        AISteering.Steer(owner, destination, entMan, comp.MeleeZoneRadius);
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);
    }
}

public sealed partial class AIAttackAction : AIAction
{
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private TimeSpan _nextReposition;
    private float _orbitSign = 1f;

    public AIAttackAction()
    {
        Name = "Attack";
        Cost = 0.8f;
        Preconditions[AIKeys.WeaponEquipped] = AIValues.True;
        Preconditions[AIKeys.WeaponWielded] = AIValues.True;
        Preconditions[AIKeys.WeaponLoaded] = AIValues.True;
        Preconditions[AIKeys.InRange] = AIValues.True;
        Preconditions[AIKeys.TargetVisible] = AIValues.True;
        Effects[AIKeys.TargetDead] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget && AIEquipment.TryFindGun(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (_gun.TryGetGun(owner, out var gunUid, out _)
            && entMan.TryGetComponent<ChamberMagazineAmmoProviderComponent>(gunUid, out var chamber)
            && chamber.BoltClosed == false)
        {
            _gun.SetBoltClosed(gunUid, chamber, true);
        }

        AICombat.ConfigureRanged(owner, comp, entMan);

        _orbitSign = _random.Prob(0.5f) ? 1f : -1f;
        _nextReposition = _timing.CurTime;
        Reposition(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Failed;

        if (entMan.System<MobStateSystem>().IsDead(comp.Target))
            return AIActionStatus.Finished;

        if (!comp.TargetVisible)
            return AIActionStatus.Failed;

        if (!AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            return AIActionStatus.Failed;

        var hands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

        if (!hands.IsHolding(owner, gun, out _) || hands.GetActiveItem(owner) != gun)
            return AIActionStatus.Failed;

        if (entMan.TryGetComponent<WieldableComponent>(gun, out var wieldable) && !wieldable.Wielded)
            return AIActionStatus.Failed;

        if (!comp.WeaponLoaded)
            return AIActionStatus.Failed;

        if (comp.TargetDistance > comp.RangedRange * 1.1f)
            return AIActionStatus.Failed;

        if (entMan.TryGetComponent<NPCSteeringComponent>(owner, out var steering)
            && steering.Status == SteeringStatus.NoPath)
        {
            AISteering.Steer(
                owner,
                entMan.GetComponent<TransformComponent>(owner).Coordinates,
                entMan,
                1f);
            _nextReposition = _timing.CurTime + TimeSpan.FromSeconds(0.5f);
        }
        else if (_timing.CurTime >= _nextReposition)
        {
            Reposition(owner, comp, entMan);
        }

        return AIActionStatus.Continuing;
    }

    private void Reposition(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<TransformComponent>(comp.Target, out var targetXform))
            return;

        var transform = entMan.System<SharedTransformSystem>();
        var ownerPos = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));
        var targetPos = transform.GetWorldPosition(targetXform);
        var toTarget = targetPos - ownerPos;
        var distance = toTarget.Length();

        if (distance < 0.01f)
            toTarget = new Vector2(1f, 0f);
        else
            toTarget /= distance;

        var radial = -toTarget;
        var tangent = new Vector2(-radial.Y, radial.X) * _orbitSign;

        Vector2 offset;

        if (comp.SquadRole == "Suppress")
        {
            var holdDistance = comp.RangedRange * 0.7f;
            offset = radial * Math.Clamp(distance - holdDistance, -0.6f, 0.6f) + tangent * 0.8f;
        }
        else if (comp.SquadRole == "Flank")
        {
            var preferred = Math.Clamp(comp.RangedRange * 0.55f, 3.5f, 8f);
            var radialStep = Math.Clamp(distance - preferred, -1.5f, 1.5f);
            offset = radial * radialStep + tangent * 2.5f;
        }
        else if (distance > comp.RangedRange * 0.85f)
        {
            var step = MathF.Min(2f, distance - comp.RangedRange * 0.6f);
            offset = toTarget * step + tangent * 1f;
        }
        else
        {
            var preferred = Math.Clamp(comp.RangedRange * 0.45f, 2.5f, 6f);
            var radialStep = Math.Clamp(distance - preferred, -2f, 2f);
            offset = radial * radialStep + tangent * 1.5f;
        }

        if (offset.Length() > 2.5f)
            offset = offset.Normalized() * 2.5f;

        AISteering.Steer(
            owner,
            AISteering.FromWorldOffset(owner, offset, entMan),
            entMan,
            0.5f);

        var pause = comp.SquadRole == "Suppress"
            ? _random.NextFloat(2.5f, 3.5f)
            : _random.NextFloat(1.2f, 2.2f);

        _nextReposition = _timing.CurTime + TimeSpan.FromSeconds(pause);
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.RemoveComponent<NPCRangedCombatComponent>(owner);
        entMan.System<NPCSteeringSystem>().Unregister(owner);
    }
}

public sealed partial class AIMeleeAction : AIAction
{
    public AIMeleeAction()
    {
        Name = "AttackMelee";
        Cost = 0.1f;
        Preconditions[AIKeys.MeleeArmed] = AIValues.True;
        Preconditions[AIKeys.InMeleeZone] = AIValues.True;
        Preconditions[AIKeys.TargetVisible] = AIValues.True;
        Effects[AIKeys.TargetDead] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget && AIEquipment.TryFindMelee(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.EnsureComponent<ActiveNPCComponent>(owner);

        if (AIEquipment.TryFindMelee(owner, entMan, out var knife, includeNearby: true))
            AIEquipment.TryHoldItem(owner, knife, entMan);

        var melee = entMan.EnsureComponent<NPCMeleeCombatComponent>(owner);
        melee.Target = comp.Target;
        melee.MissChance = 0.2f;
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Failed;

        if (entMan.System<MobStateSystem>().IsDead(comp.Target))
            return AIActionStatus.Finished;

        if (!comp.MeleeArmed)
            return AIActionStatus.Failed;

        if (comp.TargetDistance > comp.MeleeZoneRadius * 2.5f)
            return AIActionStatus.Failed;

        return AIActionStatus.Continuing;
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.RemoveComponent<NPCMeleeCombatComponent>(owner);
    }
}

public sealed partial class AIReloadAction : AIAction
{
    private const string MagazineSlot = "gun_magazine";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    private TimeSpan _start;

    public AIReloadAction()
    {
        Name = "Reload";
        Cost = 2f;
        Effects[AIKeys.WeaponLoaded] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return !comp.WeaponLoaded && AIEquipment.TryFindGun(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;

        if (AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            AIEquipment.TryHoldItem(owner, gun, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (_timing.CurTime - _start < TimeSpan.FromSeconds(comp.ReloadDuration))
            return AIActionStatus.Continuing;

        return TryRefill(owner, entMan) ? AIActionStatus.Finished : AIActionStatus.Failed;
    }

    private bool TryRefill(EntityUid owner, IEntityManager entMan)
    {
        if (!_gun.TryGetGun(owner, out var gunUid, out _))
            return false;

        var reloaded = false;

        if (entMan.TryGetComponent<BallisticAmmoProviderComponent>(gunUid, out var ownProvider))
        {
            _gun.SetBallisticUnspawned((gunUid, ownProvider), ownProvider.Capacity);
            reloaded = true;
        }

        if (_containers.TryGetContainer(gunUid, MagazineSlot, out var container)
            && container is ContainerSlot { ContainedEntity: { } magazine }
            && entMan.TryGetComponent<BallisticAmmoProviderComponent>(magazine, out var magazineProvider))
        {
            _gun.SetBallisticUnspawned((magazine, magazineProvider), magazineProvider.Capacity);
            reloaded = true;
        }

        if (reloaded
            && entMan.TryGetComponent<ChamberMagazineAmmoProviderComponent>(gunUid, out var chamber)
            && chamber.BoltClosed == false)
        {
            _gun.SetBoltClosed(gunUid, chamber, true);
        }

        return reloaded;
    }
}

public sealed partial class AIMoveToCoverAction : AIAction
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;

    public AIMoveToCoverAction()
    {
        Name = "GoToCover";
        Cost = 2f;
        Effects[AIKeys.AtCover] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget;
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;

        if (!TryGetDestination(owner, comp, entMan, comp.CoverSearchDistance, out var dest))
            return;

        AISteering.Steer(owner, dest, entMan, 0.5f);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Failed;

        if (entMan.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
        {
            if (steering.Status == SteeringStatus.NoPath)
                return AIActionStatus.Failed;

            if (steering.Status == SteeringStatus.InRange)
                return AIActionStatus.Finished;
        }

        return _timing.CurTime - _start >= TimeSpan.FromSeconds(6)
            ? AIActionStatus.Failed
            : AIActionStatus.Continuing;
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);
    }

    private static bool TryGetDestination(
        EntityUid owner,
        AIPlannerComponent comp,
        IEntityManager entMan,
        float distance,
        out EntityCoordinates destination)
    {
        destination = default;

        if (!comp.HasTarget)
            return false;

        var transform = entMan.System<SharedTransformSystem>();
        var xform = entMan.GetComponent<TransformComponent>(owner);

        if (!entMan.TryGetComponent<TransformComponent>(comp.Target, out var targetXform))
            return false;

        var ownerPos = transform.GetWorldPosition(xform);
        var targetPos = transform.GetWorldPosition(targetXform);
        var away = ownerPos - targetPos;

        if (away.LengthSquared() < 0.001f)
            away = new Vector2(1f, 0f);

        away /= away.Length();

        destination = AISteering.FromWorldOffset(owner, away * distance, entMan);
        return true;
    }
}

public sealed partial class AIAmbushAction : AIAction
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private TimeSpan _start;
    private float _sign = 1f;
    private bool _completed;

    public AIAmbushAction()
    {
        Name = "Ambush";
        Cost = 2f;
        Effects[AIKeys.AtAmbushNode] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget;
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
        _completed = false;
        _sign = _random.Prob(0.5f) ? 1f : -1f;

        var transform = entMan.System<SharedTransformSystem>();
        var xform = entMan.GetComponent<TransformComponent>(owner);

        var ownerPos = transform.GetWorldPosition(xform);
        var targetPos = comp.TargetVisible &&
                        entMan.TryGetComponent<TransformComponent>(comp.Target, out var targetXform)
            ? transform.GetWorldPosition(targetXform)
            : comp.LastKnownTargetPosition;

        var away = ownerPos - targetPos;

        if (away.LengthSquared() < 0.001f)
            away = new Vector2(1f, 0f);

        away /= away.Length();

        var flank = new Vector2(-away.Y, away.X) * _sign - away * 0.35f;
        flank /= flank.Length();

        var step = MathF.Min(comp.AmbushDistance, comp.RangedRange * 0.6f);
        var dest = AISteering.FromWorldOffset(owner, flank * step, entMan);

        AISteering.Steer(owner, dest, entMan, 0.75f);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Failed;

        if (entMan.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
        {
            if (steering.Status == SteeringStatus.NoPath)
                return AIActionStatus.Failed;

            if (steering.Status == SteeringStatus.InRange)
            {
                _completed = true;
                return AIActionStatus.Finished;
            }
        }

        if (_timing.CurTime - _start >= TimeSpan.FromSeconds(4))
        {
            _completed = true;
            return AIActionStatus.Finished;
        }

        return AIActionStatus.Continuing;
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);

        if (_completed)
            comp.FlankCooldownUntil = _timing.CurTime + TimeSpan.FromSeconds(comp.FlankCooldown);
    }
}

public sealed partial class AIDodgeAction : AIAction
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;

    public AIDodgeAction()
    {
        Name = "Dodge";
        Cost = 1f;
        Effects[AIKeys.Dodged] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget;
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;

        var transform = entMan.System<SharedTransformSystem>();
        var xform = entMan.GetComponent<TransformComponent>(owner);

        if (!entMan.TryGetComponent<TransformComponent>(comp.Target, out var targetXform))
            return;

        var ownerPos = transform.GetWorldPosition(xform);
        var targetPos = transform.GetWorldPosition(targetXform);
        var away = ownerPos - targetPos;

        if (away.LengthSquared() < 0.001f)
            away = new Vector2(1f, 0f);

        away /= away.Length();

        var side = new Vector2(-away.Y, away.X);
        var dest = AISteering.FromWorldOffset(owner, side * 2f, entMan);

        AISteering.Steer(owner, dest, entMan, 0.5f);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (_timing.CurTime - _start >= TimeSpan.FromSeconds(comp.DodgeDuration))
            return AIActionStatus.Finished;

        if (entMan.TryGetComponent<NPCSteeringComponent>(owner, out var steering) &&
            steering.Status == SteeringStatus.NoPath)
        {
            return AIActionStatus.Finished;
        }

        return AIActionStatus.Continuing;
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);
        comp.DodgeCooldownUntil = _timing.CurTime + TimeSpan.FromSeconds(comp.DodgeCooldown);
    }
}

public sealed partial class AIBlindFireAction : AIAction
{
    private const float FireInterval = 0.22f;
    private const float SpreadRadius = 0.6f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private TimeSpan _start;
    private TimeSpan _nextShot;

    public AIBlindFireAction()
    {
        Name = "BlindFireFromCover";
        Cost = 1f;
        Preconditions[AIKeys.AtCover] = AIValues.True;
        Preconditions[AIKeys.WeaponEquipped] = AIValues.True;
        Preconditions[AIKeys.WeaponWielded] = AIValues.True;
        Preconditions[AIKeys.WeaponLoaded] = AIValues.True;
        Effects[AIKeys.Suppressed] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.InCover
               && comp.HasTarget
               && comp.WeaponLoaded
               && comp.TargetVisible == false
               && AIEquipment.TryFindGun(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
        _nextShot = _start;
        comp.BlindFireUntil = _start + TimeSpan.FromSeconds(comp.BlindFireDuration);

        if (AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            AIEquipment.TryHoldItem(owner, gun, entMan);

        AICombat.ConfigureRanged(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Finished;

        if (!comp.TargetVisible && _timing.CurTime >= _nextShot)
        {
            _nextShot = _timing.CurTime + TimeSpan.FromSeconds(FireInterval);
            FireBlind(owner, comp, entMan);
        }

        return _timing.CurTime >= comp.BlindFireUntil
            ? AIActionStatus.Finished
            : AIActionStatus.Continuing;
    }

    private void FireBlind(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!_gun.TryGetGun(owner, out var gunUid, out var gun) || !_gun.CanShoot(gun))
            return;

        var xform = entMan.GetComponent<TransformComponent>(owner);

        if (xform.MapUid is not { } map)
            return;

        var scatter = _random.NextVector2(SpreadRadius);
        var target = comp.LastKnownTargetPosition + scatter;
        var coordinates = new EntityCoordinates(map, target);

        _gun.AttemptShoot(owner, gunUid, gun, coordinates);
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.RemoveComponent<NPCRangedCombatComponent>(owner);
    }
}

public sealed partial class AISuppressionFireAction : AIAction
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;

    public AISuppressionFireAction()
    {
        Name = "SuppressionFire";
        Cost = 2f;
        Preconditions[AIKeys.WeaponEquipped] = AIValues.True;
        Preconditions[AIKeys.WeaponWielded] = AIValues.True;
        Preconditions[AIKeys.WeaponLoaded] = AIValues.True;
        Effects[AIKeys.Suppressed] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget
               && comp.WeaponLoaded
               && AIEquipment.TryFindGun(owner, entMan, out _, includeNearby: true);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;

        if (AIEquipment.TryFindGun(owner, entMan, out var gun, includeNearby: true))
            AIEquipment.TryHoldItem(owner, gun, entMan);

        AICombat.ConfigureRanged(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Finished;

        return _timing.CurTime - _start >= TimeSpan.FromSeconds(comp.SuppressionDuration)
            ? AIActionStatus.Finished
            : AIActionStatus.Continuing;
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.RemoveComponent<NPCRangedCombatComponent>(owner);
    }
}

public sealed partial class AIGrenadeAction : AIAction
{
    private const float EquipDelay = 0.3f;
    private const float MaxThrowDistance = 10f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private EntityUid _grenade = EntityUid.Invalid;
    private TimeSpan _throwAt;

    public AIGrenadeAction()
    {
        Name = "FlushOutWithGrenade";
        Cost = 1.5f;
        Effects[AIKeys.TargetFlushed] = AIValues.True;
        Effects[AIKeys.WeaponEquipped] = AIValues.False;
        Effects[AIKeys.WeaponWielded] = AIValues.False;
        Effects[AIKeys.MeleeArmed] = AIValues.False;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget
               && comp.GrenadeAvailable
               && comp.SquadCanGrenade
               && comp.TargetDistance <= comp.GrenadeThrowRange
               && AIEquipment.TryFindGrenade(owner, entMan, out _);
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _grenade = EntityUid.Invalid;

        var hands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

        foreach (var held in hands.EnumerateHeld(owner).ToList())
        {
            AIEquipment.TryStowItem(owner, held, entMan, AIEquipment.GunStowSlots);
        }

        if (!AIEquipment.TryFindGrenade(owner, entMan, out var grenade))
            return;

        if (!AIEquipment.TryHoldItem(owner, grenade, entMan))
            return;

        _grenade = grenade;
        _throwAt = _timing.CurTime + TimeSpan.FromSeconds(EquipDelay);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!_grenade.IsValid())
            return AIActionStatus.Failed;

        if (_timing.CurTime < _throwAt)
            return AIActionStatus.Continuing;

        return ThrowAtTarget(owner, comp, entMan)
            ? AIActionStatus.Finished
            : AIActionStatus.Failed;
    }

    private bool ThrowAtTarget(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!entMan.TryGetComponent<TransformComponent>(owner, out var ownerXform) ||
            ownerXform.MapUid is not { } map)
        {
            return false;
        }

        var transform = entMan.System<SharedTransformSystem>();

        var targetPosition = comp.LastKnownTargetPosition;

        if (comp.TargetVisible &&
            entMan.TryGetComponent<TransformComponent>(comp.Target, out var targetXform))
        {
            targetPosition = transform.GetWorldPosition(targetXform);
        }

        if (entMan.TryGetComponent<OnUseTimerTriggerComponent>(_grenade, out var timer))
            _trigger.StartTimer((_grenade, timer), owner);

        var hands = entMan.System<Content.Server.Hands.Systems.HandsSystem>();

        if (!hands.TryDrop(owner, _grenade, checkActionBlocker: false, doDropInteraction: false))
            return false;

        _throwing.TryThrow(
            _grenade,
            new EntityCoordinates(map, targetPosition),
            baseThrowSpeed: 10f,
            user: owner,
            compensateFriction: true,
            recoil: false);

        comp.GrenadeCooldownUntil = _timing.CurTime + TimeSpan.FromSeconds(comp.GrenadeCooldown);
        comp.SquadGrenadeCooldownUntil = comp.GrenadeCooldownUntil;
        comp.SquadCanGrenade = false;

        _grenade = EntityUid.Invalid;
        return true;
    }
}

public sealed partial class AISearchLastKnownAction : AIAction
{
    private const float RetargetInterval = 0.75f;
    private const float ArriveRadius = 1.2f;
    private const float TravelTimeout = 8f;

    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;
    private TimeSpan _nextRetarget;
    private TimeSpan _arrivedAt;
    private bool _arrived;
    private bool _completed;

    public AISearchLastKnownAction()
    {
        Name = "SearchLastKnownPosition";
        Cost = 1.2f;
        Effects[AIKeys.TargetSearched] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.HasTarget && !comp.TargetVisible;
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
        _nextRetarget = _start;
        _arrivedAt = _start;
        _arrived = false;
        _completed = false;

        SteerToLastKnown(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.HasTarget)
            return AIActionStatus.Failed;

        if (comp.TargetVisible)
            return AIActionStatus.Finished;

        if (!_arrived)
        {
            var transform = entMan.System<SharedTransformSystem>();
            var ownerPos = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));

            if (Vector2.Distance(ownerPos, comp.LastKnownTargetPosition) <= ArriveRadius)
            {
                _arrived = true;
                _arrivedAt = _timing.CurTime;
            }

            if (entMan.TryGetComponent<NPCSteeringComponent>(owner, out var steering))
            {
                if (steering.Status == SteeringStatus.InRange)
                {
                    _arrived = true;
                    _arrivedAt = _timing.CurTime;
                }
                else if (steering.Status == SteeringStatus.NoPath)
                {
                    _arrived = true;
                    _arrivedAt = _timing.CurTime;
                }
            }

            if (_timing.CurTime >= _nextRetarget)
            {
                _nextRetarget = _timing.CurTime + TimeSpan.FromSeconds(RetargetInterval);
                SteerToLastKnown(owner, comp, entMan);
            }

            if (_timing.CurTime - _start >= TimeSpan.FromSeconds(TravelTimeout))
            {
                _arrived = true;
                _arrivedAt = _timing.CurTime;
            }

            return AIActionStatus.Continuing;
        }

        if (_timing.CurTime - _arrivedAt >= TimeSpan.FromSeconds(comp.SearchDuration))
        {
            _completed = true;
            return AIActionStatus.Finished;
        }

        return AIActionStatus.Continuing;
    }

    private static void SteerToLastKnown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        var transform = entMan.System<SharedTransformSystem>();
        var ownerPos = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));
        var destination = AISteering.FromWorldOffset(owner, comp.LastKnownTargetPosition - ownerPos, entMan);

        AISteering.Steer(owner, destination, entMan, ArriveRadius);
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);

        if (!_completed || comp.TargetVisible)
            return;

        comp.MemoryTarget = EntityUid.Invalid;
        comp.Target = EntityUid.Invalid;
        comp.HasTarget = false;
        comp.TargetInMemory = false;
        comp.LastSeenAge = 0f;
    }
}

public sealed partial class AIFleeExplosionAction : AIAction
{
    private const float RetargetInterval = 0.3f;
    private const float Timeout = 2f;

    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;
    private TimeSpan _nextRetarget;

    public AIFleeExplosionAction()
    {
        Name = "FleeExplosion";
        Cost = 0.5f;
        Effects[AIKeys.SafeFromExplosion] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return comp.GrenadeDanger;
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
        _nextRetarget = _start;

        MoveAway(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (!comp.GrenadeDanger)
            return AIActionStatus.Finished;

        if (_timing.CurTime - _start >= TimeSpan.FromSeconds(Timeout))
            return AIActionStatus.Finished;

        if (_timing.CurTime >= _nextRetarget)
        {
            _nextRetarget = _timing.CurTime + TimeSpan.FromSeconds(RetargetInterval);
            MoveAway(owner, comp, entMan);
        }

        return AIActionStatus.Continuing;
    }

    private static void MoveAway(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        var transform = entMan.System<SharedTransformSystem>();
        var ownerPos = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));
        var away = ownerPos - comp.GrenadeDangerPosition;

        if (away.LengthSquared() < 0.001f)
            away = new Vector2(1f, 0f);

        away /= away.Length();

        var step = comp.GrenadeDangerRadius + 2.5f;
        var destination = AISteering.FromWorldOffset(owner, away * step, entMan);

        AISteering.Steer(owner, destination, entMan, 0.75f);
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);
    }
}

public sealed partial class AIInspectAction : AIAction
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _start;

    public AIInspectAction()
    {
        Name = "InspectDisturbance";
        Cost = 1f;
        Effects[AIKeys.Inspected] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return !comp.HasTarget;
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _start = _timing.CurTime;
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        return _timing.CurTime - _start >= TimeSpan.FromSeconds(2)
            ? AIActionStatus.Finished
            : AIActionStatus.Continuing;
    }
}

public sealed partial class AIPatrolAction : AIAction
{
    private const float RetargetInterval = 0.5f;
    private const float SlotRetargetDistance = 0.4f;
    private const float ArriveRadius = 0.9f;
    private const float DwellDuration = 2.5f;
    private const float LeaderWaitSpread = 5f;
    private const float LeaderMaxWait = 6f;

    private static readonly Vector2[] FormationOffsets =
    {
        Vector2.Zero,
        new(-1.2f, -1.0f),
        new(1.2f, -1.0f),
        new(-1.2f, 1.0f),
        new(1.2f, 1.0f),
    };

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private TimeSpan _nextRetarget;
    private TimeSpan _arrivedAt;
    private TimeSpan _waitStart;
    private Vector2 _legTarget;
    private Vector2 _lastSlot;
    private bool _hasWaypoint;
    private bool _arrived;
    private bool _following;
    private bool _waiting;

    public AIPatrolAction()
    {
        Name = "Patrol";
        Cost = 2f;
        Effects[AIKeys.PatrolActive] = AIValues.True;
    }

    public override bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return !comp.HasTarget;
    }

    public override void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        _nextRetarget = _timing.CurTime;
        _arrivedAt = _timing.CurTime;
        _hasWaypoint = false;
        _arrived = false;
        _following = false;
        _waiting = false;
        _legTarget = Vector2.Zero;
        _lastSlot = Vector2.Zero;

        RefreshFormation(owner, comp, entMan);
    }

    public override AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime)
    {
        if (comp.HasTarget)
            return AIActionStatus.Finished;

        if (_timing.CurTime >= _nextRetarget)
        {
            _nextRetarget = _timing.CurTime + TimeSpan.FromSeconds(RetargetInterval);
            RefreshFormation(owner, comp, entMan);
        }

        if (_following)
            return AIActionStatus.Continuing;

        if (_hasWaypoint && _arrived &&
            _timing.CurTime - _arrivedAt < TimeSpan.FromSeconds(DwellDuration))
        {
            return AIActionStatus.Continuing;
        }

        if (!_hasWaypoint || _arrived)
        {
            if (WaitForFireteam(comp))
                return AIActionStatus.Continuing;

            PickWaypoint(owner, comp, entMan);
        }

        if (!_arrived)
        {
            var transform = entMan.System<SharedTransformSystem>();
            var position = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));

            if (Vector2.Distance(position, _legTarget) <= ArriveRadius ||
                entMan.TryGetComponent<NPCSteeringComponent>(owner, out var steering) &&
                (steering.Status == SteeringStatus.InRange || steering.Status == SteeringStatus.NoPath))
            {
                _arrived = true;
                _arrivedAt = _timing.CurTime;
            }

            return AIActionStatus.Continuing;
        }

        return AIActionStatus.Continuing;
    }

    private bool WaitForFireteam(AIPlannerComponent comp)
    {
        if (comp.PatrolGroupSize < Math.Max(comp.PatrolMinGroupSize, 2) ||
            comp.PatrolGroupSpread <= LeaderWaitSpread)
        {
            _waiting = false;
            return false;
        }

        if (!_waiting)
        {
            _waiting = true;
            _waitStart = _timing.CurTime;
        }

        if (_timing.CurTime - _waitStart < TimeSpan.FromSeconds(LeaderMaxWait))
            return true;

        _waiting = false;
        return false;
    }

    private void RefreshFormation(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        if (!TryGetLeader(owner, comp, entMan, out var leader))
        {
            if (_following)
            {
                _following = false;
                _hasWaypoint = false;
                _arrived = false;
            }

            return;
        }

        _following = true;
        _waiting = false;
        SteerToSlot(owner, comp, entMan, leader);
    }

    private static bool TryGetLeader(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, out EntityUid leader)
    {
        leader = comp.PatrolLeader;

        return leader.IsValid()
               && leader != owner
               && comp.PatrolGroupSize >= Math.Max(comp.PatrolMinGroupSize, 2)
               && entMan.EntityExists(leader);
    }

    private void SteerToSlot(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, EntityUid leader)
    {
        var transform = entMan.System<SharedTransformSystem>();
        var leaderPosition = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(leader));
        var offset = FormationOffsets[Math.Clamp(comp.PatrolSlot, 0, FormationOffsets.Length - 1)];
        var slot = leaderPosition + offset;

        if (Vector2.Distance(slot, _lastSlot) < SlotRetargetDistance &&
            entMan.TryGetComponent<NPCSteeringComponent>(owner, out var existing) &&
            existing.Status != SteeringStatus.NoPath)
        {
            return;
        }

        _lastSlot = slot;

        var ownerPosition = transform.GetWorldPosition(entMan.GetComponent<TransformComponent>(owner));

        AISteering.Steer(
            owner,
            AISteering.FromWorldOffset(owner, slot - ownerPosition, entMan),
            entMan,
            0.8f);
    }

    private void PickWaypoint(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        var offset = _random.NextVector2(MathF.Max(comp.PatrolRadius, 0.5f) * 0.5f);

        _legTarget = entMan.System<SharedTransformSystem>()
            .GetWorldPosition(entMan.GetComponent<TransformComponent>(owner)) + offset;

        _hasWaypoint = true;
        _arrived = false;
        _waiting = false;

        AISteering.Steer(owner, AISteering.FromWorldOffset(owner, offset, entMan), entMan, 0.75f);
    }

    public override void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        entMan.System<NPCSteeringSystem>().Unregister(owner);
    }
}
