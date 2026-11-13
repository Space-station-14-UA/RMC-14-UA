using Content.Server.PowerCell;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Marines;
using Content.Shared._Mriya.Monitoring;
using Content.Shared.IdentityManagement;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.Monitoring;

public sealed class MRMonitoringSystem : EntitySystem
{
    private static readonly ProtoId<NpcFactionPrototype> UnmcFaction = "UNMC";
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private readonly AreaSystem _area = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        SubscribeLocalEvent<MRMonitoringComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<MRMonitoringComponent, BoundUIClosedEvent>(OnClosed);

        Subs.BuiEvents<MRMonitoringComponent>(MRMonitoringUI.Key, subs =>
        {
            subs.Event<MRMonitoringSelectMarineMsg>(OnSelectMarine);
        });
    }

    private void OnOpened(Entity<MRMonitoringComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (_net.IsClient)
            return;

        ent.Comp.User = GetNetEntity(args.Actor);
        _appearance.SetData(ent.Owner, MRMonitoringVisuals.Enabled, true);
        UpdateBattery(ent.Owner, ent.Comp);
        UpdateRoster(ent);
        UpdateTracking(ent);
        Dirty(ent);
    }

    private void OnClosed(Entity<MRMonitoringComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.User == GetNetEntity(args.Actor))
        {
            ent.Comp.User = null;
            Dirty(ent);
        }

        if (!_ui.IsUiOpen(ent.Owner, MRMonitoringUI.Key))
            _appearance.SetData(ent.Owner, MRMonitoringVisuals.Enabled, false);
    }

    private void OnSelectMarine(Entity<MRMonitoringComponent> ent, ref MRMonitoringSelectMarineMsg args)
    {
        if (_net.IsClient)
            return;

        if (ent.Comp.User != GetNetEntity(args.Actor))
            return;

        if (!TryGetEntity(args.Marine, out var target) ||
            !HasComp<MarineComponent>(target) ||
            !TryComp<NpcFactionMemberComponent>(target, out var factions) ||
            !_factions.IsMember((target.Value, factions), UnmcFaction))
        {
            return;
        }

        ent.Comp.Selected = args.Marine;
        UpdateTracking(ent);
        Dirty(ent);
    }

    private void UpdateBattery(EntityUid uid, MRMonitoringComponent component)
    {
        if (_powerCell.TryGetBatteryFromSlot(uid, out var battery) && battery.MaxCharge > 0f)
        {
            component.BatteryPercent = Math.Clamp(battery.CurrentCharge / battery.MaxCharge * 100f, 0f, 100f);
            return;
        }

        component.BatteryPercent = 0f;
    }

    private void UpdateRoster(Entity<MRMonitoringComponent> ent)
    {
        var marines = ent.Comp.Marines;
        marines.Clear();

        var query = EntityQueryEnumerator<MarineComponent, NpcFactionMemberComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var factions, out var mobState))
        {
            if (!_factions.IsMember((uid, factions), UnmcFaction))
                continue;

            marines.Add(new MRMonitoringMarine(
                GetNetEntity(uid),
                Identity.Name(uid, EntityManager),
                mobState.CurrentState,
                _area.GetAreaName(uid)));
        }

        marines.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateTracking(Entity<MRMonitoringComponent> ent)
    {
        var component = ent.Comp;
        component.Distance = -1f;
        component.DirectionAngle = 0f;

        if (component.User is not { } userNet ||
            !TryGetEntity(userNet, out var user) ||
            component.Selected is not { } selectedNet ||
            !TryGetEntity(selectedNet, out var target))
        {
            return;
        }

        var userCoordinates = _transform.GetMapCoordinates(user.Value);
        var targetCoordinates = _transform.GetMapCoordinates(target.Value);
        if (userCoordinates.MapId != targetCoordinates.MapId)
            return;

        var delta = targetCoordinates.Position - userCoordinates.Position;
        component.Distance = delta.Length();
        component.DirectionAngle = MathF.Atan2(delta.Y, delta.X);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        if (time < _nextUpdate)
            return;

        _nextUpdate = time + UpdateEvery;

        var query = EntityQueryEnumerator<MRMonitoringComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (!_ui.IsUiOpen(uid, MRMonitoringUI.Key))
                continue;

            UpdateBattery(uid, component);
            UpdateRoster((uid, component));
            UpdateTracking((uid, component));
            Dirty(uid, component);
        }
    }
}
