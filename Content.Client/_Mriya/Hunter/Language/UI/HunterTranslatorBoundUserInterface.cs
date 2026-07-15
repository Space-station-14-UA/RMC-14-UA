using Content.Shared._Sich.Language;
using Robust.Client.GameObjects;

namespace Content.Client._Sich.Language.UI;

public sealed class HunterTranslatorBoundUserInterface : BoundUserInterface
{
    private HunterTranslatorWindow? _window;

    public HunterTranslatorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new HunterTranslatorWindow();
        _window.OnClose += Close;
        _window.OnSendMessage += message => SendMessage(new HunterTranslatorSendMessage(message: message));
        _window.OnTargetChanged += target => SendMessage(new HunterTranslatorSendMessage(translationTarget: target));

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HunterTranslatorBoundUserInterfaceState s)
            return;

        _window?.UpdateState(s.TranslationTarget);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Dispose();
        }
    }
}
