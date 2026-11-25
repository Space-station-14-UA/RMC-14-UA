using Content.Shared._Mriya.Rules.Contact;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Slot machine / blackjack table UI (ContactCasinoUiKey).
/// </summary>
[UsedImplicitly]
public sealed class ContactCasinoBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ContactCasinoWindow? _window;

    protected override void Open()
    {
        base.Open();

        var casino = EntMan.GetComponentOrNull<ContactCasinoComponent>(Owner) ?? new ContactCasinoComponent();
        _window = this.CreateWindow<ContactCasinoWindow>();
        _window.Initialize(casino, EntMan.GetComponent<MetaDataComponent>(Owner).EntityName);
        ContactCasinoHelpers.Wire(this, _window.View);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window != null)
            ContactCasinoHelpers.Receive(_window.View, message);
    }
}

/// <summary>
/// Shared by the store BUI and the casino BUI.
/// </summary>
public static class ContactCasinoHelpers
{
    public static void Wire(BoundUserInterface bui, ContactCasinoView view)
    {
        view.Spin += bet => bui.SendMessage(new ContactSlotSpinMessage(bet));
        view.BlackjackAction += (action, amount) => bui.SendMessage(new ContactBlackjackActionMessage(action, amount));
    }

    /// <returns>True if it was a game message.</returns>
    public static bool Receive(ContactCasinoView view, BoundUserInterfaceMessage message)
    {
        switch (message)
        {
            case ContactSlotResultMessage slot:
                view.SlotResult(slot);
                return true;
            case ContactBlackjackStateMessage table:
                view.BlackjackState(table);
                return true;
            default:
                return false;
        }
    }
}
