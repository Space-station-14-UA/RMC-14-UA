using System.Numerics;
using Content.Shared._RMC14.Hunter.Components;
using Content.Shared._RMC14.Hunter.Events;
using Content.Shared._Sich.Hunter.Caster;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Timing;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Maps;
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
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Перехоплюємо UseInHandEvent ДО ActivatableUISystem, щоб:
        // 1. У режимі повернення на корабель — заблокувати відкриття UI і запустити повернення
        // 2. В обох випадках — заборонити UseDelay при відкритті UI
        SubscribeLocalEvent<HunterTeleporterComponent, UseInHandEvent>(OnUseInHand,
            before: [typeof(ActivatableUISystem)]);

        SubscribeLocalEvent<HunterTeleporterComponent, BoundUIOpenedEvent>(OnBUIOpened);
        SubscribeLocalEvent<HunterTeleporterComponent, BoundUIClosedEvent>(OnBUIClosed);
        SubscribeLocalEvent<HunterTeleporterComponent, HunterTeleportRequestMsg>(OnTeleportRequest);
        SubscribeLocalEvent<HunterTeleporterComponent, HunterTeleportDoAfterEvent>(OnTeleportDoAfter);
        SubscribeLocalEvent<HunterTeleporterComponent, HunterReturnToShipDoAfterEvent>(OnReturnToShipDoAfter);
        SubscribeLocalEvent<HunterTeleporterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnGetAlternativeVerbs(Entity<HunterTeleporterComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        var comp = ent.Comp;
        var verb = new AlternativeVerb
        {
            Text = comp.TeleportToShipMode 
                ? Loc.GetString("hunter-teleport-verb-mode-planet") 
                : Loc.GetString("hunter-teleport-verb-mode-ship"),
            Act = () =>
            {
                ToggleMode(ent, user);
            }
        };
        args.Verbs.Add(verb);
    }

    private void ToggleMode(Entity<HunterTeleporterComponent> ent, EntityUid user)
    {
        ent.Comp.TeleportToShipMode = !ent.Comp.TeleportToShipMode;
        Dirty(ent);

        var modeMessage = ent.Comp.TeleportToShipMode
            ? Loc.GetString("hunter-teleport-mode-ship-selected")
            : Loc.GetString("hunter-teleport-mode-planet-selected");

        _popup.PopupEntity(modeMessage, user, user);
    }

    private void OnUseInHand(EntityUid uid, HunterTeleporterComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Кулдаун НЕ застосовується при відкритті UI — лише після успішної телепортації
        args.ApplyDelay = false;

        // Перевіряємо поточний режим телепортера
        if (component.TeleportToShipMode)
        {
            args.Handled = true;
            StartReturnToShip(uid, component, args.User);
        }
        // Якщо режим планети — нічого не робимо, ActivatableUISystem відкриє UI тактичної карти
    }

    private void StartReturnToShip(EntityUid uid, HunterTeleporterComponent component, EntityUid user)
    {
        if (_net.IsClient)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, component.TeleportDelay,
            new HunterReturnToShipDoAfterEvent(), uid, target: uid, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("hunter-teleport-returning-to-ship"), user, user);
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

        if (!IsValidTeleportTarget(targetCoords, map.Owner, grid))
        {
            _popup.PopupEntity(Loc.GetString("hunter-teleport-blocked-invalid-target"), user, user, PopupType.SmallCaution);
            return;
        }

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

        var doAfterArgs = new DoAfterArgs(EntityManager, user, component.TeleportDelay, new HunterTeleportDoAfterEvent(args.Position), uid, target: uid, used: uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        _popup.PopupEntity(Loc.GetString("hunter-teleport-charging"), user, user);
    }

    private bool IsValidTeleportTarget(EntityCoordinates targetCoords, EntityUid mapGridUid, MapGridComponent grid)
    {
        if (!_turf.TryGetTileRef(targetCoords, out var tileRef) || tileRef.Value.Tile.IsEmpty || _turf.IsSpace(tileRef.Value))
            return false;

        var center = tileCoordsToVector2(tileRef.Value.GridIndices, grid.TileSize);
        var halfSize = grid.TileSize / 2.5f;
        var bounds = new Box2(center.X - halfSize, center.Y - halfSize, center.X + halfSize, center.Y + halfSize);
        foreach (var ent in _lookup.GetEntitiesIntersecting(mapGridUid, bounds))
        {
            if (!TryComp<MetaDataComponent>(ent, out var meta))
                continue;

            var prototypeId = meta.EntityPrototype?.ID ?? string.Empty;
            if (prototypeId.Contains("wall", StringComparison.OrdinalIgnoreCase) ||
                prototypeId.Contains("window", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private Vector2 tileCoordsToVector2(Vector2i indices, float tileSize)
    {
        return new Vector2(indices.X * tileSize + tileSize / 2f, indices.Y * tileSize + tileSize / 2f);
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

        if (!IsValidTeleportTarget(targetCoords, map.Owner, grid))
        {
            _popup.PopupEntity(Loc.GetString("hunter-teleport-blocked-invalid-target"), user, user, PopupType.SmallCaution);
            return;
        }

        var targetMapCoords = _transform.ToMapCoordinates(targetCoords);

        _transform.SetMapCoordinates(user, targetMapCoords);
        _popup.PopupEntity(Loc.GetString("hunter-teleport-success"), user, user);

        // Застосовуємо кулдаун тільки після успішної телепортації
        _useDelay.TryResetDelay(uid);
    }

    private void OnReturnToShipDoAfter(EntityUid uid, HunterTeleporterComponent component, HunterReturnToShipDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        EntityCoordinates? returnCoords = null;

        // 1. Спочатку шукаємо точну точку прибуття (HunterReturnPointComponent)
        var returnQuery = EntityQueryEnumerator<HunterReturnPointComponent, TransformComponent>();
        while (returnQuery.MoveNext(out _, out _, out var xform))
        {
            returnCoords = xform.Coordinates;
            break;
        }

        // 2. Якщо окремої точки прибуття немає — шукаємо грід Левіафана (HunterShipComponent)
        if (returnCoords == null)
        {
            var shipGrid = EntityUid.Invalid;
            var query = EntityQueryEnumerator<HunterShipComponent, MapGridComponent>();
            while (query.MoveNext(out var gridUid, out _, out _))
            {
                shipGrid = gridUid;
                break;
            }

            if (shipGrid.IsValid())
            {
                returnCoords = new EntityCoordinates(shipGrid, new Vector2(0f, 2f));
            }
        }

        if (returnCoords == null)
        {
            _popup.PopupEntity(Loc.GetString("hunter-teleport-failed"), user, user, PopupType.SmallCaution);
            return;
        }

        var targetMapCoords = _transform.ToMapCoordinates(returnCoords.Value);
        _transform.SetMapCoordinates(user, targetMapCoords);
        _popup.PopupEntity(Loc.GetString("hunter-teleport-success"), user, user);

        // Застосовуємо кулдаун тільки після успішного повернення
        _useDelay.TryResetDelay(uid);
    }
}
