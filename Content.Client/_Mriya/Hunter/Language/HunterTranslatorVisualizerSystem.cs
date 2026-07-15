using Content.Shared._Sich.Language;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;

namespace Content.Client._Sich.Language;

public sealed class HunterTranslatorVisualizerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HunterTranslatorComponent, InventoryRelayedEvent<BeforeShowTypingIndicatorEvent>>(OnBeforeShowTypingIndicator);
    }

    private void OnBeforeShowTypingIndicator(EntityUid uid, HunterTranslatorComponent component, ref InventoryRelayedEvent<BeforeShowTypingIndicatorEvent> args)
    {
        if (component.ActiveUser != null)
        {
            args.Args.TryUpdateTimeAndIndicator("hunter_translate", TimeSpan.MaxValue);
        }
    }
}
