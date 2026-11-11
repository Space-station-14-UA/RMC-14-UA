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
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact shuttle cycle on top of RMC's DropshipSystem.FlyTo, for the transport dropship (Alamo) only:
/// WaitingForBoarding -> Countdown -> FlyingDown -> Unloading -> Returning -> Refueling -> Ready
/// -> (reinforcement terminal or a manual flight) -> FlyingDown -> ...
/// The state follows where the dropship actually is, so manual flights count as deployments.
/// The other dropship (Normandy) is left to the players and only shown on the HUD.
/// Evacuation: ReserveForExtraction -> TryCallExtraction (when Ready) -> lands and waits -> ExtractionTakeoff.
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
    [Dependency] private readonly ContactRuleSystem _rule = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private enum DropshipWhere : byte
    {
        Ship,
        Planet,
        Flight,
    }

    public override void Initialize()
    {
        SubscribeLocalEvent<ContactReinforcementTerminalComponent, ActivateInWorldEvent>(OnTerminalActivate);
        SubscribeLocalEvent<ContactReinforcementTerminalComponent, MapInitEvent>(OnTerminalMapInit);
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
            if (now < comp.NextCheck)
                continue;

            comp.NextCheck = now + comp.CheckEvery;

            if (TryGetDropship(comp, out var dropship))
                Step(comp, dropship, now);
        }
    }

    private void Step(ContactShuttleComponent comp, EntityUid dropship, TimeSpan now)
    {
        var where = GetWhere(dropship);

        // roundstart: the dropship flies in to the ship, skip until it has docked once
        if (comp.ShipDock == null)
        {
            if (where != DropshipWhere.Ship)
                return;

            comp.ShipDock = FindNearestDestination(dropship, planet: false);
        }

        SyncManualFlight(comp, dropship, where, now);

        switch (comp.State)
        {
            case ContactShuttleState.WaitingForBoarding:
                if (where == DropshipWhere.Ship && HalfAboard(dropship))
                {
                    SetState(comp, ContactShuttleState.Countdown, now + comp.BoardingCountdown);
                    Announce("mriya-contact-shuttle-boarding", ("seconds", (int) comp.BoardingCountdown.TotalSeconds));
                }

                break;

            case ContactShuttleState.Countdown:
                if (!HalfAboard(dropship))
                {
                    SetState(comp, ContactShuttleState.WaitingForBoarding, null);
                    Announce("mriya-contact-shuttle-boarding-cancelled");
                    break;
                }

                if (now >= comp.StateEndsAt)
                    LaunchDown(comp, dropship);

                break;

            case ContactShuttleState.FlyingDown:
                if (where == DropshipWhere.Planet)
                    Landed(comp, dropship, now);

                break;

            case ContactShuttleState.Unloading:
                if (comp.ExtractionFlight || now < comp.StateEndsAt)
                    break;

                EjectStragglers(dropship);
                if (comp.ShipDock is { } dock && Launch(dropship, dock))
                    SetState(comp, ContactShuttleState.Returning, null);

                break;

            case ContactShuttleState.Returning:
                if (where == DropshipWhere.Ship)
                {
                    comp.ExtractionFlight = false;
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
                // waits for the reinforcement terminal, a manual flight or an evacuation call
                break;
        }

        UpdateCounts(comp, dropship);
    }

    /// <summary>
    /// Players can fly the transport by hand. Whatever they do, the cycle follows where it actually is.
    /// </summary>
    private void SyncManualFlight(ContactShuttleComponent comp, EntityUid dropship, DropshipWhere where, TimeSpan now)
    {
        switch (comp.State)
        {
            // launched by hand from the ship: counts as a deployment
            case ContactShuttleState.WaitingForBoarding or ContactShuttleState.Countdown
                or ContactShuttleState.Refueling or ContactShuttleState.Ready when where != DropshipWhere.Ship:
                Announce("mriya-contact-shuttle-manual-launch");
                if (where == DropshipWhere.Planet)
                    Landed(comp, dropship, now);
                else
                    SetState(comp, ContactShuttleState.FlyingDown, null);

                break;

            // took off by hand from the colony
            case ContactShuttleState.Unloading when where != DropshipWhere.Planet:
                SetState(comp, ContactShuttleState.Returning, null);
                break;
        }
    }

    private void Landed(ContactShuttleComponent comp, EntityUid dropship, TimeSpan now)
    {
        if (comp.ExtractionFlight)
        {
            // evacuation: stay until the rule calls ExtractionTakeoff
            SetState(comp, ContactShuttleState.Unloading, null);
            return;
        }

        // reinforcements follow wherever the last drop landed (manual drops can pick another LZ)
        comp.LandingZone = FindNearestDestination(dropship, planet: true) ?? comp.LandingZone;

        SetState(comp, ContactShuttleState.Unloading, now + comp.UnloadTime);
        Announce("mriya-contact-shuttle-landed", ("seconds", (int) comp.UnloadTime.TotalSeconds));
    }

    #region Evacuation

    /// <summary>
    /// Sends the shuttle to the evacuation LZ. Only works once it's refueled (Ready).
    /// </summary>
    public bool TryCallExtraction(EntityUid landingZone)
    {
        if (GetActive() is not { } comp ||
            !TryGetDropship(comp, out var dropship) ||
            comp.State != ContactShuttleState.Ready)
        {
            return false;
        }

        comp.LandingZone = landingZone;
        comp.ExtractionFlight = true;
        LaunchDown(comp, dropship);

        if (comp.State == ContactShuttleState.FlyingDown)
            return true;

        comp.ExtractionFlight = false;
        return false;
    }

    public bool IsExtractionLanded()
    {
        return GetActive() is { ExtractionFlight: true, State: ContactShuttleState.Unloading };
    }

    /// <summary>
    /// Starts the takeoff for the ship. False if it couldn't launch (retry next check).
    /// True if players already took off by hand.
    /// </summary>
    public bool ExtractionTakeoff()
    {
        if (GetActive() is not { } comp || !TryGetDropship(comp, out var dropship))
            return false;

        if (comp.State == ContactShuttleState.Returning)
            return true;

        if (comp.ShipDock is not { } dock || !Launch(dropship, dock))
            return false;

        SetState(comp, ContactShuttleState.Returning, null);
        return true;
    }

    public bool HasLeftPlanet()
    {
        return GetActive() is { } comp && TryGetDropship(comp, out var dropship) && !OnPlanet(dropship);
    }

    /// <summary>
    /// Living marines aboard right now.
    /// </summary>
    public int CountExtracted()
    {
        return GetActive() is { } comp && TryGetDropship(comp, out var dropship) ? CountAboard(dropship) : 0;
    }

    public void ReserveForExtraction()
    {
        if (GetActive() is not { } comp)
            return;

        comp.Extraction = true;
        UpdateTerminals(comp);
    }

    #endregion

    #region Flights

    private void LaunchDown(ContactShuttleComponent comp, EntityUid dropship)
    {
        comp.ShipDock ??= FindNearestDestination(dropship, planet: false);
        comp.LandingZone = FindPrimaryLz() ?? comp.LandingZone ?? FindDestination(planet: true);

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

    private void SetState(ContactShuttleComponent comp, ContactShuttleState state, TimeSpan? endsAt)
    {
        comp.State = state;
        comp.StateEndsAt = endsAt;
        SendHud(comp);
        UpdateTerminals(comp);
    }

    private void OnTerminalMapInit(Entity<ContactReinforcementTerminalComponent> ent, ref MapInitEvent args)
    {
        var state = GetActive() is { } comp ? GetTerminalState(comp) : ContactReinforcementTerminalState.Blocked;
        _appearance.SetData(ent, ContactReinforcementTerminalVisuals.State, state);
    }

    private ContactReinforcementTerminalState GetTerminalState(ContactShuttleComponent comp)
    {
        if (comp.Extraction)
            return ContactReinforcementTerminalState.Evacuation;

        return comp.State == ContactShuttleState.Ready && comp.LastAboard > 0
            ? ContactReinforcementTerminalState.Active
            : ContactReinforcementTerminalState.Blocked;
    }

    private void UpdateTerminals(ContactShuttleComponent comp)
    {
        var state = GetTerminalState(comp);
        var query = EntityQueryEnumerator<ContactReinforcementTerminalComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _appearance.SetData(uid, ContactReinforcementTerminalVisuals.State, state);
        }
    }

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

        if (comp.Extraction)
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-reinforcements-evac"), ent, args.User);
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

    /// <summary>
    /// The transport is the dropship with TransportMarker on its grid (Alamo cockpit).
    /// Any other dropship is the gunship (HUD only).
    /// </summary>
    private bool TryGetDropship(ContactShuttleComponent comp, out EntityUid dropship)
    {
        if (comp.Dropship is { } cached && Exists(cached))
        {
            dropship = cached;
            return true;
        }

        EntityUid? transport = null;
        EntityUid? first = null;
        var query = EntityQueryEnumerator<DropshipComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            first ??= uid;
            if (transport == null && HasMarker(uid, comp.TransportMarker))
                transport = uid;
        }

        if (transport == null && first != null)
        {
            Log.Warning($"Contact shuttle: no dropship with {comp.TransportMarker}, using {ToPrettyString(first.Value)}");
            transport = first;
        }

        if (transport is not { } found)
        {
            dropship = default;
            return false;
        }

        comp.Dropship = found;
        comp.Gunship = null;

        var others = EntityQueryEnumerator<DropshipComponent>();
        while (others.MoveNext(out var uid, out _))
        {
            if (uid == found)
                continue;

            // prefer the marked gunship, otherwise any other dropship
            if (HasMarker(uid, comp.GunshipMarker))
            {
                comp.Gunship = uid;
                break;
            }

            comp.Gunship ??= uid;
        }

        SendHud(comp);
        dropship = found;
        return true;
    }

    private bool HasMarker(EntityUid grid, EntProtoId marker)
    {
        var children = Transform(grid).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (MetaData(child).EntityPrototype?.ID == marker.Id)
                return true;
        }

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
        if (planet && FindPrimaryLz() is { } primary)
            return primary;
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_rmcPlanet.IsOnPlanet(xform.Coordinates) == planet)
                found.Add(uid);
        }

        return found.Count == 0 ? null : _random.Pick(found);
    }

    /// <summary>
    /// he designated primary LZ on the planet, if there is one.
    /// </summary>
    private EntityUid? FindPrimaryLz()
    {
        var query = EntityQueryEnumerator<PrimaryLandingZoneComponent, DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            if (_rmcPlanet.IsOnPlanet(xform.Coordinates))
                return uid;
        }

        return null;
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

    private DropshipWhere GetWhere(EntityUid dropship)
    {
        if (OnPlanet(dropship))
            return DropshipWhere.Planet;

        return OnShip(dropship) ? DropshipWhere.Ship : DropshipWhere.Flight;
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

    private void CountMarines(EntityUid dropship, out int aboard, out int total)
    {
        aboard = 0;
        total = 0;
        var query = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (xform.GridUid == dropship)
                aboard++;

            // marines already on the colony don't need the transport
            if (!_rmcPlanet.IsOnPlanet(xform.Coordinates))
                total++;
        }
    }

    private int CountAboard(EntityUid dropship)
    {
        CountMarines(dropship, out var aboard, out _);
        return aboard;
    }

    /// <summary>
    /// First drop: at least half of the living marines are aboard.
    /// </summary>
    private bool HalfAboard(EntityUid dropship)
    {
        CountMarines(dropship, out var aboard, out var total);
        return aboard > 0 && aboard * 2 >= total;
    }

    /// <summary>
    /// Boarding numbers for the HUD: before the first drop (aboard / half) and when reinforcements are ready.
    /// Only sent when they change.
    /// </summary>
    private void UpdateCounts(ContactShuttleComponent comp, EntityUid dropship)
    {
        var aboard = 0;
        var needed = 0;

        switch (comp.State)
        {
            case ContactShuttleState.WaitingForBoarding or ContactShuttleState.Countdown:
                CountMarines(dropship, out aboard, out var total);
                needed = Math.Max(1, (total + 1) / 2);
                break;

            case ContactShuttleState.Ready:
                CountMarines(dropship, out aboard, out _);
                needed = 1;
                break;
        }

        if (aboard == comp.LastAboard && needed == comp.LastNeeded)
            return;

        comp.LastAboard = aboard;
        comp.LastNeeded = needed;
        SendHud(comp);
        UpdateTerminals(comp);
    }

    private ContactShuttleHudEvent MakeHud(ContactShuttleComponent comp)
    {
        return new ContactShuttleHudEvent(
            comp.State,
            comp.StateEndsAt,
            comp.Dropship is { } transport && Exists(transport) ? Name(transport) : null,
            comp.Gunship is { } gunship && Exists(gunship) ? Name(gunship) : null,
            comp.LastAboard,
            comp.LastNeeded);
    }

    private void SendHud(ContactShuttleComponent comp)
    {
        RaiseNetworkEvent(MakeHud(comp));
    }

    /// <summary>
    /// Snapshot for one player (joining, ghosting, reconnecting).
    /// </summary>
    public void SendHudTo(ICommonSession session)
    {
        if (GetActive() is { } comp)
            RaiseNetworkEvent(MakeHud(comp), session);
    }

    private void Announce(string key, params (string, object)[] args)
    {
        _rule.Announce(Loc.GetString(key, args));
    }

    #endregion
}