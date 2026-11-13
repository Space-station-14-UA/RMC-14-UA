using Content.Shared._Mriya.Monitoring;
using Content.Shared._RMC14.UserInterface;
using Robust.Client.Timing;

namespace Content.Client._Mriya.Monitoring;

public sealed class MRMonitoringSystem : EntitySystem
{
    [Dependency] private readonly RMCUserInterfaceSystem _rmcUI = default!;
    [Dependency] private readonly IClientGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MRMonitoringComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnState(Entity<MRMonitoringComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_timing.CurTick != _timing.LastRealTick)
            return;

        _rmcUI.RefreshUIs<MRMonitoringBui>(ent.Owner);
    }
}
