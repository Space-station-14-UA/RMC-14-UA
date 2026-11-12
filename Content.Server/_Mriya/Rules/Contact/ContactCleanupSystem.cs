using System.Linq;
using Content.Server.Decals;
using Content.Server.Spawners.Components;
using Content.Server.Spawners.EntitySystems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

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

    public override void Initialize()
    {
        // after the spawner: its decals already exist, so the exact ids can be recorded
        SubscribeLocalEvent<RandomDecalSpawnerComponent, MapInitEvent>(OnDecalSpawnerMapInit,
            after: [typeof(RandomDecalSpawnerSystem)]);
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

    private void OnDecalSpawnerMapInit(Entity<RandomDecalSpawnerComponent> ent, ref MapInitEvent args)
    {
        if (GetConfig() is not { } config)
            return;

        // the acid splash spawner is parented to the xeno: get grid-local coordinates
        var coords = _transform.GetMoverCoordinates(ent);
        var grid = coords.EntityId;
        if (!HasComp<MapGridComponent>(grid))
            return;

        var deleteAt = _timing.CurTime + config.DecalTime;
        foreach (var (id, decal) in _decal.GetDecalsInRange(grid, coords.Position, ent.Comp.Radius + 1f))
        {
            if (!decal.Id.StartsWith(config.DecalPrefix, StringComparison.Ordinal))
                continue;

            if (_trackedDecals.Add((grid, id)))
                _decals.Add((deleteAt, grid, id));
        }
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