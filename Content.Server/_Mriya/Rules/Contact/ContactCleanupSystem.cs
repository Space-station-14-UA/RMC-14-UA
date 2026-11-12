using System.Linq;
using Content.Server.Decals;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact cleanup: deletes dead xenos and their acid blood decals after a while,
/// so long rounds don't fill the colony with bodies and splatter.
/// Devoured marines are released by RMC itself (regurgitated on death and on terminate).
/// </summary>
public sealed class ContactCleanupSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly DecalSystem _decal = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, TimeSpan> _deadSince = new();
    private readonly List<(TimeSpan DeleteAt, EntityUid Grid, uint Id)> _decals = new();
    private readonly HashSet<(EntityUid Grid, uint Id)> _trackedDecals = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly List<EntityUid> _toDelete = new();
    private TimeSpan _nextCheck;

    /// <summary>
    /// Decal spawners seen since the last check: decals around them get picked up on the next check.
    /// </summary>
    private readonly List<(EntityUid Grid, Vector2 Position, float Radius)> _pendingSpawners = new();

    public override void Initialize()
    {
        // MapInit is taken by RandomDecalSpawnerSystem (one subscription per comp+event):
        // remember where the spawner was, its decals exist by the next check
        SubscribeLocalEvent<RandomDecalSpawnerComponent, ComponentStartup>(OnDecalSpawnerStartup);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private ContactCleanupComponent? GetConfig()
    {
        var query = EntityQueryEnumerator<ContactCleanupComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var config, out _))
        {
            return config;
        }

        return null;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _deadSince.Clear();
        _decals.Clear();
        _trackedDecals.Clear();
        _pendingSpawners.Clear();
        _nextCheck = TimeSpan.Zero;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextCheck)
            return;

        _nextCheck = now + CheckEvery;

        if (GetConfig() is not { } config)
            return;

        UpdateBodies(config, now);
        CollectDecals(config, now);
        UpdateDecals(now);
    }

    #region Bodies

    private void UpdateBodies(ContactCleanupComponent config, TimeSpan now)
    {
        _seen.Clear();
        _toDelete.Clear();

        var query = EntityQueryEnumerator<XenoComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (!_mobState.IsDead(uid, mobState))
                continue;

            _seen.Add(uid);
            if (!_deadSince.TryGetValue(uid, out var since))
            {
                _deadSince[uid] = now;
                continue;
            }

            if (now - since >= config.BodyTime)
                _toDelete.Add(uid);
        }

        // revived or already gone
        foreach (var uid in _deadSince.Keys.ToList())
        {
            if (!_seen.Contains(uid))
                _deadSince.Remove(uid);
        }

        // devoured marines are already out: RMC regurgitates on death (and again on terminate)
        foreach (var uid in _toDelete)
        {
            _deadSince.Remove(uid);
            QueueDel(uid);
        }
    }

    #endregion

    #region Decals

    private void OnDecalSpawnerStartup(Entity<RandomDecalSpawnerComponent> ent, ref ComponentStartup args)
    {
        if (GetConfig() == null)
            return;

        // the acid splash spawner is parented to the xeno: get grid-local coordinates
        var coords = _transform.GetMoverCoordinates(ent);
        if (!HasComp<MapGridComponent>(coords.EntityId))
            return;

        _pendingSpawners.Add((coords.EntityId, coords.Position, ent.Comp.Radius));
    }

    /// <summary>
    /// Xeno blood decals around spawners seen since the last check start their timer.
    /// </summary>
    private void CollectDecals(ContactCleanupComponent config, TimeSpan now)
    {
        if (_pendingSpawners.Count == 0)
            return;

        var deleteAt = now + config.DecalTime;
        foreach (var (grid, position, radius) in _pendingSpawners)
        {
            if (TerminatingOrDeleted(grid))
                continue;

            foreach (var (id, decal) in _decal.GetDecalsInRange(grid, position, radius + 1f))
            {
                if (!decal.Id.StartsWith(config.DecalPrefix, StringComparison.Ordinal))
                    continue;

                if (_trackedDecals.Add((grid, id)))
                    _decals.Add((deleteAt, grid, id));
            }
        }

        _pendingSpawners.Clear();
    }

    private void UpdateDecals(TimeSpan now)
    {
        for (var i = _decals.Count - 1; i >= 0; i--)
        {
            var (deleteAt, grid, id) = _decals[i];
            if (now < deleteAt)
                continue;

            _decals.RemoveAt(i);
            _trackedDecals.Remove((grid, id));

            if (!TerminatingOrDeleted(grid))
                _decal.RemoveDecal(grid, id);
        }
    }

    #endregion
}