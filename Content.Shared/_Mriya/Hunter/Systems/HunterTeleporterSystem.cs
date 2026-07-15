using System.Numerics;
using Content.Shared._RMC14.Hunter.Components;
using Content.Shared._RMC14.Hunter.Events;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Network;

namespace Content.Shared._RMC14.Hunter.Systems;

public sealed class HunterTeleporterSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTacticalMapSystem _tacticalMap = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterTeleporterComponent, BoundUIOpenedEvent>(OnBUIOpened);
        SubscribeLocalEvent<HunterTeleporterComponent, BoundUIClosedEvent>(OnBUIClosed);
        SubscribeLocalEvent<HunterTeleporterComponent, HunterTeleportRequestMsg>(OnTeleportRequest);
        SubscribeLocalEvent<HunterTeleporterComponent, HunterTeleportDoAfterEvent>(OnTeleportDoAfter);
    }

    private void OnBUIOpened(EntityUid uid, HunterTeleporterComponent component, BoundUIOpenedEvent args)
    {
        if (_net.IsClient)
            return;

        var player = args.Actor;
        EnsureComp<HunterTeleportingComponent>(player);
    }

    private void OnBUIClosed(EntityUid uid, HunterTeleporterComponent component, BoundUIClosedEvent args)
    {
        if (_net.IsClient)
            return;

        var player = args.Actor;
        RemComp<HunterTeleportingComponent>(player);
    }

    private void OnTeleportRequest(EntityUid uid, HunterTeleporterComponent component, HunterTeleportRequestMsg args)
    {
        var user = args.Actor;

        _ui.CloseUi(uid, HunterTeleporterUiKey.Key, user);

        if (_net.IsServer)
            RemComp<HunterTeleportingComponent>(user);

        if (!_tacticalMap.TryGetTacticalMap(out var map) ||
            !TryComp<MapGridComponent>(map.Owner, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("hunter-teleport-failed"), user, user, PopupType.SmallCaution);
            return;
        }

        var tileCoords = new Vector2(args.Position.X, args.Position.Y);
        var targetCoords = new EntityCoordinates(map.Owner, tileCoords * grid.TileSize);

        if (_area.TryGetArea(targetCoords, out var area, out var areaProto))
        {
            if (area.Value.Comp.LandingZone)
            {
                _popup.PopupEntity(Loc.GetString("hunter-teleport-blocked-landing-zone"), user, user, PopupType.SmallCaution);
                return;
            }

            var isCave = false;
            if (areaProto.ID.Contains("Cave", StringComparison.OrdinalIgnoreCase) ||
                areaProto.ID.Contains("Cavern", StringComparison.OrdinalIgnoreCase) ||
                areaProto.Name.Contains("Cave", StringComparison.OrdinalIgnoreCase) ||
                areaProto.Name.Contains("Cavern", StringComparison.OrdinalIgnoreCase) ||
                areaProto.Name.Contains("Печер", StringComparison.OrdinalIgnoreCase))
            {
                isCave = true;
            }

            if (isCave)
            {
                _popup.PopupEntity(Loc.GetString("hunter-teleport-blocked-cave"), user, user, PopupType.SmallCaution);
                return;
            }
        }

        var targetMapCoords = _transform.ToMapCoordinates(targetCoords);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, component.TeleportDelay, new HunterTeleportDoAfterEvent(args.Position), uid, target: uid, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("hunter-teleport-charging"), user, user);
    }

    private void OnTeleportDoAfter(EntityUid uid, HunterTeleporterComponent component, HunterTeleportDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        if (!_tacticalMap.TryGetTacticalMap(out var map) ||
            !TryComp<MapGridComponent>(map.Owner, out var grid))
        {
            return;
        }

        var tileCoords = new Vector2(args.TargetPosition.X, args.TargetPosition.Y);
        var targetCoords = new EntityCoordinates(map.Owner, tileCoords * grid.TileSize);
        var targetMapCoords = _transform.ToMapCoordinates(targetCoords);

        _transform.SetMapCoordinates(user, targetMapCoords);
        _popup.PopupEntity(Loc.GetString("hunter-teleport-success"), user, user);
    }
}
