using Content.Server._RMC14.Marines;
using Content.Shared._RMC14.Sensor;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Timing;
using Content.Shared._RMC14.Areas;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.DoAfter;
using Robust.Shared.Player;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact terminals ("anchors"): auto names, activation, cooldown, visuals and ARES announcements.
/// The field/wave effect itself is applied by <see cref="ContactRuleSystem.TryActivateTerminal"/>.
/// </summary>
public sealed class ContactTerminalSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly ContactRuleSystem _contact = default!;
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ContactEconomySystem _economy = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    
    private static readonly LocId[] Phonetic =
    {
        "mriya-contact-phonetic-alpha", "mriya-contact-phonetic-bravo", "mriya-contact-phonetic-charlie",
        "mriya-contact-phonetic-delta", "mriya-contact-phonetic-echo", "mriya-contact-phonetic-foxtrot",
        "mriya-contact-phonetic-golf", "mriya-contact-phonetic-hotel", "mriya-contact-phonetic-india",
        "mriya-contact-phonetic-juliett", "mriya-contact-phonetic-kilo", "mriya-contact-phonetic-lima",
        "mriya-contact-phonetic-mike", "mriya-contact-phonetic-november", "mriya-contact-phonetic-oscar",
        "mriya-contact-phonetic-papa", "mriya-contact-phonetic-quebec", "mriya-contact-phonetic-romeo",
        "mriya-contact-phonetic-sierra", "mriya-contact-phonetic-tango", "mriya-contact-phonetic-uniform",
        "mriya-contact-phonetic-victor", "mriya-contact-phonetic-whiskey", "mriya-contact-phonetic-xray",
        "mriya-contact-phonetic-yankee", "mriya-contact-phonetic-zulu",
    };

    // area prototype id ("" = no area) -> next letter index
    private readonly Dictionary<string, int> _areaCounters = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<ContactTerminalComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ContactTerminalComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<ContactTerminalComponent, ContactTerminalDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _areaCounters.Clear();
    }

    private void OnMapInit(Entity<ContactTerminalComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.TerminalName = ent.Comp.CustomName is { } custom
            ? (Loc.TryGetString(custom, out var localized) ? localized : custom.Id)
            : NextName(ent);

        _metaData.SetEntityName(ent, Loc.GetString("mriya-contact-terminal-name", ("name", ent.Comp.TerminalName)));
        UpdateAppearance(ent);
    }

    private string NextName(EntityUid uid)
    {
        string? areaName = null;
        var key = string.Empty;
        if (_area.TryGetArea(uid, out _, out var areaProto))
        {
            areaName = areaProto.Name;
            key = areaProto.ID;
        }

        var i = _areaCounters.GetValueOrDefault(key);
        _areaCounters[key] = i + 1;

        var letter = Loc.GetString(Phonetic[i % Phonetic.Length]);
        if (i >= Phonetic.Length)
            letter = Loc.GetString("mriya-contact-phonetic-numbered", ("letter", letter), ("number", i / Phonetic.Length + 1));

        return areaName == null
            ? letter
            : Loc.GetString("mriya-contact-terminal-area-name", ("area", areaName), ("letter", letter));
    }

    private void OnActivate(Entity<ContactTerminalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Activated)
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-used"), ent, args.User);
            return;
        }

        if (!_contact.CanActivateTerminal())
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-offline"), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.ActivateDelay, new ContactTerminalDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-start"), ent, args.User);
    }

    private void OnDoAfter(Entity<ContactTerminalComponent> ent, ref ContactTerminalDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // someone else may have finished first
        if (ent.Comp.Activated)
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-used"), ent, args.User);
            return;
        }

        if (!_contact.TryActivateTerminal(out var bonus))
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-offline"), ent, args.User);
            return;
        }

        ent.Comp.Activated = true;
        SendAnchors();
        if (ent.Comp.Cooldown is { } cooldown)
            ent.Comp.ReactivateAt = _timing.CurTime + cooldown;

        UpdateAppearance(ent);

        var seconds = (int) bonus.TotalSeconds;
        _popup.PopupEntity(Loc.GetString("mriya-contact-terminal-activated", ("seconds", seconds)), ent, args.User);
        _contact.Announce(Loc.GetString("mriya-contact-terminal-announce-active",
            ("name", ent.Comp.TerminalName),
            ("seconds", seconds)));
        _economy.RewardTerminal(args.User);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<ContactTerminalComponent>();
        while (query.MoveNext(out var uid, out var terminal))
        {
            if (terminal.ReactivateAt is not { } at || time < at)
                continue;

            terminal.Activated = false;
            SendAnchors();
            terminal.ReactivateAt = null;
            UpdateAppearance((uid, terminal));
            _contact.Announce(Loc.GetString("mriya-contact-terminal-announce-available",
                ("name", terminal.TerminalName)));
        }
    }

    // Reuses the sensor tower visual enum so the RMC sensor tower sprite works as-is.
    private void UpdateAppearance(Entity<ContactTerminalComponent> ent)
    {
        _appearance.SetData(ent, SensorTowerLayers.Layer, ent.Comp.Activated ? SensorTowerState.On : SensorTowerState.Off);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        SendAnchors(ev.Player);
    }

    /// <summary>
    /// HUD arrow data: every anchor that isn't activated right now. Null session = everyone.
    /// </summary>
    public void SendAnchors(ICommonSession? session = null)
    {
        var anchors = new List<ContactAnchorHudInfo>();
        var query = EntityQueryEnumerator<ContactTerminalComponent, TransformComponent>();
        while (query.MoveNext(out _, out var terminal, out var xform))
        {
            if (terminal.Activated || xform.MapUid is not { } map)
                continue;

            anchors.Add(new ContactAnchorHudInfo(GetNetEntity(map), _transform.GetWorldPosition(xform)));
        }

        var ev = new ContactAnchorsHudEvent(anchors);
        if (session != null)
            RaiseNetworkEvent(ev, session);
        else
            RaiseNetworkEvent(ev);
    }
}