using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Server.Chat.Managers;
using Content.Shared._Mriya.Terminal;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Access.Components;
using Content.Shared.PDA;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._Mriya.Terminal;

public sealed class MriyaTerminalSystem : SharedMriyaTerminalSystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MriyaTerminalComponent, MriyaTerminalSendMessage>(OnSendMessage);
        SubscribeLocalEvent<MriyaTerminalComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<MriyaTerminalComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<MriyaTerminalComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnUIOpened(EntityUid uid, MriyaTerminalComponent component, BoundUIOpenedEvent args)
    {
        UpdateUI(uid, component);
    }

    private void OnItemInserted(EntityUid uid, MriyaTerminalComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.IdCardSlotId)
            return;

        UpdateAuthorization(uid, component);
    }

    private void OnItemRemoved(EntityUid uid, MriyaTerminalComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.IdCardSlotId)
            return;

        component.AuthorizedName = null;
        Dirty(uid, component);
        UpdateUI(uid, component);
    }

    private void UpdateAuthorization(EntityUid uid, MriyaTerminalComponent component)
    {
        component.AuthorizedName = null;

        if (_itemSlots.TryGetSlot(uid, component.IdCardSlotId, out var slot) && slot.Item != null)
        {
            var item = slot.Item.Value;
            if (TryComp<IdCardComponent>(item, out var idCard))
            {
                component.AuthorizedName = idCard.LocalizedJobTitle;
            }
            else if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId != null)
            {
                if (TryComp<IdCardComponent>(pda.ContainedId, out var pdaId))
                {
                    component.AuthorizedName = pdaId.LocalizedJobTitle;
                }
            }
        }

        Dirty(uid, component);
        UpdateUI(uid, component);
    }

    public void BroadcastMessage(string message)
    {
        var query = EntityQueryEnumerator<MriyaTerminalComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            component.Messages.Add(message);
            if (component.Messages.Count > 100)
                component.Messages.RemoveAt(0);

            Dirty(uid, component);
            UpdateUI(uid, component);
        }
    }

    private void OnSendMessage(EntityUid uid, MriyaTerminalComponent component, MriyaTerminalSendMessage args)
    {
        if (!component.IsInput)
            return;

        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        var message = args.Message.Trim();
        if (message.Length > 500)
            message = message[..500];

        var name = Name(uid);
        var sender = component.AuthorizedName ?? "НЕВІДОМИЙ";
        
        var ckey = "НЕВІДОМИЙ";
        if (TryComp<ActorComponent>(args.Actor, out var actor))
            ckey = actor.PlayerSession.Name;

        _chatManager.SendAdminAnnouncement($"Термінал ({name}) отримав повідомлення від {sender} ({ckey}): {message}");
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{args.Actor:player} відправив повідомлення в термінал {name} (від {sender}): {message}");

        _audio.PlayPvs(component.ClickSound, uid);
        BroadcastMessage($"[{sender}]: {message}");
    }

    private void UpdateUI(EntityUid uid, MriyaTerminalComponent component)
    {
        _ui.SetUiState(uid, MriyaTerminalUiKey.Key, new MriyaTerminalState(component.IsInput, component.Messages, component.AuthorizedName));
    }

    public void SendCorporateMessage(string message)
    {
        BroadcastMessage($"ВЕСТОН-ЯМАДА > {message.ToUpper()}");
    }
}
