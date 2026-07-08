using Content.Server.Database;
using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.Mriya.Sponsors;
using System.Linq;

namespace Content.Server.Mriya.Sponsors.UI;

public sealed class SponsorListEui : BaseEui
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private ILogManager _logManager = default!;

    private readonly ISawmill _sawmill;
    private bool _isLoading = true;

    private readonly List<PublicSponsorEntry> _publicSponsors = new();

    public SponsorListEui()
    {
        IoCManager.InjectDependencies(this);
        _sawmill = _logManager.GetSawmill("sponsors.view");
    }

    public override void Opened()
    {
        base.Opened();
        LoadFromDb();
    }

    public override EuiStateBase GetNewState()
    {
        if (_isLoading)
        {
            return new SponsorListEuiState(new List<PublicSponsorEntry>());
        }

        return new SponsorListEuiState(_publicSponsors);
    }

    private async void LoadFromDb()
    {
        _isLoading = true;
        StateDirty();

        var (sponsors, ranks) = await _db.GetAllMriyaSponsorsAsync();

        _publicSponsors.Clear();
        var ranksDict = ranks.ToDictionary(r => r.Id);

        var sortedSponsors = sponsors
            .Select(s =>
            {
                var validRanks = s.sponsor.RoleAssignments
                    .Where(ra => ranksDict.ContainsKey(ra.RankId))
                    .Select(ra => ranksDict[ra.RankId])
                    .Where(r => r.ShowInSponsorWindow)
                    .ToList();

                return new { SponsorData = s, Ranks = validRanks };
            })
            .Where(x => x.Ranks.Count > 0)
            .Select(x =>
            {
                var topRank = x.Ranks.OrderBy(r => r.Priority).First();

                var userName = string.IsNullOrEmpty(x.SponsorData.lastUserName)
                    ? x.SponsorData.sponsor.UserId.ToString()
                    : x.SponsorData.lastUserName;

                return new
                {
                    Entry = new PublicSponsorEntry
                    {
                        UserName = userName,
                        TopRankName = topRank.Name,
                        TopRankColor = Color.FromHex(topRank.DefaultColor)
                    },
                    TopPriority = topRank.Priority
                };
            })
            .OrderBy(x => x.TopPriority)
            .ThenBy(x => x.Entry.UserName)
            .Select(x => x.Entry)
            .ToList();

        _publicSponsors.AddRange(sortedSponsors);

        _isLoading = false;
        StateDirty();
    }
}
