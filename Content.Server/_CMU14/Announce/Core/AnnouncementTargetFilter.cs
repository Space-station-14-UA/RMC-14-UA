// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using System.Collections.Generic;
using Content.Server._RMC14.Marines;
using Content.Shared._CMU14.Announce;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Announce.Core;

public sealed class AnnouncementTargetFilter
{
    private readonly IEntityManager _entityManager;

    public AnnouncementTargetFilter(IEntityManager entityManager)
    {
        _entityManager = entityManager;
    }

    public Filter Build(AnnouncementTarget target)
    {
        var allPlayers = Filter.Broadcast();

        switch (target)
        {
            case AnnouncementTarget.Marines:
                var marineFilter = new List<ICommonSession>();
                foreach (var session in allPlayers.Recipients)
                {
                    if (session.AttachedEntity is not { } entity)
                        continue;

                    if (_entityManager.HasComponent<MarineComponent>(entity) ||
                        _entityManager.HasComponent<GhostComponent>(entity))
                    {
                        marineFilter.Add(session);
                    }
                }
                return Filter.Empty().AddPlayers(marineFilter);

            case AnnouncementTarget.Xenos:
                var xenoFilter = new List<ICommonSession>();
                foreach (var session in allPlayers.Recipients)
                {
                    if (session.AttachedEntity is not { } entity)
                        continue;

                    if (_entityManager.HasComponent<XenoComponent>(entity) ||
                        _entityManager.HasComponent<GhostComponent>(entity))
                    {
                        xenoFilter.Add(session);
                    }
                }
                return Filter.Empty().AddPlayers(xenoFilter);

            case AnnouncementTarget.All:
            default:
                return allPlayers;
        }
    }
}
