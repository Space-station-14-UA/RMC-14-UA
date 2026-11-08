using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Rules.Contact;

public sealed class ContactStoreBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private ContactStoreWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ContactStoreWindow>();
        _window.Buy += (entry, amount) => SendMessage(new ContactStoreBuyMessage(entry, amount));
        _window.ResupplyKit += kit => SendMessage(new ContactStoreResupplyKitMessage(kit));
        _window.UpdatePlayer(IoCManager.Resolve<IPlayerManager>().LocalEntity);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is not ContactStoreUpdateMessage update || _window == null)
            return;

        _window.UpdateServer(update);
        _window.UpdatePlayer(IoCManager.Resolve<IPlayerManager>().LocalEntity);
    }
}