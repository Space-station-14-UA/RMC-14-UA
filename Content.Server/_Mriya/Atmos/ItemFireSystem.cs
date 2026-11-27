using Content.Server._RMC14.Atmos;
using Content.Shared._Mriya.Atmos;
using Content.Shared._Mriya.Explosion;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Map;
using Content.Shared.Atmos.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.Atmos;

public sealed class ItemFireSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly RMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private static readonly EntProtoId DefaultTileFire = "RMCTileFire";

    private float _accum;
    private const float CheckInterval = 0.5f;

    private readonly List<Entity<FlammabilityComponent, TransformComponent>> _igniteDue = new();
    private readonly List<Entity<BurningItemComponent, TransformComponent>> _burnDue = new();

    public override void Update(float frameTime)
    {
        _accum += frameTime;
        if (_accum < CheckInterval)
            return;

        _accum = 0f;
        var now = _timing.CurTime;

        TryIgniteItems(now);
        UpdateBurningItems(now);
    }

    private void TryIgniteItems(TimeSpan now)
    {
        _igniteDue.Clear();
        var query = EntityQueryEnumerator<FlammabilityComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flam, out var xform))
        {
            if (HasComp<BurningItemComponent>(uid))
                continue;

            if (HasComp<CookOffComponent>(uid))
                continue;

            _igniteDue.Add((uid, flam, xform));
        }

        foreach (var ent in _igniteDue)
        {
            var (uid, flam, _) = (ent.Owner, ent.Comp1, ent.Comp2);
            if (TerminatingOrDeleted(uid))
                continue;

            var f = Math.Clamp(flam.Flammability, 0f, 1f);
            if (f <= 0.001f)
                continue;

            if (!IsOnBurningTile(uid, out var intensity))
                continue;

            var chance = Math.Clamp(f * (0.4f + 0.6f * Math.Clamp(intensity / 30f, 0f, 1.5f)), 0f, 1f);
            if (_random.NextFloat() > chance * CheckInterval * 2f)
                continue;

            IgniteItem((uid, flam), now, intensity);
        }

        _igniteDue.Clear();
    }

    private void IgniteItem(Entity<FlammabilityComponent> ent, TimeSpan now, int fireIntensity)
    {
        var (uid, flam) = (ent.Owner, ent.Comp);

        FlammableComponent flammable;
        if (!TryComp<FlammableComponent>(uid, out var existing))
        {
            flammable = EnsureComp<FlammableComponent>(uid);
            flammable.Damage = new DamageSpecifier();
            flammable.Damage.DamageDict.Add("Heat", 2f * flam.BurnDamageMultiplier);
            flammable.FirestacksOnIgnite = 2f;
            flammable.MaximumFireStacks = 45f;
        }
        else
        {
            flammable = existing;
            if (flammable.OnFire)
                return;
        }

        var duration = 10 + (int)flam.BurnDurationBonus;
        if (!_rmcFlammable.Ignite((uid, flammable), Math.Max(1, fireIntensity), duration, 45))
            return;

        var burn = EnsureComp<BurningItemComponent>(uid);
        burn.State = ItemBurnState.Igniting;
        burn.IgnitedAt = now;
        burn.BurnUntil = now + TimeSpan.FromSeconds(duration + flam.BurnSupport * 20f);
        burn.BurntOut = false;
        burn.SpawnedTileFire = false;
        burn.FuelLeft = 1f;
        Dirty(uid, burn);
    }

    private void UpdateBurningItems(TimeSpan now)
    {
        _burnDue.Clear();
        var query = EntityQueryEnumerator<BurningItemComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var burn, out var xform))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (burn.State == ItemBurnState.BurntOut)
                continue;

            _burnDue.Add((uid, burn, xform));
        }

        foreach (var ent in _burnDue)
        {
            var (uid, burn, xform) = (ent.Owner, ent.Comp1, ent.Comp2);
            if (TerminatingOrDeleted(uid))
                continue;

            if (burn.State == ItemBurnState.Igniting && now >= burn.IgnitedAt + TimeSpan.FromSeconds(0.5))
            {
                burn.State = ItemBurnState.Burning;
                Dirty(uid, burn);
            }

            var total = (burn.BurnUntil - burn.IgnitedAt).TotalSeconds;
            var left = (burn.BurnUntil - now).TotalSeconds;
            if (total > 0)
                burn.FuelLeft = Math.Clamp((float)(left / total), 0f, 1f);

            if (burn.State == ItemBurnState.Burning && total > 0 && left < total * 0.25)
            {
                burn.State = ItemBurnState.Smouldering;
                Dirty(uid, burn);
            }

            if (!burn.SpawnedTileFire &&
                burn.State == ItemBurnState.Burning &&
                TryComp<FlammabilityComponent>(uid, out var flam) &&
                flam.BurnSupport > 0.3f)
            {
                burn.SpawnedTileFire = true;
                Dirty(uid, burn);
                EnsureTileFireForBurningItem(uid, xform, flam);
            }

            if ((burn.State == ItemBurnState.Burning || burn.State == ItemBurnState.Smouldering) &&
                TryComp<DamageableComponent>(uid, out _))
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict.Add("Heat", burn.State == ItemBurnState.Burning ? 1.5f : 0.5f);
                _damageable.TryChangeDamage(uid, dmg, true, false);
            }

            if (now >= burn.BurnUntil)
            {
                burn.State = ItemBurnState.BurntOut;
                burn.BurntOut = true;
                burn.FuelLeft = 0f;
                Dirty(uid, burn);

                if (TryComp<FlammableComponent>(uid, out var flammable))
                    _rmcFlammable.Extinguish((uid, flammable));

                RemCompDeferred<OnFireComponent>(uid);
            }
        }

        _burnDue.Clear();
    }

    private bool IsOnBurningTile(EntityUid uid, out int intensity)
    {
        intensity = 0;
        var coords = _transform.GetMoverCoordinates(uid);
        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(coords);
        while (anchored.MoveNext(out var ent))
        {
            if (ent == uid)
                continue;

            if (!TryComp<TileFireComponent>(ent, out _) ||
                !TryComp<RMCIgniteOnCollideComponent>(ent, out var ignite))
            {
                continue;
            }

            intensity = Math.Max(intensity, ignite.Intensity);
        }

        return intensity > 0;
    }

    private void EnsureTileFireForBurningItem(EntityUid uid, TransformComponent xform, FlammabilityComponent flam)
    {
        var coords = _transform.GetMoverCoordinates(uid);
        if (_rmcMap.HasAnchoredEntityEnumerator<TileFireComponent>(coords))
            return;

        var snap = coords.SnapToGrid(EntityManager);
        _rmcFlammable.SpawnFireDiamond(DefaultTileFire, snap, 0, 15, 10 + (int)flam.BurnDurationBonus);
    }
}
