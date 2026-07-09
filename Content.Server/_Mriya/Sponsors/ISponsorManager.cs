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

    /// <summary>
    /// Loads sponsor data for a user. Called when the user connects.
    /// </summary>
    /// <param name="session">The player session.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task<PlayerSponsorData> LoadData(ICommonSession session, CancellationToken cancel);

    /// <summary>
    /// Finalizes the sponsor data loading process for a user. Called after the data has been loaded and is ready for use.
    /// </summary>
    /// <param name="session">The player session.</param>
    void FinishLoad(ICommonSession session);

    /// <summary>
    /// Clears the player's cache after disconnection. Called when the player disconnects from the server.
    /// </summary>
    /// <param name="session">The player session.</param>
    void OnClientDisconnected(ICommonSession session);

    /// <summary>
    /// Checks whether the user's sponsor data is present in the cache. Used to verify if the sponsor settings for a specific user have been loaded.
    /// </summary>
    /// <param name="session">The player session.</param>
    /// <returns>True if the data exists in the cache; otherwise, false.</returns>
    bool HavePreferencesLoaded(ICommonSession session);

    /// <summary>
    /// Attempts to get the cached sponsor settings for a specific user. 
    /// Returns true if the data exists in the cache and is retrieved successfully; otherwise, false.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="playerSponsor">When this method returns, contains the sponsor data if found; otherwise, null.</param>
    /// <returns>True if the data was found in the cache; otherwise, false.</returns>
    bool TryGetCachedSponsor(NetUserId userId, [NotNullWhen(true)] out MriyaSponsor? playerPreferences);

    /// <summary>
    /// Gets the sponsor settings for a specific user from the cache. 
    /// Throws an exception if the data has not been loaded yet.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The sponsor settings for the specified user.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the sponsor settings are not yet loaded in the cache.</exception>
    MriyaSponsor GetSponsor(NetUserId userId);

    /// <summary>
    /// Returns the sponsor or null if not found. Provides a safe way to check for a sponsor without throwing an exception.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <returns>The sponsor data if found; otherwise, null.</returns>
    MriyaSponsor? GetMriyaSponsorOrNull(NetUserId? userId);

    /// <summary>
    /// Checks if the player has a specific tag within their active ranks. 
    /// Used to verify access permissions for certain features or content based on sponsor tags.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="tag">The tag to check for.</param>
    /// <returns><c>true</c> if the player has the tag; otherwise, <c>false</c>.</returns>
    bool HasTag(NetUserId userId, string tag);

    /// <summary>
    /// Returns the selected ghost color if the player has permission to use it; otherwise, null.
    /// </summary>
    string? GetGhostColor(NetUserId userId);

    /// <summary>
    /// Returns the selected OOC chat color if the player has permission to use it; otherwise, null.
    /// </summary>
    string? GetOocColor(NetUserId userId);

    /// <summary>
    /// Completely reloads all online players. Use with caution due to potential database overhead.
    /// </summary>
    Task ReloadSponsorsAsync();

    /// <summary>
    /// Reloads the data for a specific player. Useful when an admin updates a player's rank during the game.
    /// </summary>
    Task ReloadSponsorAsync(NetUserId userId, CancellationToken cancel = default);

    /// <summary>
    /// Instantly updates the object in the cache without querying the database. 
    /// Used after a player changes settings (e.g., color) via the UI and those changes have already been saved to the database.
    /// </summary>
    void UpdateCache(NetUserId userId, MriyaSponsor updatedSponsor);
}
