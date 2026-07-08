using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.Mriya.Sponsors;
using Robust.Server.Player;
using Robust.Shared.Network;
using System.Linq;
using System.Threading.Tasks;
using static Content.Shared.Mriya.Sponsors.AdminSponsorsEuiMsg;
using DbSponsorRank = Content.Server.Database.SponsorRank;

namespace Content.Server.Mriya.Sponsors.UI;

public sealed class AdminSponsorsEui : BaseEui
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private ISponsorManager _sichSponsorManager = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly ISawmill _sawmill;
    private bool _isLoading;

    private readonly List<(MriyaSponsor a, string? lastUserName)> _sponsors = new();
    private readonly List<DbSponsorRank> _sponsorRanks = new();

    public AdminSponsorsEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _logManager.GetSawmill("sponsors.perms");
    }

    public override void Opened()
    {
        base.Opened();

        StateDirty();
        LoadFromDb();
        _adminManager.OnPermsChanged += AdminManagerOnPermsChanged;
    }

    public override void Closed()
    {
        base.Closed();

        _adminManager.OnPermsChanged -= AdminManagerOnPermsChanged;
    }

    private void AdminManagerOnPermsChanged(AdminPermsChangedEventArgs obj)
    {
        // Close UI if user loses +PERMISSIONS.
        if (obj.Player == Player && !UserAdminFlagCheck(AdminFlags.Permissions))
        {
            Close();
        }
    }

    public override EuiStateBase GetNewState()
    {
        if (_isLoading)
        {
            return new AdminSponsorsEuiState { IsLoading = true };
        }

        return new AdminSponsorsEuiState
        {
            Sponsors = _sponsors.Select(p => new AdminSponsorsEuiState.SponsorData
            {
                UserId = new NetUserId(p.a.UserId),
                UserName = p.lastUserName,

                RankIds = p.a.RoleAssignments?.Select(ra => ra.RankId).ToList() ?? new List<int>(),

                SelectedGhostColor = p.a.SelectedGhostColor,
                SelectedOocColor = p.a.SelectedOocColor,
                SelectedGhostRankId = p.a.SelectedGhostRankId,
                SelectedOocRankId = p.a.SelectedOocRankId
            }).ToArray(),

            SponsorRanks = _sponsorRanks.ToDictionary(a => a.Id, a => new AdminSponsorsEuiState.SponsorRankData
            {
                Name = a.Name,
                DefaultColor = Color.FromHex(a.DefaultColor),

                DefaultGhostColor = a.DefaultGhostColor,
                DefaultOocColor = a.DefaultOocColor,
                CanSetGhostColor = a.CanSetGhostColor,
                CanSetOocColor = a.CanSetOocColor,
                ShowInSponsorWindow = a.ShowInSponsorWindow,
                Priority = a.Priority,
                Tags = a.Tags?.Select(t => t.TagValue).ToList() ?? new List<string>()
            })
        };
    }

    public override async void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (!UserAdminFlagCheck(AdminFlags.Permissions))
            return;

        switch (msg)
        {
            case AddSponsor ca:
                await HandleCreateSponsor(ca);
                break;
            case UpdateSponsor ua:
                await HandleUpdateSponsor(ua);
                break;
            case RemoveSponsor ra:
                await HandleRemoveSponsor(ra);
                break;
            case AddSponsorRank ar:
                await HandleAddSponsorRank(ar);
                break;
            case UpdateSponsorRank ur:
                await HandleUpdateSponsorRank(ur);
                break;
            case RemoveSponsorRank ra:
                await HandleRemoveSponsorRank(ra);
                break;
        }

        if (!IsShutDown)
        {
            LoadFromDb();
        }
    }

    private async Task HandleRemoveSponsorRank(RemoveSponsorRank rr)
    {
        var rank = await _db.GetSponsorRankAsync(rr.Id);
        if (rank == null) return;

        await _db.RemoveSponsorRankAsync(rr.Id);
        _sawmill.Info($"{Player} removed sponsor rank {rank.Name}.");

        await _sichSponsorManager.ReloadSponsorsAsync();
    }

    private async Task HandleUpdateSponsorRank(UpdateSponsorRank ur)
    {
        var rank = await _db.GetSponsorRankAsync(ur.Id);
        if (rank == null) return;

        rank.Name = ur.Name;
        rank.DefaultColor = ur.DefaultColor.ToHex();
        rank.DefaultGhostColor = ur.DefaultGhostColor;
        rank.DefaultOocColor = ur.DefaultOocColor;
        rank.CanSetGhostColor = ur.CanSetGhostColor;
        rank.CanSetOocColor = ur.CanSetOocColor;
        rank.ShowInSponsorWindow = ur.ShowInSponsorWindow;
        rank.Priority = ur.Priority;

        rank.Tags = ur.Tags.Select(t => new RankTag { SponsorRankId = rank.Id, TagValue = t }).ToList();

        await _db.UpdateSponsorRankAsync(rank);
        _sawmill.Info($"{Player} updated sponsor rank {rank.Name}.");

        await _sichSponsorManager.ReloadSponsorsAsync();
    }

    private async Task HandleAddSponsorRank(AddSponsorRank ar)
    {
        var rank = new DbSponsorRank
        {
            Name = ar.Name,
            DefaultColor = ar.DefaultColor.ToHex(),
            DefaultGhostColor = ar.DefaultGhostColor,
            DefaultOocColor = ar.DefaultOocColor,
            CanSetGhostColor = ar.CanSetGhostColor,
            CanSetOocColor = ar.CanSetOocColor,
            ShowInSponsorWindow = ar.ShowInSponsorWindow,
            Priority = ar.Priority,
        };

        rank.Tags = ar.Tags.Select(t => new RankTag { TagValue = t }).ToList();

        await _db.AddSponsorRankAsync(rank);
        _sawmill.Info($"{Player} added sponsor rank {rank.Name}");

        await _sichSponsorManager.ReloadSponsorsAsync();
    }

    private async Task HandleRemoveSponsor(RemoveSponsor ra)
    {
        var sponsor = await _db.GetSponsorDataForAsync(ra.UserId);
        if (sponsor == null) return;

        await _db.RemoveSponsorAsync(ra.UserId);

        var record = await _db.GetPlayerRecordByUserId(ra.UserId);
        _sawmill.Info($"{Player} removed sponsor {record?.LastSeenUserName ?? ra.UserId.ToString()}");

        await _sichSponsorManager.ReloadSponsorAsync(ra.UserId);
    }

    private async Task HandleUpdateSponsor(UpdateSponsor ua)
    {
        var sponsor = await _db.GetSponsorDataForAsync(ua.UserId);
        if (sponsor == null) return;

        sponsor.RoleAssignments = ua.RankIds.Select(rankId => new SponsorRoleAssignment
        {
            UserId = sponsor.UserId,
            RankId = rankId
        }).ToList();

        sponsor.SelectedGhostColor = ua.SelectedGhostColor;
        sponsor.SelectedOocColor = ua.SelectedOocColor;
        sponsor.SelectedGhostRankId = ua.SelectedGhostRankId;
        sponsor.SelectedOocRankId = ua.SelectedOocRankId;

        await _db.UpdateSponsorAsync(sponsor);

        var playerRecord = await _db.GetPlayerRecordByUserId(ua.UserId);
        var name = playerRecord?.LastSeenUserName ?? ua.UserId.ToString();

        _sawmill.Info($"{Player} updated sponsor {name} with {ua.RankIds.Count} ranks");

        await _sichSponsorManager.ReloadSponsorAsync(ua.UserId);
    }

    private async Task HandleCreateSponsor(AddSponsor ca)
    {
        string name;
        NetUserId userId;
        if (Guid.TryParse(ca.UserNameOrId, out var guid))
        {
            userId = new NetUserId(guid);
            var playerRecord = await _db.GetPlayerRecordByUserId(userId);
            name = playerRecord == null ? userId.ToString() : playerRecord.LastSeenUserName;
        }
        else
        {
            var dbPlayer = await _db.GetPlayerRecordByUserName(ca.UserNameOrId);
            if (dbPlayer == null)
            {
                _sawmill.Warning($"{Player} tried to add sponsor with unknown username {ca.UserNameOrId}.");
                return;
            }
            userId = dbPlayer.UserId;
            name = ca.UserNameOrId;
        }

        var existing = await _db.GetSponsorDataForAsync(userId);
        if (existing != null) return;

        var sponsor = new MriyaSponsor
        {
            UserId = userId.UserId,
            RoleAssignments = ca.RankIds.Select(rankId => new SponsorRoleAssignment
            {
                UserId = userId.UserId,
                RankId = rankId
            }).ToList()
        };

        await _db.AddSponsorAsync(sponsor);
        _sawmill.Info($"{Player} added sponsor {name} with {ca.RankIds.Count} ranks");

        await _sichSponsorManager.ReloadSponsorAsync(userId);
    }

    private async void LoadFromDb()
    {
        StateDirty();
        _isLoading = true;

        var (sponsors, ranks) = await _db.GetAllMriyaSponsorsAsync();

        _sponsors.Clear();
        _sponsors.AddRange(sponsors);
        _sponsorRanks.Clear();
        _sponsorRanks.AddRange(ranks);

        _isLoading = false;
        StateDirty();
    }

    private bool UserAdminFlagCheck(AdminFlags flags)
    {
        return _adminManager.HasAdminFlag(Player, flags);
    }
}
