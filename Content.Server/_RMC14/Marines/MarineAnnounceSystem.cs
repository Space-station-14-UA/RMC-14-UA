using Content.Server._CMU14.Announce; // Mriya. Порт оголошень з CMU
using Content.Server._RMC14.Rules.DistressSignal; // Mriya. Порт оголошень з CMU
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Radio.EntitySystems;
using Content.Shared._RMC14.AlertLevel; // Mriya. Порт оголошень з CMU
using Content.Shared._CMU14.Announce; // Mriya. Порт оголошень з CMU
using Content.Shared._RMC14.ARES; // Mriya. Порт оголошень з CMU
using Content.Shared._RMC14.ARES.Logs;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Rules;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost; // Mriya. Порт оголошень з CMU
using Content.Shared.Ghost;
using Content.Shared.Radio;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Maths; // Mriya. Порт оголошень з CMU
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Marines;

public sealed class MarineAnnounceSystem : SharedMarineAnnounceSystem
{
    [Dependency] private readonly IAdminLogManager _adminLogs = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ARESCoreSystem _core = default!;
    [Dependency] private readonly CMDistressSignalRuleSystem _distressSignal = default!;
    [Dependency] private readonly SharedDropshipSystem _dropship = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly GeneralAnnounceSystem _generalAnnounce = default!;     // Mriya. Роутер екранних оголошень.
    [Dependency] private readonly SquadSystem _squad = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly EntProtoId<ARESLogTypeComponent> LogCat = "ARESTabAnnouncementLogs";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MarineCommunicationsComputerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MarineCommunicationsComputerComponent, BoundUIOpenedEvent>(OnBUIOpened);

        SubscribeLocalEvent<RMCPlanetComponent, RMCPlanetAddedEvent>(OnPlanetAdded);

        Subs.BuiEvents<MarineCommunicationsComputerComponent>(MarineCommunicationsComputerUI.Key,
            subs =>
            {
                subs.Event<MarineCommunicationsDesignatePrimaryLZMsg>(OnMarineCommunicationsDesignatePrimaryLZMsg);
            });
    }

    private void OnMapInit(Entity<MarineCommunicationsComputerComponent> computer, ref MapInitEvent args)
    {
        UpdatePlanetMap(computer);
    }

    private void OnBUIOpened(Entity<MarineCommunicationsComputerComponent> computer, ref BoundUIOpenedEvent args)
    {
        UpdatePlanetMap(computer);
    }

    private void OnPlanetAdded(Entity<RMCPlanetComponent> ent, ref RMCPlanetAddedEvent args)
    {
        var computers = EntityQueryEnumerator<MarineCommunicationsComputerComponent>();
        while (computers.MoveNext(out var uid, out var computer))
        {
            UpdatePlanetMap((uid, computer));
        }
    }

    private void OnMarineCommunicationsDesignatePrimaryLZMsg(
        Entity<MarineCommunicationsComputerComponent> computer,
        ref MarineCommunicationsDesignatePrimaryLZMsg args)
    {
        var user = args.Actor;
        if (!TryGetEntity(args.LZ, out var lz))
        {
            Log.Warning($"{ToPrettyString(user)} tried to designate invalid entity {args.LZ} as primary LZ!");
            return;
        }

        _dropship.TryDesignatePrimaryLZ(user, lz.Value);
        _core.CreateARESLog(computer, LogCat, (string)$"{Name(args.Actor)} designated Primary LZ as: {Name(lz.Value)}");
    }

    private void UpdatePlanetMap(Entity<MarineCommunicationsComputerComponent> computer)
    {
        var planet = _distressSignal.SelectedPlanetMapName ?? string.Empty;
        var operation = _distressSignal.OperationName ?? string.Empty;
        var landingZones = new List<LandingZone>();

        foreach (var (id, metaData) in _dropship.GetPrimaryLZCandidates())
        {
            landingZones.Add(new LandingZone(GetNetEntity(id), metaData.EntityName));
        }

        landingZones.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        var state = new MarineCommunicationsComputerBuiState(planet, operation, landingZones);
        _ui.SetUiState(computer.Owner, MarineCommunicationsComputerUI.Key, state);
    }

    public override void AnnounceToMarines(
        string message,
        SoundSpecifier? sound = null,
        Filter? filter = null)
    {
        filter ??= Filter.Empty()
            .AddWhereAttachedEntity(e =>
                HasComp<MarineComponent>(e) ||
                HasComp<GhostComponent>(e)
            );

        // Filter out non rescued survivors.
        filter.RemoveWhereAttachedEntity(HasComp<IntelRescueSurvivorObjectiveComponent>);

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, message, default, false, true, null);
        _audio.PlayGlobal(sound ?? DefaultAnnouncementSound, filter, true, AudioParams.Default.WithVolume(-2f));
    }

    public override void AnnounceHighCommand(
        string message,
        string? author = null,
        SoundSpecifier? sound = null)
    {
        var wrappedMessage = FormatHighCommand(author, message);
        AnnounceToMarines(wrappedMessage);
    }

    public override void AnnounceRadio(
        EntityUid sender,
        string message,
        ProtoId<RadioChannelPrototype> channel)
    {
        base.AnnounceRadio(sender, message, channel);

        _adminLogs.Add(LogType.RMCMarineAnnounce, $"{ToPrettyString(sender):source} marine announced radio message: {message}");
        _radio.SendRadioMessage(sender, message, channel, sender);
    }

    public override void AnnounceARESStaging(
        EntityUid? source,
        string message,
        SoundSpecifier? sound = null,
        LocId? announcement = null)
    {
        base.AnnounceARESStaging(source, message, sound, announcement);

        message = FormatARESStaging(announcement, message);

        AnnounceToMarines(message, sound);
        _adminLogs.Add(LogType.RMCMarineAnnounce, $"{ToPrettyString(source):player} ARES announced message: {message}");
    }

    public override void AnnounceSquad(string message, EntProtoId<SquadTeamComponent> squad, SoundSpecifier? sound = null)
    {
        base.AnnounceSquad(message, squad, sound);

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, message, default, false, true, null);
        _audio.PlayGlobal(sound ?? DefaultSquadSound, filter, true, AudioParams.Default.WithVolume(-2f));
    }

    public override void AnnounceSquad(string message, EntityUid squad, SoundSpecifier? sound = null)
    {
        base.AnnounceSquad(message, squad, sound);

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, message, default, false, true, null);
        _audio.PlayGlobal(sound ?? DefaultSquadSound, filter, true, AudioParams.Default.WithVolume(-2f));
    }

    public override void AnnounceSingle(string message, EntityUid receiver, SoundSpecifier? sound = null)
    {
        base.AnnounceSingle(message, receiver, sound);

        if (TryComp(receiver, out ActorComponent? actor))
            _chatManager.ChatMessageToOne(ChatChannel.Radio, message, message, default, false, actor.PlayerSession.Channel);

        _audio.PlayEntity(sound, receiver, receiver, AudioParams.Default.WithVolume(-2f));
    }

    // Mriya start. Екранний віджет підписаних оголошень командування (пресет MarineCommand).
    protected override void AnnounceSignedUi(
        EntityUid sender,
        string message,
        string author,
        string name,
        SoundSpecifier? sound,
        Filter? filter)
    {
        var request = new AnnouncementRequest
        {
            Message = message,
            Preset = "MarineCommand",
            Target = AnnouncementTarget.Marines,
            Speaker = sender,
            ShowSprite = true
        };

        var uiFilter = filter == null
            ? Filter.Empty().AddWhereAttachedEntity(e =>
                HasComp<MarineComponent>(e) ||
                HasComp<GhostComponent>(e))
            : Filter.Empty().AddPlayers(filter.Recipients);

        _generalAnnounce.AnnounceAdvanced(request, uiFilter);
    }
    // Mriya end

    // Mriya start. Екранний віджет овервотчу загону.
    public override void AnnounceOverwatchSquad(
        EntityUid sender,
        string message,
        EntityUid squad,
        Color squadColor,
        string squadName,
        SoundSpecifier? sound = null)
    {
        var colorHex = squadColor.ToHex();
        var chatMessage =
            $"[color={colorHex}][bold]Overwatch:[/bold] transmits: [font size=16][bold]{message}[/bold][/font][/color]";

        AnnounceSquad(chatMessage, squad, sound);

        var title = $"{squadName.Trim().ToUpperInvariant()} СПОСТЕРЕЖЕННЯ";
        var styleOverride = new AnnouncementStyleOverride
        {
            PrimaryColor = squadColor,
            TitleColor = squadColor
        };

        var request = new AnnouncementRequest
        {
            Message = $"Overwatch transmits: {message}",
            Preset = "MarineOverwatch",
            Target = AnnouncementTarget.Marines,
            Title = title,
            ShowSprite = true,
            StyleOverride = styleOverride
        };

        var filter = Filter.Empty().AddWhereAttachedEntity(e => _squad.IsInSquad(e, squad));
        _generalAnnounce.AnnounceAdvanced(request, filter);
    }
    // Mriya end

    public override void AnnounceAlertLevel(RMCAlertLevels level, string message, Filter? filter = null)
    {
        // Mriya start. Екранний віджет рівнів тривоги.
        var (title, color, decalState) = level switch
        {
            RMCAlertLevels.Green => ($"РІВЕНЬ ТРИВОГИ: ЗЕЛЕНИЙ", Color.LawnGreen, (string?)null),
            RMCAlertLevels.Blue => ($"РІВЕНЬ ТРИВОГИ: СИНІЙ", Color.DodgerBlue, "bluealert"),
            RMCAlertLevels.Red => ($"РІВЕНЬ ТРИВОГИ: ЧЕРВОНИЙ", Color.Red, "redalert"),
            RMCAlertLevels.Delta => ($"РІВЕНЬ ТРИВОГИ: ДЕЛЬТА", Color.DarkRed, "evac"),
            _ => ($"РІВЕНЬ ТРИВОГИ: {level.ToString().ToUpperInvariant()}", Color.White, "default")
        };

        var styleOverride = new AnnouncementStyleOverride
        {
            PrimaryColor = color,
            TitleColor = color
        };

        var request = new AnnouncementRequest
        {
            Message = message,
            Preset = "MarineAlertLevel",
            Target = AnnouncementTarget.Marines,
            Title = title,
            ShowSprite = false,
            StyleOverride = styleOverride
        };

        if (!string.IsNullOrEmpty(decalState))
        {
            request.DecalRsi = "/Textures/_RMC14/Structures/Machines/status_display.rsi";
            request.DecalState = decalState;
            request.DecalPlacement = AnnouncementDecalPlacement.ReplaceSprite;
            request.DecalScale = 3.0f;
        }

        if (filter != null)
            _generalAnnounce.AnnounceAdvanced(request, filter);
        else
            _generalAnnounce.AnnounceAdvanced(request);
    }
    // Mriya end
}
