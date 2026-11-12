using Content.Server.EUI;
using Content.Shared._Mriya.Terminal;
using Content.Shared.Eui;
using Robust.Shared.IoC;

namespace Content.Server._Mriya.Terminal;

public sealed class MriyaTerminalAdminEui : BaseEui
{
    private readonly MriyaTerminalSystem _terminal;

    public MriyaTerminalAdminEui()
    {
        _terminal = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<MriyaTerminalSystem>();
    }

    public override void Opened()
    {
        _terminal.RegisterAdminEui(this);
        StateDirty();
    }

    public override void Closed()
    {
        _terminal.UnregisterAdminEui(this);
    }

    public override MriyaTerminalAdminEuiState GetNewState()
    {
        return new MriyaTerminalAdminEuiState(_terminal.GetHistory());
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is MriyaTerminalAdminEuiMsg.Send send)
            _terminal.SendCorporateMessage(send.Message);
    }
}
