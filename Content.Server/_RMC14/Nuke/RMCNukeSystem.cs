using Content.Server._RMC14.Power;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Sensor;
using Content.Shared._RMC14.Vents;
using Content.Shared._RMC14.Xenonids.Construction.Tunnel;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._RMC14.Nuke;

public sealed class RMCNukeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly RMCGibSystem _rmcGib = default!;
    [Dependency] private readonly SensorTowerSystem _sensorTower = default!;
    [Dependency] private readonly RMCPowerSystem _power = default!;

    private readonly DamageSpecifier _damage = new() { DamageDict = { ["Blunt"] = 1e10, ["Heat"] = 1e10 } };
    private readonly HashSet<EntityUid> _gridContents = new();
    private EntityQuery<RMCRepairableComponent> _repairable;

    public override void Initialize()
    {
        base.Initialize();
        _repairable = GetEntityQuery<RMCRepairableComponent>();
    }

    private void KillEverythingOnMap(MapId mapId)
    {
        var toDamage = new HashSet<EntityUid>();
        var toDelete = new HashSet<EntityUid>();
        var forcedMobDeletes = new HashSet<EntityUid>();
        var tunnelsToDelete = new HashSet<EntityUid>();
        var affectedGrids = GetAffectedGrids(mapId);

        var living = EntityQueryEnumerator<DamageableComponent, TransformComponent>();
        var tunnels = EntityQueryEnumerator<XenoTunnelComponent, TransformComponent>();
        var vents = EntityQueryEnumerator<VentCrawlableComponent, TransformComponent>();
        var tunneled = EntityQueryEnumerator<InXenoTunnelComponent>();
        while (living.MoveNext(out var uid, out var _, out var transform))
        {
            if (!IsInNukedArea(transform, mapId, affectedGrids))
                continue;

            AddNukeTarget(uid, toDamage, toDelete);
        }
        while (tunnels.MoveNext(out var uid, out _, out var transform))
        {
            if (!IsInNukedArea(transform, mapId, affectedGrids))
                continue;

            AddTunnelContents(uid, toDelete);
            tunnelsToDelete.Add(uid);
        }
        while (vents.MoveNext(out var uid, out var _, out var transform))
        {
            if (!IsInNukedArea(transform, mapId, affectedGrids))
                continue;

            AddNukeTarget(uid, toDamage, toDelete);
        }
        while (tunneled.MoveNext(out var uid, out _))
        {
            toDelete.Add(uid);
        }
        AddAffectedGridMobs(affectedGrids, toDamage, forcedMobDeletes);

        toDelete.ExceptWith(toDamage);
        toDelete.ExceptWith(forcedMobDeletes);

        // Mobs and repairables go through damage so death/destruction events can run before the map cleanup.
        foreach (var uid in toDamage)
        {
            _damageable.TryChangeDamage(uid, _damage, true);
        }

        // The nuke must be final even when a mob on a docked dropship is not caught correctly by normal damage flow.
        foreach (var uid in forcedMobDeletes)
        {
            if (TerminatingOrDeleted(uid) || _entity.IsQueuedForDeletion(uid))
                continue;

            _rmcGib.ScatterInventoryItems(uid);
            _entity.TryQueueDeleteEntity(uid);
        }

        foreach (var uid in toDelete)
        {
            _rmcGib.ScatterInventoryItems(uid);
            _entity.TryQueueDeleteEntity(uid);
        }

        foreach (var tunnel in tunnelsToDelete)
        {
            DeleteTunnelWithoutDroppingContents(tunnel);
        }

        var sensors = EntityQueryEnumerator<SensorTowerComponent, TransformComponent>();
        var generators = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (sensors.MoveNext(out var uid, out var sensor, out var transform))
        {
            if (!IsInNukedArea(transform, mapId, affectedGrids))
                continue;

            _sensorTower.FullyDestroy(new(uid, sensor));
        }
        while (generators.MoveNext(out var uid, out var generator, out var transform))
        {
            if (!IsInNukedArea(transform, mapId, affectedGrids))
                continue;

            _power.FullyDestroy(new(uid, generator));
        }
    }

    private void AddNukeTarget(EntityUid uid, HashSet<EntityUid> toDamage, HashSet<EntityUid> toDelete)
    {
        if (HasComp<MobStateComponent>(uid) || _repairable.HasComp(uid))
            toDamage.Add(uid);
        else
            toDelete.Add(uid);
    }

    private HashSet<EntityUid> GetAffectedGrids(MapId mapId)
    {
        var affected = new HashSet<EntityUid>();
        var grids = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (grids.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapID == mapId)
                affected.Add(uid);
        }

        AddParkedDropships(mapId, affected);
        return affected;
    }

    private void AddParkedDropships(MapId mapId, HashSet<EntityUid> affected)
    {
        var destinations = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (destinations.MoveNext(out _, out var destination, out var transform))
        {
            if (transform.MapID != mapId ||
                destination.Ship is not { } ship ||
                !HasComp<MapGridComponent>(ship))
            {
                continue;
            }

            affected.Add(ship);
        }
    }

    private void AddAffectedGridMobs(HashSet<EntityUid> affectedGrids, HashSet<EntityUid> toDamage, HashSet<EntityUid> forcedDeletes)
    {
        foreach (var grid in affectedGrids)
        {
            if (!TryComp(grid, out MapGridComponent? mapGrid))
                continue;

            _gridContents.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid,
                mapGrid.LocalAABB,
                _gridContents,
                LookupFlags.Uncontained | LookupFlags.Approximate);
            AddTransformChildren(grid, _gridContents);

            foreach (var uid in _gridContents)
            {
                if (!HasComp<MobStateComponent>(uid))
                    continue;

                if (HasComp<DamageableComponent>(uid))
                    toDamage.Add(uid);

                forcedDeletes.Add(uid);
            }
        }
    }

    private void AddTransformChildren(EntityUid uid, HashSet<EntityUid> contents)
    {
        if (!TryComp(uid, out TransformComponent? transform))
            return;

        var children = transform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (!contents.Add(child))
                continue;

            AddTransformChildren(child, contents);
        }
    }

    private bool IsInNukedArea(TransformComponent transform, MapId mapId, HashSet<EntityUid> affectedGrids)
    {
        return transform.MapID == mapId ||
               transform.GridUid is { } grid && affectedGrids.Contains(grid);
    }

    private void AddTunnelContents(EntityUid tunnel, HashSet<EntityUid> toDelete)
    {
        if (!_container.TryGetContainer(tunnel, XenoTunnelComponent.ContainedMobsContainerId, out var container))
            return;

        foreach (var contained in container.ContainedEntities)
        {
            toDelete.Add(contained);
        }
    }

    private void DeleteTunnelWithoutDroppingContents(EntityUid tunnel)
    {
        if (TerminatingOrDeleted(tunnel))
            return;

        _hive.RemoveTunnelFromHiveLists(tunnel);
        RemComp<XenoTunnelComponent>(tunnel);
        _entity.DeleteEntity(tunnel);
    }

    public void NukeMap(MapId mapId)
    {
        for (var i = 0; i < 3; i++)
            KillEverythingOnMap(mapId);
    }
}
