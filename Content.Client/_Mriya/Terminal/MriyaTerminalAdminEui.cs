using Content.Client.Eui;
using Content.Shared._Mriya.Terminal;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client._Mriya.Terminal;

[UsedImplicitly]
public sealed class MriyaTerminalAdminEui : BaseEui
{
    private readonly MriyaTerminalWindow _window;

    public MriyaTerminalAdminEui()
    {
        _window = new MriyaTerminalWindow
        {
            Title = "Термінал: Ве-Я",
        };
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnMessageEntered += message => SendMessage(new MriyaTerminalAdminEuiMsg.Send(message));
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not MriyaTerminalAdminEuiState cast)
            return;

        _window.SetState(new MriyaTerminalState(
            true,
            cast.Messages,
            "ВЕСТОН-ЯМАДА",
            "КОРПОРАТИВНИЙ КАНАЛ"));
    }
}
