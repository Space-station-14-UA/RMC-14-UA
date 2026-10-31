using System.Linq;
using Content.Shared._Mriya.ATM;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client._Mriya.ATM;

[UsedImplicitly]
public sealed class ATMBoundUserInterface : BoundUserInterface
{
    private ATMWindow? _window;

    public ATMBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var prototype = IoCManager.Resolve<IPrototypeManager>();
        var sprite = EntMan.System<SpriteSystem>();

        _window = new ATMWindow
        {
            PreviewResolver = id =>
            {
                if (!prototype.TryIndex<EntityPrototype>(id.Id, out var entity))
                    return new List<Texture>();

                return sprite.GetPrototypeTextures(entity).Select(texture => texture.Default).ToList();
            },
        };
        _window.OnClose += Close;
        _window.OnBuy += (category, entry) => SendMessage(new ATMBuyMsg(category, entry));
        _window.OnWithdraw += amount => SendMessage(new ATMWithdrawMsg(amount));
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ATMBuiState atmState)
            _window?.SetState(atmState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
            _window?.Dispose();
    }
}
