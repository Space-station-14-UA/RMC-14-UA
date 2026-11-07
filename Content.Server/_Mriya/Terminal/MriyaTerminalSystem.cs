using Content.Server.Administration.Logs;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Shared.Database;
using Content.Server.Chat.Managers;
using Content.Shared._Mriya.Terminal;
using Content.Shared.Chat;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.PDA;
using Content.Shared.Audio;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mriya.Terminal;

public sealed class MriyaTerminalSystem : SharedMriyaTerminalSystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly GunIFFSystem _gunIFF = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const int MaxHistory = 100;
    private const string WeyaChannel = "WEYA";
    private readonly List<string> _history = new();
    private readonly List<MriyaTerminalAdminEui> _adminEuis = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MriyaTerminalComponent, MriyaTerminalSendMessage>(OnSendMessage);
        SubscribeLocalEvent<MriyaTerminalComponent, BoundUIOpenedEvent>(OnUIOpened);
        SubscribeLocalEvent<MriyaTerminalComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<MriyaTerminalComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _history.Clear();
        NotifyAdminEuis();
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
        component.AuthorizedJob = null;
        Dirty(uid, component);
        UpdateUI(uid, component);
    }

    private void UpdateAuthorization(EntityUid uid, MriyaTerminalComponent component)
    {
        component.AuthorizedName = null;
        component.AuthorizedJob = null;

        if (_itemSlots.TryGetSlot(uid, component.IdCardSlotId, out var slot) && slot.Item != null)
        {
            var item = slot.Item.Value;
            if (TryComp<IdCardComponent>(item, out var idCard))
            {
                SetAuthorizedOperator(component, idCard);
            }
            else if (TryComp<PdaComponent>(item, out var pda) && pda.ContainedId != null)
            {
                if (TryComp<IdCardComponent>(pda.ContainedId, out var pdaId))
                {
                    SetAuthorizedOperator(component, pdaId);
                }
            }
        }

        Dirty(uid, component);
        UpdateUI(uid, component);
    }

    private static void SetAuthorizedOperator(MriyaTerminalComponent component, IdCardComponent idCard)
    {
        var job = idCard.LocalizedJobTitle;
        component.AuthorizedJob = job;
        component.AuthorizedName = string.IsNullOrWhiteSpace(idCard.FullName) ? job : idCard.FullName;
    }

    public EntityUid? BroadcastMessage(string message)
    {
        EntityUid? firstTerminal = null;
        var query = EntityQueryEnumerator<MriyaTerminalComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            firstTerminal ??= uid;

            // Add message to the terminal
            component.Messages.Add(message);
            if (component.Messages.Count > MaxHistory)
                component.Messages.RemoveAt(0);

            // Play admin sound only for admin messages
            if (message.StartsWith("ВЕСТОН-ЯМАДА >"))
                _audio.PlayPvs(component.AdminMessageSound, uid);
            Dirty(uid, component);
            UpdateUI(uid, component);
        }

        _history.Add(message);
        if (_history.Count > MaxHistory)
            _history.RemoveAt(0);

        NotifyAdminEuis();
        return firstTerminal;
    }

    public void RegisterAdminEui(MriyaTerminalAdminEui eui)
    {
        _adminEuis.Add(eui);
    }

    public void UnregisterAdminEui(MriyaTerminalAdminEui eui)
    {
        _adminEuis.Remove(eui);
    }

    public List<string> GetHistory()
    {
        return new List<string>(_history);
    }

    private void NotifyAdminEuis()
    {
        foreach (var eui in _adminEuis)
        {
            eui.StateDirty();
        }
    }

    private void OnSendMessage(EntityUid uid, MriyaTerminalComponent component, MriyaTerminalSendMessage args)
    {
        if (!component.IsInput)
            return;

        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        _audio.PlayPvs(component.ClickSound, uid);

        // Check if there is an ID card/PDA inserted in the terminal
        if (!_itemSlots.TryGetSlot(uid, component.IdCardSlotId, out var slot) || slot.Item == null)
        {
            component.Messages.Add("ВЕСТОН-ЯМАДА > СТАТУС КОРИСТУВАЧА: НЕВИЗНАЧЕНО\nДОСТУП ЗАБЛОКОВАНО. ПОТРІБНА ID ІДЕНТИФІКАЦІЯ.");
            if (component.Messages.Count > 100)
                component.Messages.RemoveAt(0);

            Dirty(uid, component);
            UpdateUI(uid, component);
            return;
        }

        // If access levels are required, check if the inserted card/PDA has at least one of them
        var card = slot.Item.Value;

        // Check if the card belongs to a hostile faction (UPP/SPP or CLF)
        if (_gunIFF.IsInFaction(card, "FactionSPP") || _gunIFF.IsInFaction(card, "FactionCLF"))
        {
            component.Messages.Add("ВЕСТОН-ЯМАДА > СТАТУС КОРИСТУВАЧА: ВОРОГ КОМПАНІЇ.\nУ ДОСТУПІ АВТОМАТИЧНО ВІДХИЛЕНО.");
            if (component.Messages.Count > 100)
                component.Messages.RemoveAt(0);

            Dirty(uid, component);
            UpdateUI(uid, component);
            return;
        }

        if (component.RequiredAccesses.Count > 0)
        {
            var tags = _accessReader.FindAccessTags(card);
            var hasAccess = false;
            foreach (var reqAccess in component.RequiredAccesses)
            {
                if (tags.Contains(reqAccess))
                {
                    hasAccess = true;
                    break;
                }
            }

            if (!hasAccess)
            {
                component.Messages.Add("ВЕСТОН-ЯМАДА > ВАШ РІВЕНЬ ДОСТУПУ НЕ ВІДПОВІДАЄ ВИМОГАМ КОРПОРАТИВНОЇ МЕРЕЖІ. ДОСТУП ДО КАНАЛУ КОРПОРАТИВНИХ КОМУНІКАЦІЙ АВТОМАТИЧНО ВІДХИЛЕНО.");
                if (component.Messages.Count > 100)
                    component.Messages.RemoveAt(0);

                Dirty(uid, component);
                UpdateUI(uid, component);
                return;
            }
        }

        var message = args.Message.Trim();
        if (message.Length > 500)
            message = message[..500];

        var name = Name(uid);
        var sender = component.AuthorizedName ?? component.AuthorizedJob ?? "НЕВІДОМИЙ";
        
        var ckey = "НЕВІДОМИЙ";
        if (TryComp<ActorComponent>(args.Actor, out var actor))
            ckey = actor.PlayerSession.Name;

        _chatManager.SendAdminAnnouncement($"Термінал ({name}) отримав повідомлення від {sender} ({ckey}): {message}");
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"{args.Actor:player} відправив повідомлення в термінал {name} (від {sender}): {message}");

        BroadcastMessage($"[{sender}]: {message}");
    }

    private void UpdateUI(EntityUid uid, MriyaTerminalComponent component)
    {
        _ui.SetUiState(uid, MriyaTerminalUiKey.Key, new MriyaTerminalState(
            component.IsInput,
            component.Messages,
            component.AuthorizedName,
            component.AuthorizedJob));
    }

    public void SendCorporateMessage(string message)
    {
        message = message.Trim();
        if (message.Length == 0)
            return;

        if (message.Length > 500)
            message = message[..500];

        var terminal = BroadcastMessage($"ВЕСТОН-ЯМАДА > {message.ToUpper()}");
        if (terminal != null)
            NotifyWeyaRadio(terminal.Value);
    }

    private void NotifyWeyaRadio(EntityUid terminal)
    {
        if (!_prototype.TryIndex<RadioChannelPrototype>(WeyaChannel, out var channel))
            return;

        var sendAttempt = new RadioSendAttemptEvent(channel, terminal);
        RaiseLocalEvent(ref sendAttempt);
        RaiseLocalEvent(terminal, ref sendAttempt);
        if (sendAttempt.Cancelled)
            return;

        var message = Loc.GetString("mriya-terminal-radio-incoming");
        var wrappedMessage = Loc.GetString("chat-radio-system-message",
            ("color", channel.Color),
            ("fontType", "Default"),
            ("fontSize", 12),
            ("channel", $"\\[{channel.LocalizedName}\\]"),
            ("message", FormattedMessage.EscapeText(message)));

        var filter = Filter.Empty();
        var radioQuery = EntityQueryEnumerator<ActiveRadioComponent>();
        while (radioQuery.MoveNext(out var receiver, out var radio))
        {
            if (!radio.ReceiveAllChannels)
            {
                if (!radio.Channels.Contains(channel.ID))
                    continue;

                if (TryComp<IntercomComponent>(receiver, out var intercom) &&
                    !intercom.SupportedChannels.Contains(channel.ID))
                    continue;
            }

            var listener = ResolveRadioListener(receiver);
            if (listener == null)
                continue;

            var receiveAttempt = new RadioReceiveAttemptEvent(channel, terminal, receiver);
            RaiseLocalEvent(ref receiveAttempt);
            RaiseLocalEvent(receiver, ref receiveAttempt);
            if (receiveAttempt.Cancelled)
                continue;

            if (TryComp<ActorComponent>(listener.Value, out var actor))
                filter.AddPlayer(actor.PlayerSession);
        }

        if (filter.Count == 0)
            return;

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, default, false, true, null);
    }

    private EntityUid? ResolveRadioListener(EntityUid receiver)
    {
        if (HasComp<IntrinsicRadioReceiverComponent>(receiver))
            return receiver;

        var wearer = Transform(receiver).ParentUid;
        if (wearer.IsValid() &&
            TryComp<WearingHeadsetComponent>(wearer, out var wearing) &&
            wearing.Headset == receiver)
            return wearer;

        return null;
    }
}
