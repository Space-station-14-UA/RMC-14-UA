using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines;
using Robust.Shared.Map;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact evacuation, see <see cref="ContactExtractionComponent"/>.
/// </summary>
public sealed partial class ContactRuleSystem
{
    [Dependency] private readonly ContactShuttleSystem _shuttle = default!;

    private readonly List<(EntityUid Lz, MapCoordinates Pos)> _zones = new();
    private readonly List<MapCoordinates> _colony = new();

    /// <summary>
    /// Called from StartWave after the wave counter went up.
    /// </summary>
    private void TryOfferExtraction(EntityUid uid, ContactRuleComponent rule, TimeSpan now)
    {
        if (!TryComp(uid, out ContactExtractionComponent? ex) || ex.State != ContactExtractionState.None)
            return;

        var forced = rule.FinalWave > 0 && rule.Wave >= rule.FinalWave;
        if (!forced && (ex.EveryWaves <= 0 || rule.Wave % ex.EveryWaves != 0))
            return;

        ex.Forced = forced;
        ex.State = ContactExtractionState.Available;
        ex.WindowEndsAt = now + ex.BreakTime;
        ex.StateEndsAt = ex.WindowEndsAt;
        ex.NextCheck = now;

        // break before the next wave; anchors offline, field frozen
        rule.NextWaveAt = ex.WindowEndsAt;
        rule.FieldUnavailable = true;
        rule.FieldEndsAt = null;

        _marineAnnounce.AnnounceARESStaging(default, Loc.GetString(
            forced ? "mriya-contact-extraction-final" : "mriya-contact-extraction-available",
            ("seconds", (int) ex.BreakTime.TotalSeconds)));

        SendHud(rule);
        SendExtractionHud(ex);
    }

    /// <summary>
    /// Evacuation is running (hold onwards): waves don't count, anchors stay off.
    /// </summary>
    private bool IsEvacuating(EntityUid uid, out ContactExtractionState state)
    {
        state = TryComp(uid, out ContactExtractionComponent? ex) ? ex.State : ContactExtractionState.None;
        return state is ContactExtractionState.Holding
            or ContactExtractionState.ShuttleInbound
            or ContactExtractionState.Boarding
            or ContactExtractionState.Departing;
    }

    private TimeSpan GetWaveInterval(EntityUid uid, ContactRuleComponent rule)
    {
        if (IsEvacuating(uid, out _) && TryComp(uid, out ContactExtractionComponent? ex))
            return ex.HoldWaveInterval;

        return rule.WaveInterval;
    }

    private void UpdateExtraction(EntityUid uid, ContactRuleComponent rule, TimeSpan now)
    {
        if (!TryComp(uid, out ContactExtractionComponent? ex) ||
            ex.State is ContactExtractionState.None or ContactExtractionState.Done ||
            now < ex.NextCheck)
        {
            return;
        }

        ex.NextCheck = now + ex.CheckEvery;

        switch (ex.State)
        {
            case ContactExtractionState.Available:
                if (now >= ex.WindowEndsAt)
                {
                    if (ex.Forced)
                        ForceStart(rule, ex, now);
                    else
                        CancelOffer(rule, ex, now);

                    return;
                }

                if (FindQuorumZone(ex, out var lz))
                {
                    ex.Zone = lz;
                    ex.State = ContactExtractionState.Countdown;
                    ex.StateEndsAt = now + ex.StartCountdown;
                }

                break;

            case ContactExtractionState.Countdown:
                if (ex.Zone is not { } zone || !HasQuorum(ex, zone))
                {
                    // they scattered
                    if (now >= ex.WindowEndsAt)
                    {
                        CancelOffer(rule, ex, now);
                        return;
                    }

                    ex.State = ContactExtractionState.Available;
                    ex.StateEndsAt = ex.WindowEndsAt;
                    ex.Zone = null;
                    break;
                }

                if (now >= ex.StateEndsAt)
                    StartHold(rule, ex, now);

                break;

            case ContactExtractionState.Holding:
                // after the hold, the shuttle comes as soon as it's refueled at the ship
                if (now >= ex.StateEndsAt && ex.Zone is { } holdZone && _shuttle.TryCallExtraction(holdZone))
                {
                    ex.State = ContactExtractionState.ShuttleInbound;
                    ex.StateEndsAt = null;
                    _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("mriya-contact-extraction-inbound"));
                }

                break;

            case ContactExtractionState.ShuttleInbound:
                if (_shuttle.IsExtractionLanded())
                {
                    ex.State = ContactExtractionState.Boarding;
                    ex.StateEndsAt = now + ex.BoardTime;
                    _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("mriya-contact-extraction-boarding",
                        ("seconds", (int) ex.BoardTime.TotalSeconds)));
                }

                break;

            case ContactExtractionState.Boarding:
                if (now >= ex.StateEndsAt && _shuttle.ExtractionTakeoff())
                {
                    ex.State = ContactExtractionState.Departing;
                    ex.StateEndsAt = null;
                    _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("mriya-contact-extraction-departing"));
                }

                break;
                
            case ContactExtractionState.Departing:
                // wait until it's actually off the planet, then give it DepartCheckDelay
                if (ex.StateEndsAt == null)
                {
                    if (_shuttle.HasLeftPlanet())
                        ex.StateEndsAt = now + ex.DepartCheckDelay;

                    break;
                }

                if (now >= ex.StateEndsAt)
                {
                    ex.State = ContactExtractionState.Done;
                    SendExtractionHud(ex);

                    var aboard = _shuttle.CountExtracted();
                    EndContact(rule, aboard > 0 ? ContactResult.Evacuated : ContactResult.ExtractionFailed);
                    return;
                }

                break;
        }

        SendExtractionHud(ex);
    }

    /// <summary>
    /// Nobody went for it: back to normal, field reset to its starting value.
    /// </summary>
    private void CancelOffer(ContactRuleComponent rule, ContactExtractionComponent ex, TimeSpan now)
    {
        ex.State = ContactExtractionState.None;
        ex.Zone = null;

        rule.FieldUnavailable = false;
        rule.FieldEndsAt = now + rule.FieldStart;

        _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("mriya-contact-extraction-skipped",
            ("seconds", (int) rule.FieldStart.TotalSeconds)));

        SendHud(rule);
        SendExtractionHud(ex);
    }

    private void StartHold(ContactRuleComponent rule, ContactExtractionComponent ex, TimeSpan now)
    {
        ex.State = ContactExtractionState.Holding;
        ex.StateEndsAt = now + ex.HoldTime;

        rule.NextWaveAt = now + ex.HoldWaveInterval;
        _shuttle.ReserveForExtraction();

        _marineAnnounce.AnnounceARESStaging(default, Loc.GetString("mriya-contact-extraction-started",
            ("seconds", (int) ex.HoldTime.TotalSeconds)));

        SendHud(rule);
    }

    /// <summary>
    /// Final wave, window over: evacuate from the LZ with the most marines nearby (random if nobody's near one).
    /// </summary>
    private void ForceStart(ContactRuleComponent rule, ContactExtractionComponent ex, TimeSpan now)
    {
        FindQuorumZone(ex, out var lz);
        if (!Exists(lz))
        {
            CollectZones();
            if (_zones.Count == 0)
            {
                Log.Error("Contact: no LZ on the planet for the final evacuation");
                return;
            }

            lz = _random.Pick(_zones).Lz;
        }

        ex.Zone = lz;
        StartHold(rule, ex, now);
        SendExtractionHud(ex);
    }

    #region Quorum

    private void CollectZones()
    {
        _zones.Clear();
        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var lz, out _, out var xform))
        {
            if (_rmcPlanet.IsOnPlanet(xform.Coordinates))
                _zones.Add((lz, _transform.GetMapCoordinates(lz, xform)));
        }
    }

    /// <summary>
    /// Living marines on the colony; returns how many are needed for "more than half".
    /// </summary>
    private int CollectColony()
    {
        _colony.Clear();
        var query = EntityQueryEnumerator<MarineComponent, TransformComponent>();
        while (query.MoveNext(out var marine, out _, out var xform))
        {
            if (_mobState.IsDead(marine) || !_rmcPlanet.IsOnPlanet(xform.Coordinates))
                continue;

            _colony.Add(_transform.GetMapCoordinates(marine, xform));
        }

        return _colony.Count == 0 ? 0 : _colony.Count / 2 + 1;
    }

    private int CountNear(MapCoordinates zone, float radius)
    {
        var count = 0;
        foreach (var pos in _colony)
        {
            if (pos.MapId == zone.MapId && (pos.Position - zone.Position).Length() <= radius)
                count++;
        }

        return count;
    }

    private bool FindQuorumZone(ContactExtractionComponent ex, out EntityUid lz)
    {
        lz = default;
        CollectZones();
        ex.Needed = CollectColony();
        ex.InZone = 0;

        foreach (var (zone, pos) in _zones)
        {
            var near = CountNear(pos, ex.ZoneRadius);
            if (near <= ex.InZone)
                continue;

            ex.InZone = near;
            lz = zone;
        }

        return ex.Needed > 0 && ex.InZone >= ex.Needed;
    }

    private bool HasQuorum(ContactExtractionComponent ex, EntityUid lz)
    {
        if (!Exists(lz))
            return false;

        ex.Needed = CollectColony();
        ex.InZone = CountNear(_transform.GetMapCoordinates(lz), ex.ZoneRadius);
        return ex.Needed > 0 && ex.InZone >= ex.Needed;
    }

    #endregion

    #region HUD

    private ContactExtractionHudEvent MakeExtractionHudEvent(ContactExtractionComponent ex)
    {
        var zones = new List<NetCoordinates>();
        if (ex.State == ContactExtractionState.Available)
        {
            CollectZones();
            foreach (var (lz, _) in _zones)
            {
                zones.Add(GetNetCoordinates(Transform(lz).Coordinates));
            }
        }
        else if (ex.Zone is { } zone && Exists(zone))
        {
            zones.Add(GetNetCoordinates(Transform(zone).Coordinates));
        }

        var endsAt = ex.State == ContactExtractionState.Available ? ex.WindowEndsAt : ex.StateEndsAt;
        return new ContactExtractionHudEvent(ex.State, zones, ex.ZoneRadius, endsAt, ex.InZone, ex.Needed);
    }

    /// <summary>
    /// Broadcast only when something the HUD shows actually changed.
    /// </summary>
    private void SendExtractionHud(ContactExtractionComponent ex)
    {
        var endsAt = ex.State == ContactExtractionState.Available ? ex.WindowEndsAt : ex.StateEndsAt;
        var key = (ex.State, ex.InZone, ex.Needed, endsAt);
        if (key == ex.LastSent)
            return;

        ex.LastSent = key;
        RaiseNetworkEvent(MakeExtractionHudEvent(ex));
    }

    #endregion
}