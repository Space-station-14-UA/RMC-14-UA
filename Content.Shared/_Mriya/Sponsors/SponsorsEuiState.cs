using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;


namespace Content.Shared.Mriya.Sponsors;

/// <summary>
/// EUI state for the personal sponsor settings window. Contains information about the user's current sponsor settings and allowed ranks.
/// </summary>
[Serializable, NetSerializable]
public sealed class PersonalSponsorSettingsEuiState : EuiStateBase
{
    public bool CanSetCustomGhostColor { get; }
    public bool CanSetCustomOocColor { get; }

    public string? CurrentGhostColor { get; }
    public string? CurrentOocColor { get; }
    public int? SelectedGhostRankId { get; }
    public int? SelectedOocRankId { get; }

    public List<PersonalSponsorRankInfo> AllowedRanks { get; }

    public PersonalSponsorSettingsEuiState(
        bool canSetCustomGhostColor,
        bool canSetCustomOocColor,
        string? currentGhostColor,
        string? currentOocColor,
        int? selectedGhostRankId,
        int? selectedOocRankId,
        List<PersonalSponsorRankInfo> allowedRanks)
    {
        CanSetCustomGhostColor = canSetCustomGhostColor;
        CanSetCustomOocColor = canSetCustomOocColor;
        CurrentGhostColor = currentGhostColor;
        CurrentOocColor = currentOocColor;
        SelectedGhostRankId = selectedGhostRankId;
        SelectedOocRankId = selectedOocRankId;
        AllowedRanks = allowedRanks;
    }
}

/// <summary>
/// Struct representing information about a personal sponsor rank. Contains the rank's ID, name, default color, and optional fixed colors for ghost and OOC modes.
/// </summary>
[Serializable, NetSerializable]
public struct PersonalSponsorRankInfo
{
    public int Id;
    public string Name;
    public string DefaultColor;
    public string? FixedGhostColor;
    public string? FixedOocColor;
}

/// <summary>
/// Wrapper for EUI messages related to the sponsor personal settings window. 
/// Contains classes for updating user settings.
/// </summary>
public static class PersonalSponsorEuiMsg
{
    /// <summary>
    /// EUI message for updating personal sponsor settings. Contains optional new values for ghost color, OOC color, and selected rank IDs. If a value is null, it indicates no change for that setting.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class UpdateSettings : EuiMessageBase
    {
        public string? NewGhostColor;
        public string? NewOocColor;
        public int? SelectedGhostRankId;
        public int? SelectedOocRankId;
    }
}

/// <summary>
/// Event message sent to request the personal sponsor settings window. This message is handled by the server to open the personal sponsor settings EUI for the requesting player.
/// </summary>

[Serializable, NetSerializable]
public sealed class RequestPersonalSponsorWindowMessage : EntityEventArgs
{
}

/// <summary>
/// Eui state for the admin sponsor management window. Contains information about all sponsors and their associated ranks, as well as loading state.
/// </summary>

[Serializable, NetSerializable]
public sealed class AdminSponsorsEuiState : EuiStateBase
{
    public bool IsLoading;

    public SponsorData[] Sponsors = Array.Empty<SponsorData>();
    public Dictionary<int, SponsorRankData> SponsorRanks = new();

    /// <summary>
    /// Struct representing information about a sponsor, including their user ID, username, associated rank IDs, and selected colors and ranks for ghost and OOC modes.
    /// </summary>
    [Serializable, NetSerializable]
    public struct SponsorData
    {
        public NetUserId UserId;
        public string? UserName;

        public List<int> RankIds;

        public string? SelectedGhostColor;
        public string? SelectedOocColor;
        public int? SelectedGhostRankId;
        public int? SelectedOocRankId;
    }

    /// <summary>
    /// Struct representing information about a sponsor rank, including its name, default color, optional default colors for ghost and OOC modes, permissions for setting custom colors, visibility in the sponsor window, priority, and associated tags.
    /// </summary>
    [Serializable, NetSerializable]
    public struct SponsorRankData
    {
        public string Name;
        public Color DefaultColor;

        public string? DefaultGhostColor;
        public string? DefaultOocColor;

        public bool CanSetGhostColor;
        public bool CanSetOocColor;

        public bool ShowInSponsorWindow;
        public int Priority;

        public List<string> Tags;
    }
}

/// <summary>
/// Wrapper for EUI messages related to the admin sponsor management window. Contains classes for adding, removing, and updating sponsors and sponsor ranks.
/// </summary>
public static class AdminSponsorsEuiMsg
{
    /// <summary>
    /// Eui message for adding a new sponsor. Contains the username or ID of the user to be added and a list of rank IDs to assign to the new sponsor. 
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class AddSponsor : EuiMessageBase
    {
        public string UserNameOrId = string.Empty;
        public List<int> RankIds = new();
    }

    /// <summary>
    /// Eui message for removing an existing sponsor. Contains the user ID of the sponsor to be removed.
    /// </summary>

    [Serializable, NetSerializable]
    public sealed class RemoveSponsor : EuiMessageBase
    {
        public NetUserId UserId;
    }

    /// <summary>
    /// Eui message for updating an existing sponsor's settings. Contains the user ID of the sponsor to be updated, a list of new rank IDs, and optional new values for selected ghost color, selected OOC color, selected ghost rank ID, and selected OOC rank ID. If a value is null, it indicates no change for that setting.
    /// </summary>

    [Serializable, NetSerializable]
    public sealed class UpdateSponsor : EuiMessageBase
    {
        public NetUserId UserId;

        public List<int> RankIds = new();

        public string? SelectedGhostColor;
        public string? SelectedOocColor;
        public int? SelectedGhostRankId;
        public int? SelectedOocRankId;
    }

    /// <summary>
    /// Eui message for adding a new sponsor rank. Contains the name of the rank, default color, optional default colors for ghost and OOC modes, permissions for setting custom colors, visibility in the sponsor window, priority, and associated tags.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class AddSponsorRank : EuiMessageBase
    {
        public string Name = string.Empty;
        public Color DefaultColor = Color.White;

        public string? DefaultGhostColor;
        public string? DefaultOocColor;

        public bool CanSetGhostColor;
        public bool CanSetOocColor;

        public bool ShowInSponsorWindow = true;
        public int Priority = 0;

        public List<string> Tags = new();
    }

    /// <summary>
    /// Eui message for removing an existing sponsor rank. Contains the ID of the rank to be removed.
    /// </summary>
    [Serializable, NetSerializable]
    public sealed class RemoveSponsorRank : EuiMessageBase
    {
        public int Id;
    }

    /// <summary>
    /// Eui message for updating an existing sponsor rank's settings. Contains the ID of the rank to be updated, name, default color, optional default colors for ghost and OOC modes, permissions for setting custom colors, visibility in the sponsor window, priority, and associated tags.
    /// </summary>

    [Serializable, NetSerializable]
    public sealed class UpdateSponsorRank : EuiMessageBase
    {
        public int Id;

        public string Name = string.Empty;
        public Color DefaultColor = Color.White;

        public string? DefaultGhostColor;
        public string? DefaultOocColor;

        public bool CanSetGhostColor;
        public bool CanSetOocColor;

        public bool ShowInSponsorWindow = true;
        public int Priority = 0;

        public List<string> Tags = new();
    }
}

/// <summary>
/// Request message for opening the admin sponsor management window. This message is sent to the server to request the current state of all sponsors and their associated ranks, which will be used to populate the admin sponsor management EUI.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestAdminSponsorWindowMessage : EntityEventArgs
{
}

/// <summary>
/// request message for opening the sponsor list window. This message is sent to the server to request the current list of sponsors, which will be used to populate the sponsor list EUI for all players.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestSponsorListWindowMessage : EntityEventArgs
{
}

/// <summary>
/// Eui state for the sponsor list window. Contains a list of public sponsor entries, each with a username, top rank name, and associated color. This state is used to display the sponsor list to all players.
/// </summary>
[Serializable, NetSerializable]
public sealed class SponsorListEuiState : EuiStateBase
{
    public List<PublicSponsorEntry> Sponsors { get; }

    public SponsorListEuiState(List<PublicSponsorEntry> sponsors)
    {
        Sponsors = sponsors;
    }
}

/// <summary>
/// Struct representing a public sponsor entry for the sponsor list window. Contains the username of the sponsor, the name of their top rank, and the associated color for that rank. This struct is used to display individual sponsor entries in the sponsor list EUI.
/// </summary>
[Serializable, NetSerializable]
public struct PublicSponsorEntry
{
    public string UserName;
    public string TopRankName;
    public Color TopRankColor;
}
