using System.Numerics;
using Content.Server._Mriya.Rules.Contact;
using Content.Shared._Mriya.RoundEnd;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.RoundEnd;

public sealed class RoundEndAwardsSystem : EntitySystem
{
    private const int FactionNone = 0;
    private const int FactionMarine = 1;
    private const int FactionXeno = 2;

    private const int TreasurerMinimum = 1300;
    private const int ReaperMinimum = 100;
    private const int RunnerMinimum = 1000;
    private const int FriendlyFireMinimum = 100;
    private const int MedicMinimum = 150;
    private const int UnluckyMinimum = 3;
    private const int PacifistMaximumKills = 5;
    private const int TankMinimum = 1000;
    private const int RiggerMinimum = 10;
    private const int GardenerMinimum = 55;
    private const int ArchitectMinimum = 60;
    private const int LeaperMinimum = 25;

    private static readonly TimeSpan DistanceSampleInterval = TimeSpan.FromSeconds(1);
    private const float MaxDistancePerSample = 30f;

    [Dependency] private readonly ContactEconomySystem _economy = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<NetUserId, RoundStats> _stats = new();
    private readonly Dictionary<EntityUid, float> _damageReceivedByCharacter = new();
    private readonly Dictionary<EntityUid, NetUserId> _lastAttacker = new();
    private readonly Dictionary<EntityUid, (EntityUid Parent, Vector2 Position)> _lastPositions = new();

    private TimeSpan _nextDistanceSample;
    private NetUserId? _firstBlood;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<RoundEndAwardsEvent>(OnRoundEndAwards);

        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);

        SubscribeLocalEvent<PlayerPlantedWeedsEvent>(OnPlantedWeeds);
        SubscribeLocalEvent<PlayerBuiltBarricadeEvent>(OnBuiltBarricades);
        SubscribeLocalEvent<XenoLeapingComponent, XenoLeapHitEvent>(OnLeapHit);
    }

    private RoundStats GetStats(NetUserId user)
    {
        if (_stats.TryGetValue(user, out var stats))
            return stats;

        stats = new RoundStats();
        _stats[user] = stats;
        return stats;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _stats.Clear();
        _damageReceivedByCharacter.Clear();
        _lastAttacker.Clear();
        _lastPositions.Clear();
        _nextDistanceSample = default;
        _firstBlood = null;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var stats = GetStats(ev.Player.UserId);
        stats.Faction = GetFaction(ev.Mob);
        stats.PlayedAsCombatant |= stats.Faction != FactionNone;
        _lastPositions.Remove(ev.Mob);
    }

    #region Damage / kills / deaths

    private void OnDamageChanged(EntityUid uid, DamageableComponent component, DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta)
            return;

        var total = (float) delta.GetTotal();
        if (total == 0f)
            return;

        var hasVictimUser = TryComp(uid, out ActorComponent? victimActor);
        var hasAttacker = TryGetUser(args.Origin, out var attacker, out var attackerFaction);
        if (!hasVictimUser && !hasAttacker)
            return;

        var victimUser = hasVictimUser ? victimActor!.PlayerSession.UserId : (NetUserId?) null;
        var victimFaction = GetFaction(uid);

        if (total > 0f)
        {
            if (victimUser is { } victim)
            {
                var victimStats = GetStats(victim);
                victimStats.PlayedAsCombatant |= victimFaction != FactionNone;

                var received = _damageReceivedByCharacter.GetValueOrDefault(uid) + total;
                _damageReceivedByCharacter[uid] = received;
                if (received > victimStats.MaxDamageReceived)
                    victimStats.MaxDamageReceived = received;
            }

            if (!hasAttacker)
                return;

            var attackerStats = GetStats(attacker);
            attackerStats.PlayedAsCombatant |= attackerFaction != FactionNone;

            if (attackerFaction == FactionNone || victimFaction == FactionNone)
                return;

            if (attackerFaction != victimFaction)
            {
                attackerStats.EnemyDamage += total;
                _lastAttacker[uid] = attacker;
                return;
            }

            if (args.Origin == uid || attacker == victimUser)
                return;

            attackerStats.FriendlyFireDamage += total;
            return;
        }

        var healed = -total;
        if (args.Origin == null ||
            args.Origin == uid ||
            victimFaction == FactionNone ||
            !hasAttacker ||
            attacker == victimUser)
        {
            return;
        }

        GetStats(attacker).Healing += healed;
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState >= MobState.Dead)
            return;

        var uid = args.Target;

        if (TryComp(uid, out ActorComponent? actor))
        {
            GetStats(actor.PlayerSession.UserId).Deaths++;
        }

        _lastAttacker.Remove(uid, out var lastAttacker);

        NetUserId? killer = null;
        var killerFaction = FactionNone;
        if (TryGetUser(args.Origin, out var originUser, out var originFaction))
        {
            killer = originUser;
            killerFaction = originFaction;
        }
        else if (lastAttacker is { } previous)
        {
            killer = previous;
            killerFaction = GetStats(previous).Faction;
        }

        if (killer is not { } killerUser)
            return;

        var victimFaction = GetFaction(uid);
        if (victimFaction == FactionNone || killerFaction == FactionNone || killerFaction == victimFaction)
            return;

        GetStats(killerUser).Kills++;

        _firstBlood ??= killerUser;
    }

    private void OnPlantedWeeds(PlayerPlantedWeedsEvent args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        GetStats(actor.PlayerSession.UserId).WeedsPlanted += args.Amount;
    }

    private void OnBuiltBarricades(PlayerBuiltBarricadeEvent args)
    {
        if (!TryComp(args.User, out ActorComponent? actor))
            return;

        GetStats(actor.PlayerSession.UserId).BarricadesBuilt += args.Amount;
    }

    private void OnLeapHit(Entity<XenoLeapingComponent> xeno, ref XenoLeapHitEvent args)
    {
        if (!HasComp<XenoLeapComponent>(xeno) ||
            !HasComp<MarineComponent>(args.Hit) ||
            !HasComp<LeapIncapacitatedComponent>(args.Hit))
        {
            return;
        }

        if (!TryComp(xeno, out ActorComponent? actor))
            return;

        GetStats(actor.PlayerSession.UserId).LeapHits++;
    }

    #endregion

    #region Distance

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextDistanceSample)
            return;

        _nextDistanceSample = now + DistanceSampleInterval;

        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out var actor, out var mobState, out var xform))
        {
            if (_mobState.IsDead(uid, mobState))
            {
                _lastPositions.Remove(uid);
                continue;
            }

            var position = xform.LocalPosition;
            var parent = xform.ParentUid;

            if (_lastPositions.TryGetValue(uid, out var last) && last.Parent == parent)
            {
                var distance = (position - last.Position).Length();
                if (distance > 0.01f && distance < MaxDistancePerSample)
                    GetStats(actor.PlayerSession.UserId).Distance += distance;
            }

            _lastPositions[uid] = (parent, position);
        }
    }

    #endregion

    #region Awards

    private void OnRoundEndAwards(RoundEndAwardsEvent ev)
    {
        var participants = new HashSet<NetUserId>();
        foreach (var player in ev.Players)
        {
            if (player.PlayerGuid is not { } userId)
                continue;

            participants.Add(userId);

            var stats = GetStats(userId);
            stats.MoneyEarned = _economy.GetTotalEarned(userId);
            stats.AnchorsActivated = _economy.GetTerminalsActivated(userId);
        }

        AddMostAward(ev, participants,
            "rmc-round-end-award-treasurer-title",
            "rmc-round-end-award-treasurer-description",
            stats => stats.MoneyEarned,
            TreasurerMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-reaper-title",
            "rmc-round-end-award-reaper-description",
            stats => stats.Kills,
            ReaperMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-runner-title",
            "rmc-round-end-award-runner-description",
            stats => (int) MathF.Round(stats.Distance),
            RunnerMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-friendly-fire-title",
            "rmc-round-end-award-friendly-fire-description",
            stats => (int) MathF.Round(stats.FriendlyFireDamage),
            FriendlyFireMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-medic-title",
            "rmc-round-end-award-medic-description",
            stats => (int) MathF.Round(stats.Healing),
            MedicMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-unlucky-title",
            "rmc-round-end-award-unlucky-description",
            stats => stats.Deaths,
            UnluckyMinimum);

        AddPacifistAward(ev, participants);

        AddMostAward(ev, participants,
            "rmc-round-end-award-tank-title",
            "rmc-round-end-award-tank-description",
            stats => (int) MathF.Round(stats.MaxDamageReceived),
            TankMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-rigger-title",
            "rmc-round-end-award-rigger-description",
            stats => stats.AnchorsActivated,
            RiggerMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-gardener-title",
            "rmc-round-end-award-gardener-description",
            stats => stats.WeedsPlanted,
            GardenerMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-architect-title",
            "rmc-round-end-award-architect-description",
            stats => stats.BarricadesBuilt,
            ArchitectMinimum);

        AddMostAward(ev, participants,
            "rmc-round-end-award-leaper-title",
            "rmc-round-end-award-leaper-description",
            stats => stats.LeapHits,
            LeaperMinimum);

        AddFirstBloodAward(ev, participants);
    }

    private void AddFirstBloodAward(RoundEndAwardsEvent ev, HashSet<NetUserId> participants)
    {
        if (_firstBlood is not { } winner || !participants.Contains(winner))
            return;

        AddAward(ev,
            "rmc-round-end-award-first-blood-title",
            "rmc-round-end-award-first-blood-description",
            winner,
            1);
    }

    private void AddMostAward(RoundEndAwardsEvent ev,
        HashSet<NetUserId> participants,
        string titleKey,
        string descriptionKey,
        Func<RoundStats, int> valueSelector,
        int minimum)
    {
        NetUserId? winner = null;
        var best = 0;

        foreach (var (user, stats) in _stats)
        {
            if (!participants.Contains(user))
                continue;

            var value = valueSelector(stats);
            if (value < minimum)
                continue;

            if (winner == null ||
                value > best ||
                value == best && ComparePlayers(ev, user, winner.Value) < 0)
            {
                winner = user;
                best = value;
            }
        }

        if (winner is not { } winnerUser)
            return;

        AddAward(ev, titleKey, descriptionKey, winnerUser, best);
    }

    private void AddPacifistAward(RoundEndAwardsEvent ev, HashSet<NetUserId> participants)
    {
        NetUserId? winner = null;
        var bestKills = PacifistMaximumKills + 1;
        var bestDamage = -1f;

        foreach (var (user, stats) in _stats)
        {
            if (!participants.Contains(user) ||
                !stats.PlayedAsCombatant ||
                stats.Kills > PacifistMaximumKills ||
                stats.EnemyDamage <= 0f && stats.Healing <= 0f && stats.Distance < 100f)
            {
                continue;
            }

            var damage = stats.EnemyDamage + stats.Healing;
            if (winner == null ||
                stats.Kills < bestKills ||
                stats.Kills == bestKills && damage > bestDamage)
            {
                winner = user;
                bestKills = stats.Kills;
                bestDamage = damage;
            }
        }

        if (winner is not { } winnerUser)
            return;

        AddAward(ev,
            "rmc-round-end-award-pacifist-title",
            "rmc-round-end-award-pacifist-description",
            winnerUser,
            bestKills);
    }

    private void AddAward(RoundEndAwardsEvent ev, string titleKey, string descriptionKey, NetUserId user, int value)
    {
        var player = FindPlayer(ev, user);
        var name = player?.PlayerICName;
        if (string.IsNullOrEmpty(name))
            name = player?.PlayerOOCName;
        if (string.IsNullOrEmpty(name))
            name = Loc.GetString("rmc-round-end-award-unknown-player");

        ev.Awards.Add(new RoundEndMessageEvent.RoundEndAwardInfo
        {
            Title = Loc.GetString(titleKey),
            Description = Loc.GetString(descriptionKey, ("name", name), ("value", value)),
            PlayerNetEntity = player?.PlayerNetEntity,
        });
    }

    private int ComparePlayers(RoundEndAwardsEvent ev, NetUserId left, NetUserId right)
    {
        return string.CompareOrdinal(GetPlayerName(ev, left), GetPlayerName(ev, right));
    }

    private string GetPlayerName(RoundEndAwardsEvent ev, NetUserId user)
    {
        var player = FindPlayer(ev, user);
        return player?.PlayerICName ?? player?.PlayerOOCName ?? user.ToString();
    }

    private static RoundEndMessageEvent.RoundEndPlayerInfo? FindPlayer(RoundEndAwardsEvent ev, NetUserId user)
    {
        foreach (var player in ev.Players)
        {
            if (player.PlayerGuid == user)
                return player;
        }

        return null;
    }

    #endregion

    #region Helpers

    private int GetFaction(EntityUid uid)
    {
        if (HasComp<MarineComponent>(uid))
            return FactionMarine;
        if (HasComp<XenoComponent>(uid))
            return FactionXeno;
        return FactionNone;
    }

    private bool TryGetUser(EntityUid? origin, out NetUserId user, out int faction)
    {
        user = default;
        faction = FactionNone;

        if (origin is not { } originUid)
            return false;

        var current = originUid;
        for (var i = 0; i < 5; i++)
        {
            if (TryComp(current, out ActorComponent? actor))
            {
                user = actor.PlayerSession.UserId;
                faction = GetFaction(current);
                return true;
            }

            if (_economy.TryGetPlayerCredit(current, out user, out var bySentry))
            {
                faction = bySentry ? FactionMarine : GetStats(user).Faction;
                return true;
            }

            if (GetDamageSourceOwner(current) is not { } next || next == current)
                break;

            current = next;
        }

        return false;
    }

    private EntityUid? GetDamageSourceOwner(EntityUid source)
    {
        if (TryComp(source, out ProjectileComponent? projectile) && projectile.Shooter is { } shooter)
            return shooter;

        if (TryComp(source, out ThrownItemComponent? thrown) && thrown.Thrower is { } thrower)
            return thrower;

        return null;
    }

    private sealed class RoundStats
    {
        public int Faction;
        public bool PlayedAsCombatant;
        public int Kills;
        public int Deaths;
        public float EnemyDamage;
        public float FriendlyFireDamage;
        public float Healing;
        public float MaxDamageReceived;
        public float Distance;
        public int MoneyEarned;
        public int AnchorsActivated;
        public int WeedsPlanted;
        public int BarricadesBuilt;
        public int LeapHits;
    }

    #endregion
}
