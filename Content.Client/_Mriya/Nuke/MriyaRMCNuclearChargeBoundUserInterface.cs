using Content.Shared._Mriya.Nuke;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Nuke;

[UsedImplicitly]
public sealed class MriyaRMCNuclearChargeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private MriyaRMCNuclearChargeWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MriyaRMCNuclearChargeWindow>();
        _window.OnStart += () => SendPredictedMessage(new MriyaRMCNuclearChargeStartBuiMsg());
        _window.OnAbort += () => SendPredictedMessage(new MriyaRMCNuclearChargeAbortBuiMsg());

        if (State is MriyaRMCNuclearChargeBuiState state)
            _window.SetState(state);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is MriyaRMCNuclearChargeBuiState chargeState)
            _window?.SetState(chargeState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
