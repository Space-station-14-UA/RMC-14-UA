using Content.Server._RMC14.Announce;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.Nuke;

public sealed class MriyaIntelNukeSystem : EntitySystem
{
    [Dependency] private readonly IntelSystem _intel = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly SharedGameTicker _ticker = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
    private static readonly int[] DecodeAnnouncementThresholds = [240, 180, 120, 60, 30];
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _nextUpdate = default;

        var query = EntityQueryEnumerator<MriyaIntelNukeObjectiveComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RemCompDeferred<MriyaIntelNukeObjectiveComponent>(uid);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        if (time < _nextUpdate)
            return;

        _nextUpdate = time + UpdateInterval;

        var query = EntityQueryEnumerator<IntelTechTreeComponent>();
        while (query.MoveNext(out var uid, out var tree))
        {
            var objective = EnsureComp<MriyaIntelNukeObjectiveComponent>(uid);
            ProcessObjective((uid, objective), (uid, tree), time);
            break;
        }
    }

    private void ProcessObjective(
        Entity<MriyaIntelNukeObjectiveComponent> objective,
        Entity<IntelTechTreeComponent> tree,
        TimeSpan time)
    {
        var comp = objective.Comp;
        if (comp.LastUpdatedAt == default)
            comp.LastUpdatedAt = time;

        var elapsed = time - comp.LastUpdatedAt;
        comp.LastUpdatedAt = time;

        if (comp.Stage == MriyaIntelNukeStage.WaitingForIntel)
        {
            if (!CanStartAuthorization(tree, comp))
                return;

            comp.Stage = MriyaIntelNukeStage.WaitingForTowers;
            Announce(Loc.GetString("mriya-nuke-intel-fragments-recovered"));
            AnnounceXenos(Loc.GetString("mriya-nuke-xeno-intel-fragments-recovered"));
        }

        if (comp.Stage is not (MriyaIntelNukeStage.WaitingForTowers or MriyaIntelNukeStage.Decoding))
            return;

        var activeTowers = CountActiveMarineTowers();
        if (activeTowers < comp.RequiredTowers)
        {
            if (comp.Stage == MriyaIntelNukeStage.Decoding)
            {
                comp.Stage = MriyaIntelNukeStage.WaitingForTowers;
                comp.DecodeProgress = TimeSpan.Zero;
                comp.DecodeAnnouncedAtSeconds.Clear();
                Announce(Loc.GetString("mriya-nuke-decryption-paused", ("percent", 0)));
                AnnounceXenos(Loc.GetString("mriya-nuke-xeno-decryption-paused", ("percent", 0)));
            }

            AnnounceTowerStatusIfNeeded(comp, activeTowers, time);
            return;
        }

        if (comp.Stage == MriyaIntelNukeStage.WaitingForTowers)
        {
            comp.Stage = MriyaIntelNukeStage.Decoding;
            var minutes = Math.Max(1, (int) Math.Ceiling((comp.DecodeDuration - comp.DecodeProgress).TotalMinutes));
            Announce(Loc.GetString("mriya-nuke-decryption-resumed", ("remaining", FormatRemaining(minutes * 60))));
            AnnounceXenos(Loc.GetString("mriya-nuke-xeno-decryption-resumed", ("remaining", FormatRemainingUkrainian(minutes * 60))));
        }

        comp.DecodeProgress += elapsed;
        if (comp.DecodeProgress < comp.DecodeDuration)
        {
            AnnounceDecodeProgress(comp);
            return;
        }

        AuthorizeChargePurchase(objective, tree);
    }

    private int CountActiveMarineTowers()
    {
        var count = 0;
        var towers = EntityQueryEnumerator<CommunicationsTowerComponent>();
        while (towers.MoveNext(out var tower))
        {
            if (tower.State != CommunicationsTowerState.On || tower.XenoControlled)
                continue;

            count++;
        }

        return count;
    }

    private void AnnounceTowerStatusIfNeeded(MriyaIntelNukeObjectiveComponent comp, int activeTowers, TimeSpan time)
    {
        if (comp.LastReportedActiveTowers == activeTowers &&
            time < comp.NextTowerStatusAt)
        {
            return;
        }

        comp.LastReportedActiveTowers = activeTowers;
        comp.NextTowerStatusAt = time + TimeSpan.FromMinutes(1);
        Announce(Loc.GetString("mriya-nuke-decryption-waiting-towers", ("active", activeTowers), ("required", comp.RequiredTowers)));
        AnnounceXenos(Loc.GetString("mriya-nuke-xeno-decryption-waiting-towers", ("active", activeTowers), ("required", comp.RequiredTowers)));
    }

    private void AnnounceDecodeProgress(MriyaIntelNukeObjectiveComponent comp)
    {
        var remaining = (int) Math.Ceiling((comp.DecodeDuration - comp.DecodeProgress).TotalSeconds);
        foreach (var threshold in DecodeAnnouncementThresholds)
        {
            if (remaining > threshold ||
                !comp.DecodeAnnouncedAtSeconds.Add(threshold))
            {
                continue;
            }

            var percent = GetDecodePercent(comp);
            Announce(Loc.GetString("mriya-nuke-decryption-progress", ("remaining", FormatRemaining(threshold)), ("percent", percent)));
            AnnounceXenos(Loc.GetString("mriya-nuke-xeno-decryption-progress", ("remaining", FormatRemainingUkrainian(threshold)), ("percent", percent)));
            return;
        }
    }

    private void AuthorizeChargePurchase(
        Entity<MriyaIntelNukeObjectiveComponent> objective,
        Entity<IntelTechTreeComponent> tree)
    {
        var comp = objective.Comp;
        if (comp.Stage == MriyaIntelNukeStage.ChargeAuthorized)
            return;

        if (TryFindChargeOption(tree, comp.ChargePrototype, out var tier, out var optionIndex, out var option))
        {
            tier[optionIndex] = option with { Disabled = false };
            comp.Stage = MriyaIntelNukeStage.ChargeAuthorized;
            Dirty(tree);
            _intel.UpdateTree(tree);

            Announce(Loc.GetString("mriya-nuke-decryption-complete"));
            AnnounceXenos(Loc.GetString("mriya-nuke-xeno-decryption-complete"));
            return;
        }

        Announce(Loc.GetString("mriya-nuke-decryption-complete-missing-option"));
    }

    private bool CanStartAuthorization(Entity<IntelTechTreeComponent> tree, MriyaIntelNukeObjectiveComponent comp)
    {
        if (tree.Comp.Tree.TotalEarned < comp.RequiredIntelPoints)
            return false;

        if (!TryFindChargeOption(tree, comp.ChargePrototype, out _, out _, out var option, out var tierIndex))
            return false;

        return tree.Comp.Tree.Tier >= tierIndex &&
               option.TimeLock <= _ticker.RoundDuration();
    }

    private bool TryFindChargeOption(
        Entity<IntelTechTreeComponent> tree,
        EntProtoId chargePrototype,
        out List<TechOption> tier,
        out int optionIndex,
        out TechOption option)
    {
        return TryFindChargeOption(tree, chargePrototype, out tier, out optionIndex, out option, out _);
    }

    private bool TryFindChargeOption(
        Entity<IntelTechTreeComponent> tree,
        EntProtoId chargePrototype,
        out List<TechOption> tier,
        out int optionIndex,
        out TechOption option,
        out int tierIndex)
    {
        for (tierIndex = 0; tierIndex < tree.Comp.Tree.Options.Count; tierIndex++)
        {
            tier = tree.Comp.Tree.Options[tierIndex];
            for (optionIndex = 0; optionIndex < tier.Count; optionIndex++)
            {
                option = tier[optionIndex];
                if (DeliversCharge(option, chargePrototype))
                    return true;
            }
        }

        tier = default!;
        optionIndex = -1;
        option = default;
        return false;
    }

    private bool DeliversCharge(TechOption option, EntProtoId chargePrototype)
    {
        foreach (var ev in option.Events)
        {
            if (ev is TechLogisticsDeliveryEvent logistics &&
                logistics.Object == chargePrototype)
            {
                return true;
            }
        }

        return false;
    }

    private int GetDecodePercent(MriyaIntelNukeObjectiveComponent comp)
    {
        if (comp.DecodeDuration <= TimeSpan.Zero)
            return 100;

        return Math.Clamp((int) (comp.DecodeProgress.TotalSeconds / comp.DecodeDuration.TotalSeconds * 100), 0, 100);
    }

    private string FormatRemaining(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return Loc.GetString(minutes == 1 ? "mriya-nuke-time-minute" : "mriya-nuke-time-minutes", ("minutes", minutes));
        }

        return Loc.GetString(seconds == 1 ? "mriya-nuke-time-second" : "mriya-nuke-time-seconds", ("seconds", seconds));
    }

    private string FormatRemainingUkrainian(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return Loc.GetString("mriya-nuke-time-ukrainian", ("value", minutes), ("unit", GetUkrainianPlural(minutes, "mriya-nuke-time-ukrainian-minute-one", "mriya-nuke-time-ukrainian-minute-few", "mriya-nuke-time-ukrainian-minute-many")));
        }

        return Loc.GetString("mriya-nuke-time-ukrainian", ("value", seconds), ("unit", GetUkrainianPlural(seconds, "mriya-nuke-time-ukrainian-second-one", "mriya-nuke-time-ukrainian-second-few", "mriya-nuke-time-ukrainian-second-many")));
    }

    private string GetUkrainianPlural(int value, string oneKey, string fewKey, string manyKey)
    {
        var mod100 = value % 100;
        if (mod100 is >= 11 and <= 14)
            return Loc.GetString(manyKey);

        var key = (value % 10) switch
        {
            1 => oneKey,
            >= 2 and <= 4 => fewKey,
            _ => manyKey,
        };
        return Loc.GetString(key);
    }

    private void Announce(string message)
    {
        _marineAnnounce.AnnounceARESStaging(null, message);
    }

    private void AnnounceXenos(string message)
    {
        _xenoAnnounce.AnnounceQueenMother(message);
    }
}
