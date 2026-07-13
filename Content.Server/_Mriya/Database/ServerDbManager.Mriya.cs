using Robust.Shared.Network;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<((MriyaSponsor sponsor, string? lastUserName)[] sponsors, SponsorRank[] ranks)> GetAllMriyaSponsorsAsync(CancellationToken cancel = default);
    Task<SponsorRank?> GetSponsorRankAsync(int id, CancellationToken cancel = default);
    Task RemoveSponsorRankAsync(int rankId, CancellationToken cancel = default);
    Task AddSponsorRankAsync(SponsorRank rank, CancellationToken cancel = default);
    Task UpdateSponsorRankAsync(SponsorRank rank, CancellationToken cancel = default);

    Task<MriyaSponsor?> GetSponsorDataForAsync(NetUserId userId, CancellationToken cancel = default);
    Task RemoveSponsorAsync(NetUserId userId, CancellationToken cancel = default);
    Task AddSponsorAsync(MriyaSponsor sponsor, CancellationToken cancel = default);
    Task UpdateSponsorAsync(MriyaSponsor sponsor, CancellationToken cancel = default);
}

public sealed partial class ServerDbManager
{
    public Task<((MriyaSponsor sponsor, string? lastUserName)[] sponsors, SponsorRank[] ranks)>
            GetAllMriyaSponsorsAsync(CancellationToken cancel)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetAllMriyaSponsorsAsync(cancel));
    }

    public Task<SponsorRank?> GetSponsorRankAsync(int id, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetSponsorRankDataForAsync(id, cancel));
    }

    public Task RemoveSponsorRankAsync(int rankId, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.RemoveSponsorRankAsync(rankId, cancel));
    }

    public Task AddSponsorRankAsync(SponsorRank rank, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddSponsorRankAsync(rank, cancel));
    }

    public Task UpdateSponsorRankAsync(SponsorRank rank, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.UpdateSponsorRankAsync(rank, cancel));
    }

    public Task<MriyaSponsor?> GetSponsorDataForAsync(NetUserId userId, CancellationToken cancel = default)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetSponsorDataForAsync(userId, cancel));
    }

    public Task RemoveSponsorAsync(NetUserId userId, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.RemoveSponsorAsync(userId, cancel));
    }

    public Task AddSponsorAsync(MriyaSponsor sponsor, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.AddSponsorAsync(sponsor, cancel));
    }

    public Task UpdateSponsorAsync(MriyaSponsor sponsor, CancellationToken cancel = default)
    {
        DbWriteOpsMetric.Inc();
        return RunDbCommand(() => _db.UpdateSponsorAsync(sponsor, cancel));
    }
}
