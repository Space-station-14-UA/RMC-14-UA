using Content.Server._RMC14.Dropship;
using Content.Server._RMC14.Marines;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact shuttle cycle on top of RMC's DropshipSystem.FlyTo:
/// WaitingForBoarding -> Countdown -> FlyingDown -> Unloading -> Returning -> Refueling -> Ready
/// -> (reinforcement terminal) -> FlyingDown -> loop
/// </summary>
public sealed class ContactShuttleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DropshipSystem _dropship = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ContactReinforcementTerminalComponent, ActivateInWorldEvent>(OnTerminalActivate);
    }

    private ContactShuttleComponent? GetActive()
    {
        var query = EntityQueryEnumerator<ContactShuttleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var comp, out _))
        {
            return comp;
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ContactShuttleComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var comp, out _))
        {
            if (now >= comp.NextHudSync)
            {
                comp.NextHudSync = now + comp.HudSyncEvery;
                SendHud(comp);
            }

            if (now < comp.NextCheck)
                continue;

            comp.NextCheck = now + comp.CheckEvery;

            if (TryGetDropship(comp, out var dropship))
                Step(comp, dropship, now);
        }
    }

    private void Step(ContactShuttleComponent comp, EntityUid dropship, TimeSpan now)
    {
        switch (comp.State)
        {
            case ContactShuttleState.WaitingForBoarding:
                if (OnShip(dropship) && CountAboard(dropship) > 0)
                {
                    SetState(comp, ContactShuttleState.Countdown, now + comp.BoardingCountdown);
                    Announce("mriya-contact-shuttle-boarding", ("seconds", (int) comp.BoardingCountdown.TotalSeconds));
                }

                break;

            case ContactShuttleState.Countdown:
                // everyone's aboard: don't make them wait the full countdown
                var fullBoardAt = now + comp.FullBoardCountdown;
                if (comp.StateEndsAt > fullBoardAt && AllMarinesAboard(dropship))
                {
                    SetState(comp, ContactShuttleState.Countdown, fullBoardAt);
                    Announce("mriya-contact-shuttle-full-board", ("seconds", (int) comp.FullBoardCountdown.TotalSeconds));
                }

                if (now >= comp.StateEndsAt)
                    LaunchDown(comp, dropship);

                break;

            case ContactShuttleState.FlyingDown:
                if (OnPlanet(dropship))
                {
                    SetState(comp, ContactShuttleState.Unloading, now + comp.UnloadTime);
                    Announce("mriya-contact-shuttle-landed", ("seconds", (int) comp.UnloadTime.TotalSeconds));
                }

                break;

            case ContactShuttleState.Unloading:
                if (now < comp.StateEndsAt)
                    break;

                EjectStragglers(dropship);
                if (comp.ShipDock is { } dock && Launch(dropship, dock))
                    SetState(comp, ContactShuttleState.Returning, null);

                break;

            case ContactShuttleState.Returning:
                if (OnShip(dropship))
                {
                    SetState(comp, ContactShuttleState.Refueling, now + comp.RefuelTime);
                    Announce("mriya-contact-shuttle-refueling", ("seconds", (int) comp.RefuelTime.TotalSeconds));
                }

                break;

            case ContactShuttleState.Refueling:
                if (now >= comp.StateEndsAt)
                {
                    SetState(comp, ContactShuttleState.Ready, null);
                    Announce("mriya-contact-shuttle-ready");
                }

                break;

            case ContactShuttleState.Ready:
                // waits for the reinforcement terminal
                break;
        }
    }

    #region Flights

    private void LaunchDown(ContactShuttleComponent comp, EntityUid dropship)
    {
        comp.ShipDock ??= FindNearestDestination(dropship, planet: false);
        comp.LandingZone ??= FindDestination(planet: true);

        if (comp.LandingZone is not { } lz)
        {
            Log.Error("Contact shuttle: no landing zone on the planet");
            return;
        }

        if (!Launch(dropship, lz))
            return;

        SetState(comp, ContactShuttleState.FlyingDown, null);
        Announce("mriya-contact-shuttle-launch");
    }

    private bool Launch(EntityUid dropship, EntityUid destination)
    {
        if (!TryGetComputer(dropship, out var computer))
        {
            Log.Error($"Contact shuttle: no navigation computer on {ToPrettyString(dropship)}");
            return false;
        }

        return _dropship.FlyTo(computer, destination, null);
    }

    /// <summary>
    /// Nobody rides back up: marines still aboard are moved next to a teammate on the colony.
    /// If nobody's on the colony, they ride back (and the round's "all marines on colony dead" check takes over).
    /// </summary>
    private void EjectStragglers(EntityUid dropship)
    {
        var colony = new List<EntityUid>();
        var stragglers = new List<EntityUid>();

        var query = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (xform.GridUid == dropship)
                stragglers.Add(uid);
            else if (_rmcPlanet.IsOnPlanet(xform.Coordinates))
                colony.Add(uid);
        }

        if (colony.Count == 0)
            return;

        foreach (var marine in stragglers)
        {
            _transform.SetCoordinates(marine, Transform(_random.Pick(colony)).Coordinates);
            _popup.PopupEntity(Loc.GetString("mriya-contact-shuttle-ejected"), marine, marine, PopupType.MediumCaution);
        }
    }

    #endregion

    #region Reinforcement terminal

    private void OnTerminalActivate(Entity<ContactReinforcementTerminalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (GetActive() is not { } comp || !TryGetDropship(comp, out var dropship))
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-reinforcements-offline"), ent, args.User);
            return;
        }

        switch (comp.State)
        {
            case ContactShuttleState.Ready:
                if (CountAboard(dropship) == 0)
                {
                    _popup.PopupEntity(Loc.GetString("mriya-contact-reinforcements-empty"), ent, args.User);
                    return;
                }

                LaunchDown(comp, dropship);
                _popup.PopupEntity(Loc.GetString("mriya-contact-reinforcements-called"), ent, args.User);
                return;

            case ContactShuttleState.Refueling:
                var left = (comp.StateEndsAt ?? _timing.CurTime) - _timing.CurTime;
                _popup.PopupEntity(Loc.GetString("mriya-contact-reinforcements-refueling",
                    ("seconds", Math.Max(0, (int) left.TotalSeconds))), ent, args.User);
                return;

            default:
                _popup.PopupEntity(Loc.GetString("mriya-contact-reinforcements-busy"), ent, args.User);
                return;
        }
    }

    #endregion

    #region Helpers

    private bool TryGetDropship(ContactShuttleComponent comp, out EntityUid dropship)
    {
        if (comp.Dropship is { } cached && Exists(cached))
        {
            dropship = cached;
            return true;
        }

        // TODO Mriya: the Contact map should have a single dropship; pick a specific one if it ever has more
        var query = EntityQueryEnumerator<DropshipComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            comp.Dropship = uid;
            dropship = uid;
            return true;
        }

        dropship = default;
        return false;
    }

    private bool TryGetComputer(EntityUid dropship, out Entity<DropshipNavigationComputerComponent> computer)
    {
        var query = EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != dropship)
                continue;

            computer = (uid, comp);
            return true;
        }

        computer = default;
        return false;
    }

    private EntityUid? FindDestination(bool planet)
    {
        var found = new List<EntityUid>();
        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_rmcPlanet.IsOnPlanet(xform.Coordinates) == planet)
                found.Add(uid);
        }

        return found.Count == 0 ? null : _random.Pick(found);
    }

    private EntityUid? FindNearestDestination(EntityUid dropship, bool planet)
    {
        var shipPos = _transform.GetMapCoordinates(dropship);
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_rmcPlanet.IsOnPlanet(xform.Coordinates) != planet)
                continue;

            var pos = _transform.GetMapCoordinates(uid, xform);
            if (pos.MapId != shipPos.MapId)
                continue;

            var dist = (pos.Position - shipPos.Position).LengthSquared();
            if (dist < bestDist)
            {
                best = uid;
                bestDist = dist;
            }
        }

        return best ?? FindDestination(planet);
    }

    private bool OnPlanet(EntityUid dropship)
    {
        return _rmcPlanet.IsOnPlanet(Transform(dropship).Coordinates);
    }

    private bool OnShip(EntityUid dropship)
    {
        var map = Transform(dropship).MapID;
        var query = EntityQueryEnumerator<AlmayerComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == map)
                return true;
        }

        return false;
    }

    private int CountAboard(EntityUid dropship)
    {
        var count = 0;
        var query = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid == dropship && !_mobState.IsDead(uid))
                count++;
        }

        return count;
    }

    private bool AllMarinesAboard(EntityUid dropship)
    {
        var aboard = 0;
        var query = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (xform.GridUid != dropship)
                return false;

            aboard++;
        }

        return aboard > 0;
    }

    private void SetState(ContactShuttleComponent comp, ContactShuttleState state, TimeSpan? endsAt)
    {
        comp.State = state;
        comp.StateEndsAt = endsAt;
        SendHud(comp);
    }

    private void SendHud(ContactShuttleComponent comp)
    {
        RaiseNetworkEvent(new ContactShuttleHudEvent(comp.State, comp.StateEndsAt));
    }

    private void Announce(string key, params (string, object)[] args)
    {
        _marineAnnounce.AnnounceARESStaging(default, Loc.GetString(key, args));
    }

    #endregion
}