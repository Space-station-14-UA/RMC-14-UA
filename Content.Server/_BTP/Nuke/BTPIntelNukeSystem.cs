using Content.Server._RMC14.Announce;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._BTP.Nuke;

public sealed class BTPIntelNukeSystem : EntitySystem
{
    [Dependency] private readonly IntelSystem _intel = default!;
    [Dependency] private readonly SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;
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

        var query = EntityQueryEnumerator<BTPIntelNukeObjectiveComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RemCompDeferred<BTPIntelNukeObjectiveComponent>(uid);
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
            var objective = EnsureComp<BTPIntelNukeObjectiveComponent>(uid);
            ProcessObjective((uid, objective), (uid, tree), time);
            break;
        }
    }

    private void ProcessObjective(
        Entity<BTPIntelNukeObjectiveComponent> objective,
        Entity<IntelTechTreeComponent> tree,
        TimeSpan time)
    {
        var comp = objective.Comp;
        if (comp.LastUpdatedAt == default)
            comp.LastUpdatedAt = time;

        var elapsed = time - comp.LastUpdatedAt;
        comp.LastUpdatedAt = time;

        if (comp.Stage == BTPIntelNukeStage.WaitingForIntel &&
            tree.Comp.Tree.TotalEarned >= comp.RequiredIntelPoints)
        {
            comp.Stage = BTPIntelNukeStage.WaitingForTowers;
            Announce("Nuclear ordnance authorization fragments recovered from colony intelligence. Maintain control of two communication towers to begin decryption.");
            AnnounceXenos("Верховна Королева попереджає: інкубатори розпочали формування Винищувача-вуликів. Заважайте його формуванню: знищте або захопіть їхні енергетичні вежі.");
        }

        if (comp.Stage is not (BTPIntelNukeStage.WaitingForTowers or BTPIntelNukeStage.Decoding))
            return;

        var activeTowers = CountActiveMarineTowers();
        if (activeTowers < comp.RequiredTowers)
        {
            if (comp.Stage == BTPIntelNukeStage.Decoding)
            {
                comp.Stage = BTPIntelNukeStage.WaitingForTowers;
                var percent = GetDecodePercent(comp);
                Announce($"Communication tower control interrupted. Nuclear ordnance decryption paused at {percent}%.");
                AnnounceXenos($"Зв'язок інкубаторів порушено. Формування Винищувача-вуликів зупинено на {percent}%. Поверніть вежі під владу Вулика.");
            }

            AnnounceTowerStatusIfNeeded(comp, activeTowers, time);
            return;
        }

        if (comp.Stage == BTPIntelNukeStage.WaitingForTowers)
        {
            comp.Stage = BTPIntelNukeStage.Decoding;
            var minutes = Math.Max(1, (int) Math.Ceiling((comp.DecodeDuration - comp.DecodeProgress).TotalMinutes));
            Announce($"Two friendly communication towers linked. Nuclear ordnance decryption resumed. Estimated completion in {minutes} minutes.");
            AnnounceXenos($"Інкубатори поєднали дві енергетичні вежі. Формування Винищувача-вуликів відновлено. До завершення приблизно {FormatRemainingUkrainian(minutes * 60)}.");
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

    private void AnnounceTowerStatusIfNeeded(BTPIntelNukeObjectiveComponent comp, int activeTowers, TimeSpan time)
    {
        if (comp.LastReportedActiveTowers == activeTowers &&
            time < comp.NextTowerStatusAt)
        {
            return;
        }

        comp.LastReportedActiveTowers = activeTowers;
        comp.NextTowerStatusAt = time + TimeSpan.FromMinutes(1);
        Announce($"Nuclear ordnance decryption waiting for tower link: {activeTowers}/{comp.RequiredTowers} friendly communication towers active.");
        AnnounceXenos($"Формування Винищувача-вуликів очікує живлення через вежі: {activeTowers}/{comp.RequiredTowers} енергетичних веж під контролем ворога. Зірвіть їхній контроль.");
    }

    private void AnnounceDecodeProgress(BTPIntelNukeObjectiveComponent comp)
    {
        var remaining = (int) Math.Ceiling((comp.DecodeDuration - comp.DecodeProgress).TotalSeconds);
        foreach (var threshold in DecodeAnnouncementThresholds)
        {
            if (remaining > threshold ||
                !comp.DecodeAnnouncedAtSeconds.Add(threshold))
            {
                continue;
            }

            Announce($"Nuclear ordnance decryption in progress. Estimated completion in {FormatRemaining(threshold)} ({GetDecodePercent(comp)}%).");
            AnnounceXenos($"Формування Винищувача-вуликів триває. До завершення лишається {FormatRemainingUkrainian(threshold)} ({GetDecodePercent(comp)}%).");
            return;
        }
    }

    private void AuthorizeChargePurchase(
        Entity<BTPIntelNukeObjectiveComponent> objective,
        Entity<IntelTechTreeComponent> tree)
    {
        var comp = objective.Comp;
        if (comp.Stage == BTPIntelNukeStage.ChargeAuthorized)
            return;

        for (var tierIndex = 0; tierIndex < tree.Comp.Tree.Options.Count; tierIndex++)
        {
            var tier = tree.Comp.Tree.Options[tierIndex];
            for (var optionIndex = 0; optionIndex < tier.Count; optionIndex++)
            {
                var option = tier[optionIndex];
                if (!DeliversCharge(option, comp.ChargePrototype))
                    continue;

                tier[optionIndex] = option with { TimeLock = TimeSpan.Zero };
                comp.Stage = BTPIntelNukeStage.ChargeAuthorized;
                Dirty(tree);
                _intel.UpdateTree(tree);

                Announce("Nuclear ordnance decryption complete. Nuclear Fission Explosive purchase has been authorized at the asset authorization console.");
                AnnounceXenos("Формування Винищувача-вуликів завершено. Ворог може доставити заряд через свою логістику. Знищте боєголовку до активації.");
                return;
            }
        }

        Announce("Nuclear ordnance decryption complete, but no matching Nuclear Fission Explosive technology option was found for authorization.");
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

    private int GetDecodePercent(BTPIntelNukeObjectiveComponent comp)
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
            return $"{minutes} minute{(minutes == 1 ? "" : "s")}";
        }

        return $"{seconds} second{(seconds == 1 ? "" : "s")}";
    }

    private string FormatRemainingUkrainian(int seconds)
    {
        if (seconds >= 60)
        {
            var minutes = (int) Math.Ceiling(seconds / 60f);
            return $"{minutes} {GetUkrainianPlural(minutes, "хвилина", "хвилини", "хвилин")}";
        }

        return $"{seconds} {GetUkrainianPlural(seconds, "секунда", "секунди", "секунд")}";
    }

    private string GetUkrainianPlural(int value, string one, string few, string many)
    {
        var mod100 = value % 100;
        if (mod100 is >= 11 and <= 14)
            return many;

        return (value % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => few,
            _ => many,
        };
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
