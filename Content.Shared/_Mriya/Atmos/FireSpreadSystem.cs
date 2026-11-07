using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Map;
using Content.Shared._RMC14.OnCollide;
using Content.Shared.Atmos;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Directions;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Mriya.Atmos;

public sealed class FireSpreadSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedRMCFlammableSystem _flammable = default!;

    private readonly List<Entity<TileFireComponent, RMCIgniteOnCollideComponent, TransformComponent>> _due = new();

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;

        _due.Clear();
        var query = EntityQueryEnumerator<TileFireComponent, RMCIgniteOnCollideComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fire, out var ignite, out var xform))
        {
            if (!fire.CanSpread)
                continue;

            if (fire.NextSpreadCheck == TimeSpan.Zero)
            {
                fire.NextSpreadCheck = fire.SpawnedAt + TimeSpan.FromSeconds(fire.SpreadCheckInterval);
                Dirty(uid, fire);
                continue;
            }

            if (now < fire.NextSpreadCheck)
                continue;

            _due.Add((uid, fire, ignite, xform));
        }

        foreach (var ent in _due)
        {
            var (uid, fire, _, _) = (ent.Owner, ent.Comp1, ent.Comp2, ent.Comp3);
            if (TerminatingOrDeleted(uid))
                continue;

            fire.NextSpreadCheck = now + TimeSpan.FromSeconds(fire.SpreadCheckInterval);
            Dirty(uid, fire);

            var despawnAt = fire.SpawnedAt + fire.Duration;
            var timeLeft = despawnAt - now;
            if (timeLeft <= TimeSpan.Zero)
                continue;

            if (timeLeft < fire.Duration * 0.25)
                continue;

            TrySpread(ent, ent.Comp3);
        }

        _due.Clear();
    }

    private void TrySpread(Entity<TileFireComponent, RMCIgniteOnCollideComponent> ent, TransformComponent xform)
    {
        var (uid, fire, ignite) = (ent.Owner, ent.Comp1, ent.Comp2);
        if (fire.DistanceFromOrigin >= fire.MaxSpreadDistance)
            return;

        var coords = _transform.GetMoverCoordinates(uid);
        var gridCoords = coords.SnapToGrid(EntityManager);

        var neighbours = new List<EntityCoordinates>(4);
        foreach (var dir in _rmcMap.CardinalDirections)
        {
            neighbours.Add(gridCoords.Offset(dir));
        }

        for (var i = neighbours.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (neighbours[i], neighbours[j]) = (neighbours[j], neighbours[i]);
        }

        var spawned = 0;
        foreach (var target in neighbours)
        {
            if (spawned >= fire.MaxSpreadsPerCheck)
                break;

            if (!CanSpreadTo(target, fire))
                continue;

            var chance = CalcSpreadChance(target, fire, ignite);
            if (_random.NextFloat() > chance)
                continue;

            SpawnSpreadFire(target, fire, ignite);
            spawned++;
        }
    }

    private bool CanSpreadTo(EntityCoordinates target, TileFireComponent fire)
    {
        if (!_rmcMap.TryGetTileDef(target, out var tile) ||
            tile.ID == ContentTileDefinition.SpaceID)
        {
            return false;
        }

        if (_rmcMap.HasAnchoredEntityEnumerator<TileFireComponent>(target, out var old))
        {
            if (old.Comp.Id == fire.Id)
                return false;

            return false;
        }

        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(target);
        while (anchored.MoveNext(out var ent))
        {
            if (HasComp<BlockTileFireComponent>(ent))
                return false;
        }

        return true;
    }

    private float CalcSpreadChance(EntityCoordinates target, TileFireComponent fire, RMCIgniteOnCollideComponent ignite)
    {
        var childDistance = fire.DistanceFromOrigin + 1;

        var distanceFactor = Math.Max(0f, 1f - fire.DistancePenaltyPerTile * childDistance);
        if (distanceFactor <= 0f)
            return 0f;

        var chance = fire.BaseSpreadChance - fire.SpreadFalloffPerTile * childDistance;
        chance = MathF.Max(chance, fire.MinSpreadChance);
        chance = Math.Clamp(chance, 0f, 1f);

        chance *= distanceFactor;

        var hasItems = false;
        var combinedFlammability = 0f;
        var combinedSupport = 0f;

        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(target);
        while (anchored.MoveNext(out var ent))
        {
            if (!TryComp<FlammabilityComponent>(ent, out var flam))
                continue;

            hasItems = true;
            var f = Math.Clamp(flam.Flammability, 0f, 1f);
            var s = Math.Clamp(flam.BurnSupport, 0f, 1f);
            combinedFlammability = 1f - (1f - combinedFlammability) * (1f - f);
            combinedSupport = 1f - (1f - combinedSupport) * (1f - s);
        }

        if (!hasItems)
        {
            chance *= fire.EmptyTileFactor;
        }
        else
        {
            if (combinedFlammability <= 0.001f)
                return 0f;

            chance *= 0.25f + 0.75f * combinedFlammability;
            chance *= 1f + 0.25f * combinedSupport;
        }

        chance *= 0.8f + 0.2f * Math.Clamp(ignite.Intensity / 30f, 0f, 2f);

        return Math.Clamp(chance, 0f, 1f);
    }

    private void SpawnSpreadFire(EntityCoordinates target, TileFireComponent parent, RMCIgniteOnCollideComponent parentIgnite)
    {
        if (parent.Id is not { } protoId)
            return;

        var spawned = Spawn(protoId, target);
        var childDistance = parent.DistanceFromOrigin + 1;

        if (TryComp<TileFireComponent>(spawned, out var childFire))
        {
            childFire.DistanceFromOrigin = childDistance;
            childFire.NextSpreadCheck = _timing.CurTime + TimeSpan.FromSeconds(childFire.SpreadCheckInterval);

            var supportBonus = GetTileSupportBonus(target);
            if (supportBonus > 0f)
                childFire.Duration += TimeSpan.FromSeconds(supportBonus);

            Dirty(spawned, childFire);
        }

        Entity<RMCIgniteOnCollideComponent?, DamageOnCollideComponent?, TileFireComponent?> spreadTarget = new(spawned, null, null, null);
        _flammable.SetIntensityDuration(spreadTarget, parentIgnite.Intensity, parentIgnite.Duration);
    }

    private float GetTileSupportBonus(EntityCoordinates target)
    {
        var combined = 0f;
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(target);
        while (anchored.MoveNext(out var ent))
        {
            if (!TryComp<FlammabilityComponent>(ent, out var flam))
                continue;

            combined = 1f - (1f - combined) * (1f - Math.Clamp(flam.BurnSupport, 0f, 1f));
        }

        return combined * 10f;
    }
}
