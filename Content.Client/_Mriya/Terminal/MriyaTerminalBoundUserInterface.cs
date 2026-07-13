using Content.Shared._Mriya.Terminal;
using JetBrains.Annotations;
using Robust.Client.GameObjects;

namespace Content.Client._Mriya.Terminal;

[UsedImplicitly]
public sealed class MriyaTerminalBoundUserInterface : BoundUserInterface
{
    private MriyaTerminalWindow? _window;

    public MriyaTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new MriyaTerminalWindow();
        _window.OnClose += Close;
        _window.OnMessageEntered += msg =>
        {
            SendMessage(new MriyaTerminalSendMessage(msg));
        };
        
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MriyaTerminalState terminalState)
            return;

        _window?.SetState(terminalState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }
}
