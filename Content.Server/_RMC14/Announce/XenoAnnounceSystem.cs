using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server._CMU14.Announce; // Mriya. Порт оголошень з CMU
using Content.Shared._CMU14.Announce; // Mriya. Порт оголошень з CMU
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Evolution; // Mriya. Порт оголошень з CMU
using Content.Shared._RMC14.Xenonids.Word; // Mriya. Порт оголошень з CMU
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Announce;

public sealed class XenoAnnounceSystem : SharedXenoAnnounceSystem
{
    // Mriya start. Екранні оголошення королеви: пресет віджета та роутер оголошень з CMU.
    private const string QueenAnnouncementPreset = "XenoQueen";

    [Dependency] private readonly GeneralAnnounceSystem _generalAnnounce = default!;
    // Mriya end
    [Dependency] private readonly IAdminLogManager _adminLogs = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Announce(EntityUid source, Filter filter, string message, string wrapped, SoundSpecifier? sound = null, PopupType? popup = null, bool needsQueen = false)
    {
        base.Announce(source, filter, message, wrapped, sound, popup, needsQueen);

        if (needsQueen)
        {
            if (Hive.GetHive(source) is { } sourceHive)
            {
                if (!Hive.HasHiveQueen(sourceHive))
                    return;
            }
            else
            {
                return;
            }
        }

        filter.AddWhereAttachedEntity(HasComp<GhostComponent>);

        if (source.IsValid())
            _adminLogs.Add(LogType.RMCXenoAnnounce, $"{ToPrettyString(source):source} xeno announced message: {message}");

        // Mriya start. Додано умову IsQueenAnnouncementSource: екранний віджет отримує лише королева. В оригіналі був тільки чат зі звуком.
        if (source.IsValid() && IsQueenAnnouncementSource(source))
        {
            var request = new AnnouncementRequest
            {
                Message = message,
                Preset = QueenAnnouncementPreset,
                Target = AnnouncementTarget.Xenos,
                Speaker = source,
                Source = source,
                ShowSprite = false,
            };

            _generalAnnounce.AnnounceAdvanced(request, filter);
        }
        // Mriya end

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrapped, source, false, true, null);
        _audio.PlayGlobal(sound, filter, true);

        if (popup == null)
            return;

        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } recipient)
                _popup.PopupEntity(message, recipient, recipient, popup.Value);
        }
    }

    // Mriya start. Перевірка джерела-королеви для екранного оголошення.
    private bool IsQueenAnnouncementSource(EntityUid source)
    {
        return HasComp<XenoWordQueenComponent>(source) ||
               HasComp<XenoEvolutionGranterComponent>(source);
    }
    // Mriya end
}
