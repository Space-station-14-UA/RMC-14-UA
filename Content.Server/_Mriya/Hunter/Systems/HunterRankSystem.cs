using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.GameTicking;
using Robust.Shared.Utility;

namespace Content.Server._Mriya.Hunter.Systems;

public sealed class HunterRankSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RankComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(Entity<RankComponent> ent, ref PlayerSpawnCompleteEvent args)
    {
        if (ent.Comp.Rank is null)
            return;

        SetHunterMapBlip(ent.Owner, ent.Comp.Rank.Value.Id);
    }

    public void SetHunterMapBlip(EntityUid mob, string rankId)
    {
        string? blipState = rankId switch
        {
            "MRRankYoungBlood" => "young",
            "MRRankHunter" => "pred",
            "MRRankClanCommander" => "captain",
            "MRRankClanHead" => "captain",
            "MRRankElders" => "captain",
            _ => null
        };

        if (blipState == null)
            return;

        var blip = EnsureComp<MapBlipIconOverrideComponent>(mob);
        blip.Icon = new SpriteSpecifier.Rsi(
            new ResPath("/Textures/_Mriya/Hunter/Interface/map_blips.rsi"),
            blipState);
        Dirty(mob, blip);
    }
}
