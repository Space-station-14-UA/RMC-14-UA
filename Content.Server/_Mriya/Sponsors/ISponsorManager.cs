using Content.Server.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Mriya.Sponsors;

public interface ISponsorManager
{
    void Init();
    Task<PlayerSponsorData> LoadData(ICommonSession session, CancellationToken cancel);
    void FinishLoad(ICommonSession session);
    void OnClientDisconnected(ICommonSession session);
    bool HavePreferencesLoaded(ICommonSession session);

    bool TryGetCachedSponsor(NetUserId userId, [NotNullWhen(true)] out MriyaSponsor? playerPreferences);
    MriyaSponsor GetSponsor(NetUserId userId);
    MriyaSponsor? GetSichSponsorOrNull(NetUserId? userId);

    /// <summary>
    /// Перевіряє, чи має гравець в своїх активних рангах вказаний тег.
    /// </summary>
    bool HasTag(NetUserId userId, string tag);

    /// <summary>
    /// Повертає обраний колір привида, якщо гравець має на це права. Інакше - null.
    /// </summary>
    string? GetGhostColor(NetUserId userId);

    /// <summary>
    /// Повертає обраний колір OOC чату, якщо гравець має на це права. Інакше - null.
    /// </summary>
    string? GetOocColor(NetUserId userId);

    // --- Керування кешем та перезавантаження ---
    /// <summary>
    /// Повністю перечитує всіх онлайн-гравців. Викликати обережно через можливе навантаження на БД.
    /// </summary>
    Task ReloadSponsorsAsync();

    /// <summary>
    /// Перезавантажує дані конкретного гравця. Корисно, коли адмін оновив ранг гравця під час гри.
    /// </summary>
    Task ReloadSponsorAsync(NetUserId userId, CancellationToken cancel = default);

    /// <summary>
    /// Миттєво оновлює об'єкт у кеші без запиту до БД. 
    /// Використовується після того, як гравець змінив налаштування (наприклад, колір) через UI і ми вже зберегли це в БД.
    /// </summary>
    void UpdateCache(NetUserId userId, MriyaSponsor updatedSponsor);
}
