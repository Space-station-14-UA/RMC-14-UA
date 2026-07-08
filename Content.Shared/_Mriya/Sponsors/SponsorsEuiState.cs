using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;


namespace Content.Shared.Mriya.Sponsors;

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

[Serializable, NetSerializable]
public struct PersonalSponsorRankInfo
{
    public int Id;
    public string Name;
    public string DefaultColor;
    public string? FixedGhostColor;
    public string? FixedOocColor;
}

public static class PersonalSponsorEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class UpdateSettings : EuiMessageBase
    {
        public string? NewGhostColor;
        public string? NewOocColor;
        public int? SelectedGhostRankId;
        public int? SelectedOocRankId;
    }
}

[Serializable, NetSerializable]
public sealed class RequestPersonalSponsorWindowMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class AdminSponsorsEuiState : EuiStateBase
{
    public bool IsLoading;

    public SponsorData[] Sponsors = Array.Empty<SponsorData>();
    public Dictionary<int, SponsorRankData> SponsorRanks = new();

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

public static class AdminSponsorsEuiMsg
{
    [Serializable, NetSerializable]
    public sealed class AddSponsor : EuiMessageBase
    {
        public string UserNameOrId = string.Empty;
        public List<int> RankIds = new();
    }

    [Serializable, NetSerializable]
    public sealed class RemoveSponsor : EuiMessageBase
    {
        public NetUserId UserId;
    }

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

    [Serializable, NetSerializable]
    public sealed class RemoveSponsorRank : EuiMessageBase
    {
        public int Id;
    }

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

[Serializable, NetSerializable]
public sealed class RequestAdminSponsorWindowMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class RequestSponsorListWindowMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class SponsorListEuiState : EuiStateBase
{
    public List<PublicSponsorEntry> Sponsors { get; }

    public SponsorListEuiState(List<PublicSponsorEntry> sponsors)
    {
        Sponsors = sponsors;
    }
}

[Serializable, NetSerializable]
public struct PublicSponsorEntry
{
    public string UserName;
    public string TopRankName;
    public Color TopRankColor;
}
