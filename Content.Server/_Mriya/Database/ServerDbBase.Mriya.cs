using Microsoft.EntityFrameworkCore;
using Robust.Shared.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<((MriyaSponsor sponsor, string? lastUserName)[] sponsors, SponsorRank[] ranks)> GetAllMriyaSponsorsAsync(CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var preferences = await db.DbContext.MriyaSponsors
            .Include(p => p.RoleAssignments)
                .ThenInclude(ra => ra.Rank)
            .Include(p => p.SelectedGhostRank)
            .Include(p => p.SelectedOocRank)
            .AsSplitQuery()
            .ToArrayAsync(cancel);

        var userIds = preferences.Select(p => p.UserId).Distinct().ToList();

        var userNames = await db.DbContext.Player
            .Where(p => userIds.Contains(p.UserId))
            .Select(p => new { p.UserId, p.LastSeenUserName })
            .ToArrayAsync(cancel);

        var nameDict = userNames.ToDictionary(x => x.UserId, x => x.LastSeenUserName);

        var preferenceTuples = preferences
            .Select(p => (p, nameDict.TryGetValue(p.UserId, out var name) ? name : null))
            .ToArray();

        // Отримуємо всі ранги разом із їхніми тегами
        var sponsorRanks = await db.DbContext.SponsorRanks
            .Include(r => r.Tags)
            .AsSplitQuery()
            .ToArrayAsync(cancel);

        return (preferenceTuples, sponsorRanks);
    }

    public async Task<SponsorRank?> GetSponsorRankDataForAsync(int id, CancellationToken cancel = default)
    {
        await using var db = await GetDb(cancel);

        return await db.DbContext.SponsorRanks
            .Include(r => r.Tags)
            .SingleOrDefaultAsync(r => r.Id == id, cancel);
    }

    public async Task RemoveSponsorRankAsync(int rankId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var rank = await db.DbContext.SponsorRanks.SingleAsync(a => a.Id == rankId, cancel);
        db.DbContext.SponsorRanks.Remove(rank);

        await db.DbContext.SaveChangesAsync(cancel);
    }

    public async Task AddSponsorRankAsync(SponsorRank rank, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        db.DbContext.SponsorRanks.Add(rank);

        await db.DbContext.SaveChangesAsync(cancel);
    }

    public async Task UpdateSponsorRankAsync(SponsorRank rank, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var existing = await db.DbContext.SponsorRanks
            .Include(r => r.Tags)
            .SingleAsync(a => a.Id == rank.Id, cancel);

        existing.Name = rank.Name;
        existing.DefaultColor = rank.DefaultColor;
        existing.CanSetGhostColor = rank.CanSetGhostColor;
        existing.CanSetOocColor = rank.CanSetOocColor;

        existing.DefaultGhostColor = rank.DefaultGhostColor;
        existing.DefaultOocColor = rank.DefaultOocColor;
        existing.ShowInSponsorWindow = rank.ShowInSponsorWindow;
        existing.Priority = rank.Priority;

        db.DbContext.RankTags.RemoveRange(existing.Tags);

        var newTags = rank.Tags.Select(t => new RankTag { SponsorRankId = existing.Id, TagValue = t.TagValue }).ToList();
        existing.Tags = newTags;

        await db.DbContext.SaveChangesAsync(cancel);
    }

    public async Task<MriyaSponsor?> GetSponsorDataForAsync(NetUserId userId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        return await db.DbContext.MriyaSponsors
            .Include(p => p.RoleAssignments)
                .ThenInclude(ra => ra.Rank)
                    .ThenInclude(r => r.Tags)
            .Include(p => p.SelectedGhostRank)
            .Include(p => p.SelectedOocRank)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.UserId == userId.UserId, cancel);
    }

    public async Task RemoveSponsorAsync(NetUserId userId, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var sponsor = await db.DbContext.MriyaSponsors.SingleAsync(a => a.UserId == userId.UserId, cancel);
        db.DbContext.MriyaSponsors.Remove(sponsor);

        await db.DbContext.SaveChangesAsync(cancel);
    }

    public async Task AddSponsorAsync(MriyaSponsor sponsor, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        db.DbContext.MriyaSponsors.Add(sponsor);

        await db.DbContext.SaveChangesAsync(cancel);
    }

    public async Task UpdateSponsorAsync(MriyaSponsor sponsor, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);

        var existing = await db.DbContext.MriyaSponsors
            .Include(s => s.RoleAssignments)
            .SingleAsync(a => a.UserId == sponsor.UserId, cancel);

        existing.SelectedGhostColor = sponsor.SelectedGhostColor;
        existing.SelectedOocColor = sponsor.SelectedOocColor;

        existing.SelectedGhostRankId = sponsor.SelectedGhostRankId;
        existing.SelectedOocRankId = sponsor.SelectedOocRankId;

        db.DbContext.SponsorRoleAssignments.RemoveRange(existing.RoleAssignments);

        var newRoles = sponsor.RoleAssignments.Select(ra => new SponsorRoleAssignment
        {
            UserId = existing.UserId,
            RankId = ra.RankId
        }).ToList();

        existing.RoleAssignments = newRoles;

        await db.DbContext.SaveChangesAsync(cancel);
    }
}
