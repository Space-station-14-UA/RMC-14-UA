using System.Numerics;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Doors.Components;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Wizden's pathfinder only checks entities on their own tile, so the parts of a big prop
/// (container, vehicle, ice chunk) that hang over other tiles look walkable to NPCs.
/// After the first landing on the Contact planet, this puts an invisible static helper on every
/// overhang tile with a fixture that covers just the overhang, so those tiles count as blocked.
/// </summary>
public sealed class ContactPathBlockerSystem : EntitySystem
{
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private static readonly EntProtoId BlockerProto = "MRContactPathBlocker";
    private const string FixtureId = "contact-path-blocker";

    // Ignore overhangs thinner than this, they're just fixture padding.
    private const float MinOverlap = 0.05f;

    private readonly HashSet<EntityUid> _doneMaps = new();

    private EntityQuery<ContactPathBlockerComponent> _blockerQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();

        _blockerQuery = GetEntityQuery<ContactPathBlockerComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnDropshipLanded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<ContactPathBlockerSourceComponent, ComponentShutdown>(OnSourceShutdown);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _doneMaps.Clear();
    }

    private void OnDropshipLanded(ref DropshipLandedOnPlanetEvent ev)
    {
        if (!IsContactActive())
            return;

        var dropship = ev.Dropship.Owner;
        if (Transform(dropship).MapUid is not { } map || !_doneMaps.Add(map))
            return;

        var count = SpawnBlockers(map, dropship);
        Log.Info($"Contact: {count} large prop fixtures got pathfinding helpers");
    }

    private void OnSourceShutdown(Entity<ContactPathBlockerSourceComponent> ent, ref ComponentShutdown args)
    {
        foreach (var blocker in ent.Comp.Blockers)
        {
            if (!TerminatingOrDeleted(blocker))
                QueueDel(blocker);
        }

        ent.Comp.Blockers.Clear();
    }

    private bool IsContactActive()
    {
        var query = EntityQueryEnumerator<ContactRuleComponent, ActiveGameRuleComponent>();
        return query.MoveNext(out _, out _, out _);
    }

    private int SpawnBlockers(EntityUid map, EntityUid dropship)
    {
        // Collect first, spawning while enumerating physics bodies isn't safe.
        var overhangs = new List<Overhang>();

        var query = EntityQueryEnumerator<FixturesComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var fixtures, out var body, out var xform))
        {
            if (xform.MapUid != map ||
                !xform.Anchored ||
                body.BodyType != BodyType.Static ||
                xform.GridUid is not { } grid ||
                xform.ParentUid != grid ||
                grid == dropship ||
                !_gridQuery.TryComp(grid, out var gridComp))
            {
                continue;
            }

            if (_blockerQuery.HasComp(uid) || _doorQuery.HasComp(uid))
                continue;

            var ownTile = _map.LocalToTile(grid, gridComp, xform.Coordinates);
            var transform = new Transform(xform.LocalPosition, xform.LocalRotation);
            var tileSize = gridComp.TileSize;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard ||
                    (fixture.CollisionLayer & (int) CollisionGroup.MobMask) == 0)
                {
                    continue;
                }

                var aabb = fixture.Shape.ComputeAABB(transform, 0);

                var minX = (int) MathF.Floor((aabb.Left + MinOverlap) / tileSize);
                var maxX = (int) MathF.Floor((aabb.Right - MinOverlap) / tileSize);
                var minY = (int) MathF.Floor((aabb.Bottom + MinOverlap) / tileSize);
                var maxY = (int) MathF.Floor((aabb.Top - MinOverlap) / tileSize);

                for (var x = minX; x <= maxX; x++)
                {
                    for (var y = minY; y <= maxY; y++)
                    {
                        var tile = new Vector2i(x, y);
                        if (tile == ownTile)
                            continue;

                        var tileBox = new Box2(x * tileSize, y * tileSize, (x + 1) * tileSize, (y + 1) * tileSize);
                        var overlap = aabb.Intersect(tileBox);
                        if (overlap.Width < MinOverlap || overlap.Height < MinOverlap)
                            continue;

                        overhangs.Add(new Overhang(uid, grid, tile, tileSize, overlap,
                            fixture.CollisionLayer, fixture.CollisionMask));
                    }
                }
            }
        }

        foreach (var overhang in overhangs)
        {
            SpawnBlocker(overhang);
        }

        return overhangs.Count;
    }

    private void SpawnBlocker(Overhang overhang)
    {
        var center = new Vector2(overhang.Tile.X + 0.5f, overhang.Tile.Y + 0.5f) * overhang.TileSize;
        var blocker = Spawn(BlockerProto, new EntityCoordinates(overhang.Grid, center));

        // Pathfinder tests points in the helper's local space, so shift the box to the tile centre.
        var shape = new PolygonShape();
        shape.SetAsBox(new Box2(overhang.Box.BottomLeft - center, overhang.Box.TopRight - center));

        // Impassable so the pathfinder blocks it even if the prop sits on a low/table layer.
        _fixtures.TryCreateFixture(blocker, shape, FixtureId,
            hard: true,
            collisionLayer: overhang.Layer | (int) CollisionGroup.Impassable,
            collisionMask: overhang.Mask);

        // Fixtures added after spawn don't dirty the pathfinding chunk, CollisionChangeEvent does.
        _physics.SetCanCollide(blocker, false);
        _physics.SetCanCollide(blocker, true);

        EnsureComp<ContactPathBlockerComponent>(blocker).Source = overhang.Prop;
        EnsureComp<ContactPathBlockerSourceComponent>(overhang.Prop).Blockers.Add(blocker);
    }

    private readonly record struct Overhang(
        EntityUid Prop,
        EntityUid Grid,
        Vector2i Tile,
        ushort TileSize,
        Box2 Box,
        int Layer,
        int Mask);
}