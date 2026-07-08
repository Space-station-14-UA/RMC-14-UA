using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared.Preferences;
using Content.Shared.Mriya.Sponsors;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Mriya.Sponsors;

/// <summary>
/// Менеджер налаштувань спонсорів. Кешує дані при підключенні та надає зручний API для інших систем.
/// </summary>
public sealed class SponsorManager : ISponsorManager, IPostInjectInit
{
    [Dependency] private IServerNetManager _netManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private IServerPreferencesManager _prefsManager = default!;

    // Cache player prefs on the server so we don't need as much async hell related to them.
    private readonly Dictionary<NetUserId, PlayerSponsorData> _cachedPlayerPrefs = new();

    private ISawmill _sawmill = default!;

    public void Init()
    {
        _netManager.RegisterNetMessage<MsgSponsorInfo>();
        _sawmill = _log.GetSawmill("sponsorPrefs");
    }

    #region Lifecycle & Database Loading

    // Should only be called via UserDbDataManager.
    public async Task<PlayerSponsorData> LoadData(ICommonSession session, CancellationToken cancel = default)
    {
        if (!ShouldStorePrefs(session.Channel.AuthType))
        {
            // Don't store data for guests.
            var sponsorData = new PlayerSponsorData
            {
                SponsorLoaded = true,
                Sponsor = null
            };

            _cachedPlayerPrefs[session.UserId] = sponsorData;
            return sponsorData;
        }
        else
        {
            var sponsorData = new PlayerSponsorData();
            var loadTask = LoadPrefs();
            _cachedPlayerPrefs[session.UserId] = sponsorData;

            await loadTask;

            async Task LoadPrefs()
            {
                var spons = await GetOrCreateSponsorAsync(session.UserId, cancel);
                sponsorData.Sponsor = spons;
            }
            return sponsorData;
        }
    }

    public void FinishLoad(ICommonSession session)
    {
        var sponsData = _cachedPlayerPrefs[session.UserId];
        sponsData.SponsorLoaded = true;

        SyncTags(session);
    }

    private void SyncTags(ICommonSession session)
    {
        if (!_cachedPlayerPrefs.TryGetValue(session.UserId, out var data) || data.Sponsor == null)
            return;

        var sponsor = data.Sponsor;
        var msg = new MsgSponsorInfo();

        // Збираємо унікальні теги з усіх призначених ролей
        msg.Tags = sponsor.RoleAssignments
            .Where(ra => ra.Rank != null)
            .SelectMany(ra => ra.Rank!.Tags.Select(t => t.TagValue))
            .Distinct()
            .ToList();

        _netManager.ServerSendMessage(msg, session.Channel);
    }

    public void OnClientDisconnected(ICommonSession session)
    {
        _cachedPlayerPrefs.Remove(session.UserId);
    }

    public bool HavePreferencesLoaded(ICommonSession session)
    {
        return _cachedPlayerPrefs.ContainsKey(session.UserId);
    }

    private async Task<MriyaSponsor?> GetOrCreateSponsorAsync(NetUserId userId, CancellationToken cancel)
    {
        var prefs = await _db.GetSponsorDataForAsync(userId, cancel);
        return prefs;
    }

    internal static bool ShouldStorePrefs(LoginType loginType)
    {
        return loginType.HasStaticUserId();
    }

    void IPostInjectInit.PostInject()
    {
        Init();

        _userDb.AddOnLoadPlayer(LoadData);
        _userDb.AddOnFinishLoad(FinishLoad);
        _userDb.AddOnPlayerDisconnect(OnClientDisconnected);
    }

    #endregion

    #region Raw Data Access

    public bool TryGetCachedSponsor(NetUserId userId, [NotNullWhen(true)] out MriyaSponsor? playerSponsor)
    {
        if (_cachedPlayerPrefs.TryGetValue(userId, out var spons))
        {
            playerSponsor = spons.Sponsor;
            return spons.Sponsor != null;
        }

        playerSponsor = null;
        return false;
    }

    public MriyaSponsor GetSponsor(NetUserId userId)
    {
        var spons = _cachedPlayerPrefs[userId].Sponsor;
        if (spons == null)
        {
            throw new InvalidOperationException("Preferences for this player have not loaded yet.");
        }

        return spons;
    }

    public MriyaSponsor? GetSichSponsorOrNull(NetUserId? userId)
    {
        if (userId == null)
            return null;

        if (_cachedPlayerPrefs.TryGetValue(userId.Value, out var spons))
            return spons.Sponsor;
        return null;
    }

    #endregion

    #region Feature Helpers (Фасад)

    public bool HasTag(NetUserId userId, string tag)
    {
        if (!TryGetCachedSponsor(userId, out var sponsor) || sponsor.RoleAssignments == null)
            return false;

        return sponsor.RoleAssignments.Any(ra =>
            ra.Rank != null && ra.Rank.Tags != null && ra.Rank.Tags.Any(t => t.TagValue == tag));
    }

    public string? GetGhostColor(NetUserId userId)
    {
        if (!TryGetCachedSponsor(userId, out var sponsor) || sponsor.RoleAssignments == null)
            return null;

        var canSetCustomColor = sponsor.RoleAssignments.Any(ra => ra.Rank != null && ra.Rank.CanSetGhostColor);
        if (canSetCustomColor && !string.IsNullOrEmpty(sponsor.SelectedGhostColor))
        {
            return sponsor.SelectedGhostColor;
        }

        if (sponsor.SelectedGhostRankId != null)
        {
            var selectedRank = sponsor.RoleAssignments
                .FirstOrDefault(ra => ra.RankId == sponsor.SelectedGhostRankId)?.Rank;

            if (selectedRank != null && !string.IsNullOrEmpty(selectedRank.DefaultGhostColor))
            {
                return selectedRank.DefaultGhostColor;
            }
        }

        return null;
    }

    public string? GetOocColor(NetUserId userId)
    {
        if (!TryGetCachedSponsor(userId, out var sponsor) || sponsor.RoleAssignments == null)
            return null;

        var canSetCustomColor = sponsor.RoleAssignments.Any(ra => ra.Rank != null && ra.Rank.CanSetOocColor);
        if (canSetCustomColor && !string.IsNullOrEmpty(sponsor.SelectedOocColor))
        {
            return sponsor.SelectedOocColor;
        }

        if (sponsor.SelectedOocRankId != null)
        {
            var selectedRank = sponsor.RoleAssignments
                .FirstOrDefault(ra => ra.RankId == sponsor.SelectedOocRankId)?.Rank;

            if (selectedRank != null && !string.IsNullOrEmpty(selectedRank.DefaultOocColor))
            {
                return selectedRank.DefaultOocColor;
            }
        }

        return null;
    }

    #endregion

    #region Cache Management

    public async Task ReloadSponsorsAsync()
    {
        _cachedPlayerPrefs.Clear();
        var chanels = _netManager.Channels.ToList();
        foreach (var chanel in chanels)
        {
            if (!chanel.IsConnected)
                continue;

            var session = _playerManager.GetSessionByChannel(chanel);
            if (session == null)
                continue;

            await LoadData(session);
            SyncTags(session);
        }
    }

    public async Task ReloadSponsorAsync(NetUserId userId, CancellationToken cancel = default)
    {
        if (!_playerManager.TryGetSessionById(userId, out var session))
            return;

        var spons = await GetOrCreateSponsorAsync(userId, cancel);

        if (_cachedPlayerPrefs.TryGetValue(userId, out var data))
        {
            data.Sponsor = spons;
        }
        else
        {
            _cachedPlayerPrefs[userId] = new PlayerSponsorData { SponsorLoaded = true, Sponsor = spons };
        }

        SyncTags(session);
        _prefsManager.RefreshPreferences(userId);
    }

    public void UpdateCache(NetUserId userId, MriyaSponsor updatedSponsor)
    {
        if (_cachedPlayerPrefs.TryGetValue(userId, out var data))
        {
            data.Sponsor = updatedSponsor;
        }
        else
        {
            _cachedPlayerPrefs[userId] = new PlayerSponsorData { SponsorLoaded = true, Sponsor = updatedSponsor };
        }

        if (_playerManager.TryGetSessionById(userId, out var session))
        {
            SyncTags(session);
            _prefsManager.RefreshPreferences(userId);
        }
    }

    #endregion
}

public sealed class PlayerSponsorData
{
    public bool SponsorLoaded;
    public MriyaSponsor? Sponsor;
}
