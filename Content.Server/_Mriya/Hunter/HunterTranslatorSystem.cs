using Content.Shared._Sich.Language;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;
using Content.Shared.Chat;
using Content.Shared.Chat.TypingIndicator;
using Content.Server.Chat.Systems;
using Content.Shared.Inventory;
using Robust.Server.GameObjects;

namespace Content.Server._Sich.Language;

public sealed class HunterTranslatorSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterTranslatorComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<HunterTranslatorComponent, GotUnequippedEvent>(OnUnequipped);

        SubscribeLocalEvent<HunterTranslatorComponent, HunterTranslatorActionEvent>(OnActionActivated);

        SubscribeLocalEvent<HunterTranslatorComponent, BoundUIClosedEvent>(OnBoundUIClosed);

        SubscribeLocalEvent<HunterTranslatorComponent, HunterTranslatorSendMessage>(OnSendMessage);
    }

    private void OnGetItemActions(EntityUid uid, HunterTranslatorComponent component, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags == null || !args.SlotFlags.Value.HasFlag(SlotFlags.GLOVES))
            return;

        args.AddAction(ref component.ActionEntity, component.ActionId, uid);
    }

    private void OnUnequipped(EntityUid uid, HunterTranslatorComponent component, GotUnequippedEvent args)
    {
        if (component.ActiveUser != null)
        {
            _ui.CloseUi(uid, HunterTranslatorUiKey.Key, GetEntity(component.ActiveUser.Value));
        }
    }

    private void OnActionActivated(EntityUid uid, HunterTranslatorComponent component, HunterTranslatorActionEvent args)
    {
        if (args.Handled)
            return;


        _ui.OpenUi(uid, HunterTranslatorUiKey.Key, args.Performer);
        component.ActiveUser = GetNetEntity(args.Performer);
        Dirty(uid, component);
        UpdateUi(uid, component);

        if (TryComp<AppearanceComponent>(args.Performer, out var appearance))
            _appearance.SetData(args.Performer, TypingIndicatorVisuals.State, TypingIndicatorState.Typing, appearance);

        args.Handled = true;
    }

    private void OnBoundUIClosed(EntityUid uid, HunterTranslatorComponent component, BoundUIClosedEvent args)
    {
        if (component.ActiveUser != GetNetEntity(args.Actor))
            return;

        component.ActiveUser = null;
        Dirty(uid, component);

        if (TryComp<AppearanceComponent>(args.Actor, out var appearance))
            _appearance.SetData(args.Actor, TypingIndicatorVisuals.State, TypingIndicatorState.None, appearance);
    }

    private void UpdateUi(EntityUid uid, HunterTranslatorComponent component)
    {
        _ui.SetUiState(uid, HunterTranslatorUiKey.Key, new HunterTranslatorBoundUserInterfaceState(component.TranslationTarget));
    }

    private void OnSendMessage(EntityUid uid, HunterTranslatorComponent component, HunterTranslatorSendMessage args)
    {
        var sender = args.Actor;
        if (sender == default)
            return;

        if (args.TranslationTarget.HasValue)
        {
            component.TranslationTarget = args.TranslationTarget.Value;
            Dirty(uid, component);
            UpdateUi(uid, component);
        }

        if (string.IsNullOrEmpty(args.Message))
            return;

        var message = args.Message;
        if (message.Length > component.MaxLength)
            message = message[..component.MaxLength];


        AddComp<HunterTranslatingMessageComponent>(sender);

        try
        {
            _chat.TrySendInGameICMessage(sender, message, InGameICChatType.Speak, false);
        }
        finally
        {
            RemCompDeferred<HunterTranslatingMessageComponent>(sender);
        }
    }


}
