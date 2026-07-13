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
/// Manages sponsor configurations. Caches data on player connection and exposes a convenient API for other systems.
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
    /// <summary>
    /// Loads sponsor data for a user. Called when the user connects.
    /// </summary>
    /// <param name="session">The player session.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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
                var spons = await GetSponsorAsync(session.UserId, cancel);
                sponsorData.Sponsor = spons;
            }
            return sponsorData;
        }
    }

    /// <summary>
    /// Finalizes the sponsor data loading process for a user. Called after the data has been loaded and is ready for use.
    /// </summary>
    /// <param name="session">The player session.</param>
    public void FinishLoad(ICommonSession session)
    {
        var sponsData = _cachedPlayerPrefs[session.UserId];
        sponsData.SponsorLoaded = true;

        SyncTags(session);
    }

    /// <summary>
    /// Synchronizes the sponsor tags for a user by sending the relevant information to the client. This method is called after the sponsor data has been loaded and is ready for use.
    /// </summary>
    /// <param name="session">The player session.</param>
    private void SyncTags(ICommonSession session)
    {
        if (!_cachedPlayerPrefs.TryGetValue(session.UserId, out var data) || data.Sponsor == null)
            return;

        var sponsor = data.Sponsor;
        var msg = new MsgSponsorInfo();

        msg.Tags = sponsor.RoleAssignments
            .Where(ra => ra.Rank != null)
            .SelectMany(ra => ra.Rank!.Tags.Select(t => t.TagValue))
            .Distinct()
            .ToList();

        _netManager.ServerSendMessage(msg, session.Channel);
    }

    /// <summary>
    /// Clears the player's cache after disconnection. Called when the player disconnects from the server.
    /// </summary>
    /// <param name="session">The player session.</param>
    public void OnClientDisconnected(ICommonSession session)
    {
        _cachedPlayerPrefs.Remove(session.UserId);
    }

    /// <summary>
    /// Checks whether the user's sponsor data is present in the cache. Used to verify if the sponsor settings for a specific user have been loaded.
    /// </summary>
    /// <param name="session">The player session.</param>
    /// <returns>True if the data exists in the cache; otherwise, false.</returns>
    public bool HavePreferencesLoaded(ICommonSession session)
    {
        return _cachedPlayerPrefs.ContainsKey(session.UserId);
    }

    /// <summary>
    /// Gets sponsor data for a specific user from the database.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The sponsor data if found; otherwise, <c>null</c>.</returns>
    private async Task<MriyaSponsor?> GetSponsorAsync(NetUserId userId, CancellationToken cancel)
    {
        var prefs = await _db.GetSponsorDataForAsync(userId, cancel);
        return prefs;
    }

    /// <summary>
    /// Determines whether the sponsor preferences should be stored for a given login type. This is used to decide if the sponsor data should be cached for a user based on their login type.
    /// </summary>
    /// <param name="loginType"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Attempts to get the cached sponsor settings for a specific user. 
    /// Returns true if the data exists in the cache and is retrieved successfully; otherwise, false.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="playerSponsor">When this method returns, contains the sponsor data if found; otherwise, null.</param>
    /// <returns>True if the data was found in the cache; otherwise, false.</returns>
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

    /// <summary>
    /// Gets the sponsor settings for a specific user from the cache. 
    /// Throws an exception if the data has not been loaded yet.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The sponsor settings for the specified user.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the sponsor settings are not yet loaded in the cache.</exception>
    public MriyaSponsor GetSponsor(NetUserId userId)
    {
        var spons = _cachedPlayerPrefs[userId].Sponsor;
        if (spons == null)
        {
            throw new InvalidOperationException("Preferences for this player have not loaded yet.");
        }

        return spons;
    }

    /// <summary>
    /// Returns the sponsor or null if not found. Provides a safe way to check for a sponsor without throwing an exception.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The sponsor data if found; otherwise, null.</returns>
    public MriyaSponsor? GetMriyaSponsorOrNull(NetUserId? userId)
    {
        if (userId == null)
            return null;

        if (_cachedPlayerPrefs.TryGetValue(userId.Value, out var spons))
            return spons.Sponsor;
        return null;
    }

    #endregion

    #region Feature Helpers (Фасад)

    /// <summary>
    /// Checks if the player has a specific tag within their active ranks. 
    /// Used to verify access permissions for certain features or content based on sponsor tags.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="tag">The tag to check for.</param>
    /// <returns><c>true</c> if the player has the tag; otherwise, <c>false</c>.</returns>
    public bool HasTag(NetUserId userId, string tag)
    {
        if (!TryGetCachedSponsor(userId, out var sponsor) || sponsor.RoleAssignments == null)
            return false;

        return sponsor.RoleAssignments.Any(ra =>
            ra.Rank != null && ra.Rank.Tags != null && ra.Rank.Tags.Any(t => t.TagValue == tag));
    }

    /// <summary>
    /// Returns the selected ghost color if the player has permission to use it; otherwise, null.
    /// </summary>
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

    /// <summary>
    /// Returns the selected OOC chat color if the player has permission to use it; otherwise, null.
    /// </summary>
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

    /// <summary>
    /// Completely reloads all online players. Use with caution due to potential database overhead.
    /// </summary>
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

    /// <summary>
    /// Reloads the data for a specific player. Useful when an admin updates a player's rank during the game.
    /// </summary>
    public async Task ReloadSponsorAsync(NetUserId userId, CancellationToken cancel = default)
    {
        if (!_playerManager.TryGetSessionById(userId, out var session))
            return;

        var spons = await GetSponsorAsync(userId, cancel);

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

    /// <summary>
    /// Instantly updates the object in the cache without querying the database. 
    /// Used after a player changes settings (e.g., color) via the UI and those changes have already been saved to the database.
    /// </summary>
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
