using Content.Server._RMC14.Atmos;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._Mriya.Explosion;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Map;
using Content.Shared.Atmos.Components;
using Content.Shared.Coordinates.Helpers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.Explosion;

public sealed class CookOffSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly RMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private float _accum;
    private const float CheckInterval = 0.25f;

    private readonly List<Entity<CookOffComponent, TransformComponent>> _due = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CookOffComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CookOffComponent, ExplosionReceivedEvent>(OnExplosionReceived);
    }

    private void OnMapInit(Entity<CookOffComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.MaxCookTime < ent.Comp.MinCookTime)
            ent.Comp.MaxCookTime = ent.Comp.MinCookTime;
    }

    private void OnExplosionReceived(Entity<CookOffComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (ent.Comp.Exploded || TerminatingOrDeleted(ent))
            return;

        var now = _timing.CurTime;
        if (!ent.Comp.Cooking)
        {
            StartCooking(ent, now, true);
        }
        else
        {
            var fast = now + TimeSpan.FromSeconds(0.5);
            if (fast < ent.Comp.CookUntil)
            {
                ent.Comp.CookUntil = fast;
                Dirty(ent);
            }
        }
    }

    public override void Update(float frameTime)
    {
        _accum += frameTime;
        if (_accum < CheckInterval)
            return;

        _accum = 0f;
        var now = _timing.CurTime;

        _due.Clear();
        var query = EntityQueryEnumerator<CookOffComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var cook, out var xform))
        {
            if (cook.Exploded || TerminatingOrDeleted(uid))
                continue;

            _due.Add((uid, cook, xform));
        }

        foreach (var ent in _due)
        {
            var (uid, cook, _) = (ent.Owner, ent.Comp1, ent.Comp2);
            if (cook.Exploded || TerminatingOrDeleted(uid))
                continue;

            if (cook.Cooking)
            {
                if (now >= cook.CookUntil)
                    Detonate(ent);
                continue;
            }

            if (ShouldStartCooking(uid, out var multiplier))
                StartCooking(ent, now, false, multiplier);
        }

        _due.Clear();
    }

    private bool ShouldStartCooking(EntityUid uid, out float multiplier)
    {
        multiplier = 1f;

        if (TryComp<FlammableComponent>(uid, out var flammable) && flammable.OnFire)
            return true;

        var coords = _transform.GetMoverCoordinates(uid);
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(coords);
        var foundFire = false;
        while (anchored.MoveNext(out var ent))
        {
            if (ent == uid)
                continue;

            if (!TryComp<TileFireComponent>(ent, out var fire))
                continue;

            foundFire = true;
            multiplier = Math.Min(multiplier, fire.CookOffMultiplier);
        }

        if (multiplier <= 0f)
            multiplier = 1f;

        return foundFire;
    }

    private void StartCooking(Entity<CookOffComponent> ent, TimeSpan now, bool fromExplosion, float fireMultiplier = 1f)
    {
        var (uid, cook) = (ent.Owner, ent.Comp);
        if (cook.Cooking || cook.Exploded)
            return;

        var delay = fromExplosion
            ? Math.Min(1.5f, cook.MinCookTime)
            : _random.NextFloat(cook.MinCookTime, Math.Max(cook.MinCookTime, cook.MaxCookTime));

        if (!fromExplosion)
        {
            if (fireMultiplier <= 0f)
                fireMultiplier = 1f;

            delay *= fireMultiplier;
            delay = Math.Max(0.5f, delay);
        }

        cook.Cooking = true;
        cook.CookUntil = now + TimeSpan.FromSeconds(delay);
        Dirty(ent);

        var flammable = EnsureComp<FlammableComponent>(uid);
        _rmcFlammable.Ignite((uid, flammable), 15, (int)Math.Ceiling(delay) + 2, 45);
    }

    private void Detonate(Entity<CookOffComponent> ent)
    {
        var (uid, cook) = (ent.Owner, ent.Comp);
        if (cook.Exploded || TerminatingOrDeleted(uid))
            return;

        cook.Exploded = true;
        cook.Cooking = false;
        Dirty(ent);

        var coords = _transform.GetMoverCoordinates(uid).SnapToGrid(EntityManager);

        _explosion.TriggerExplosive(uid);

        if (cook.SpawnFireAfter)
        {
            try
            {
                _rmcFlammable.SpawnFireDiamond(cook.FireProto, coords, cook.FireRange, cook.FireIntensity, cook.FireDuration);
            }
            catch (Exception e)
            {
                Log.Error($"CookOff failed to spawn fire after explosion for {ToPrettyString(uid)}:\n{e}");
            }
        }

        if (cook.ExtinguishFireAfter)
            ExtinguishNearbyFires(coords, cook.ExtinguishRange);
    }

    private void ExtinguishNearbyFires(EntityCoordinates coords, int range)
    {
        var fires = _lookup.GetEntitiesInRange<TileFireComponent>(coords, range);
        foreach (var fire in fires)
        {
            if (TerminatingOrDeleted(fire))
                continue;

            QueueDel(fire);
        }
    }
}
