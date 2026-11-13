using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Marines;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared._RMC14.Gibbing;
using Content.Shared.Mobs.Systems;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Є-бали: per-player balance for the Contact store.
/// Keyed by NetUserId so it survives death and respawn on the ship.
/// Kills: killer gets the full reward (per caste), everyone else who damaged the xeno gets an assist share.
/// Sentries a marine carried and deployed credit that marine.
/// Fire tiles are credited to whoever fired nearest to them when they appeared (flamer marine / owned flamer sentry);
/// their damage has no origin, so it's matched by the fire under the mob, and burning deaths go to the last credited hit.
/// The client gets its balance on every change, plus a snapshot when attached (via ContactRuleSystem).
/// </summary>
public sealed class ContactEconomySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <summary>A fire counts as yours if you fired within this long before it appeared.</summary>
    private static readonly TimeSpan ShotWindow = TimeSpan.FromSeconds(1);

    /// <summary>How far from the shooter a new fire can be and still count as theirs.</summary>
    private const float FireCreditRange = 8f;

    private readonly Dictionary<NetUserId, int> _balances = new();
    private readonly Dictionary<NetUserId, int> _earned = new();
    private readonly Dictionary<NetUserId, int> _terminalsActivated = new();
    private readonly List<EntityUid> _newFires = new();
    private readonly HashSet<Entity<TileFireComponent>> _fireTiles = new();

    /// <summary>The first flamer tile is right next to the shooter.</summary>
    private const float FlamerHolderRange = 3f;

    private readonly List<EntityUid> _newChains = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<ContactWaveMobComponent, DamageChangedEvent>(OnWaveMobDamageChanged);
        SubscribeLocalEvent<ContactWaveMobComponent, MobStateChangedEvent>(OnWaveMobStateChanged,
            before: [typeof(RMCGibSystem)]);
        SubscribeLocalEvent<ContactPlayerComponent, DamageChangedEvent>(OnPlayerDamageChanged);
        SubscribeLocalEvent<ContactPlayerComponent, MobStateChangedEvent>(OnPlayerMobStateChanged,
            before: [typeof(RMCGibSystem)]);
        SubscribeLocalEvent<ContactPlayerComponent, DidEquipHandEvent>(OnPlayerEquipHand);
    }

    private ContactEconomyComponent? GetConfig()
    {
        var query = EntityQueryEnumerator<ContactEconomyComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var config, out _))
        {
            return config;
        }

        return null;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _balances.Clear();
        _earned.Clear();
        _terminalsActivated.Clear();
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (GetConfig() is not { } config || !HasComp<MarineComponent>(ev.Mob))
            return;

        EnsureComp<ContactPlayerComponent>(ev.Mob);
        _balances.TryAdd(ev.Player.UserId, config.StartingPoints);
        SendBalanceTo(ev.Player);
    }

    /// <summary>
    /// New fire tiles get credited every tick: RMC's collide system damages with a new fire right after it spawns.
    /// </summary>
    public override void Update(float frameTime)
    {
        if (GetConfig() == null)
            return;

        // flamer shots: one chain entity per shot, spawned the tick the flamer fired
        _newChains.Clear();
        var chains = EntityQueryEnumerator<RMCFlamerChainComponent>();
        while (chains.MoveNext(out var uid, out var chain))
        {
            if (chain.Tiles.Count > 0 && !HasComp<ContactFireCreditComponent>(uid))
                _newChains.Add(uid);
        }

        foreach (var chain in _newChains)
        {
            StampFlamerChain(chain);
        }

        _newFires.Clear();
        var fires = EntityQueryEnumerator<TileFireComponent>();
        while (fires.MoveNext(out var uid, out _))
        {
            if (!TryComp(uid, out ContactFireCreditComponent? fireCredit))
            {
                _newFires.Add(uid);
                continue;
            }

            // credited too early (before its flamer chain was): retry while the chain still exists
            if (fireCredit.Owner == null &&
                TryComp(uid, out DamageOnCollideComponent? collide) &&
                collide.Chain is { } collideChain &&
                TryGetFlamerChainCredit(collideChain, out var chainOwner))
            {
                fireCredit.Owner = chainOwner;
            }
        }
    }

    #region Public API

    public bool TryGetUser(EntityUid player, out NetUserId user)
    {
        user = default;
        if (!TryComp(player, out ActorComponent? actor))
            return false;

        user = actor.PlayerSession.UserId;
        return true;
    }

    public int GetBalance(NetUserId user)
    {
        return _balances.GetValueOrDefault(user);
    }

    public int GetTotalEarned(NetUserId user)
    {
        return _earned.GetValueOrDefault(user);
    }

    public int GetTerminalsActivated(NetUserId user)
    {
        return _terminalsActivated.GetValueOrDefault(user);
    }

    public int GetBalance(EntityUid player)
    {
        return TryGetUser(player, out var user) ? GetBalance(user) : 0;
    }

    public void AddPoints(EntityUid player, int amount, LocId? reason = null)
    {
        if (TryGetUser(player, out var user))
            AddPoints(user, amount, reason);
    }

    /// <summary>
    /// By user, so dead / ghosted / respawned players still get their kill and assist points.
    /// </summary>
    public void AddPoints(NetUserId user, int amount, LocId? reason = null)
    {
        if (amount <= 0)
            return;

        _balances[user] = GetBalance(user) + amount;
        _earned[user] = _earned.GetValueOrDefault(user) + amount;

        if (!_player.TryGetSessionById(user, out var session))
            return;

        SendBalanceTo(session);

        if (session.AttachedEntity is not { } attached)
            return;

        var msg = reason is { } r
            ? Loc.GetString("mriya-contact-points-gain-reason", ("amount", amount), ("reason", Loc.GetString(r)))
            : Loc.GetString("mriya-contact-points-gain", ("amount", amount));
        _popup.PopupEntity(msg, attached, attached, PopupType.Small);
    }

    public bool TrySpend(EntityUid player, int amount)
    {
        if (!TryGetUser(player, out var user))
            return false;

        var balance = GetBalance(user);
        if (balance < amount)
            return false;

        _balances[user] = balance - amount;
        SendBalance(player);
        return true;
    }

    /// <summary>
    /// Called by ContactTerminalSystem when a player finishes activating a terminal.
    /// </summary>
    public void RewardTerminal(EntityUid player)
    {
        if (TryGetUser(player, out var user))
        {
            _terminalsActivated[user] = _terminalsActivated.GetValueOrDefault(user) + 1;
        }

        if (GetConfig() is { } config)
            AddPoints(player, config.TerminalReward, "mriya-contact-points-reason-terminal");
    }

    /// <summary>
    /// Snapshot for one player (spawning, ghosting, reconnecting).
    /// </summary>
    public void SendBalanceTo(ICommonSession session)
    {
        RaiseNetworkEvent(new ContactBalanceEvent(GetBalance(session.UserId)), session);
    }

    #endregion

    private void SendBalance(EntityUid player)
    {
        if (TryComp(player, out ActorComponent? actor))
            SendBalanceTo(actor.PlayerSession);
    }

    #region Kills

    /// <summary>
    /// Picking up a sentry makes you its owner: whoever carried it last deployed it.
    /// Dropship / map sentries are never carried, so they credit nobody.
    /// </summary>
    private void OnPlayerEquipHand(Entity<ContactPlayerComponent> ent, ref DidEquipHandEvent args)
    {
        if (GetConfig() is not { } config ||
            !_whitelist.IsWhitelistPass(config.SentryWhitelist, args.Equipped) ||
            !TryGetUser(ent, out var user))
        {
            return;
        }

        EnsureComp<ContactSentryOwnerComponent>(args.Equipped).Owner = user;
    }

    public bool TryGetPlayerCredit(EntityUid? origin, out NetUserId user, out bool bySentry)
    {
        return TryGetCredit(origin, out user, out bySentry);
    }

    /// <summary>
    /// Player, owned sentry or credited fire behind a hit.
    /// </summary>
    private bool TryGetCredit(EntityUid? origin, out NetUserId user, out bool bySentry)
    {
        user = default;
        bySentry = false;

        if (origin is not { } o)
            return false;

        if (TryComp(o, out ContactSentryOwnerComponent? sentry))
        {
            user = sentry.Owner;
            bySentry = true;
            return true;
        }

        if (TryComp(o, out ContactFireCreditComponent? fire))
        {
            if (fire.Owner is not { } fireOwner)
                return false;

            user = fireOwner;
            bySentry = fire.BySentry;
            return true;
        }

        return HasComp<ContactPlayerComponent>(o) && TryGetUser(o, out user);
    }

    private void OnWaveMobDamageChanged(Entity<ContactWaveMobComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || _mobState.IsDead(ent))
            return;

        // fire tiles have no origin, burning has the mob itself as origin
        var noOrigin = args.Origin == null || args.Origin == ent.Owner;

        // standing in someone's fire? (burning after leaving it is covered by LastHit)
        if (!TryGetCredit(args.Origin, out var user, out var bySentry) &&
            (!noOrigin || !TryGetFireUnder(ent, out user, out bySentry)))
        {
            return;
        }

        var credit = EnsureComp<ContactKillCreditComponent>(ent);
        credit.Damagers.Add(user);
        credit.LastHit = user;
        credit.LastHitBySentry = bySentry;
    }

    private void OnWaveMobStateChanged(Entity<ContactWaveMobComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || GetConfig() is not { } config)
            return;

        TryComp(ent, out ContactKillCreditComponent? credit);

        // the hit that killed it; no origin / self (fire, burning): the fire it's standing in, else the last credited hit
        NetUserId? killer = null;
        var bySentry = false;
        var noOrigin = args.Origin == null || args.Origin == ent.Owner;
        if (TryGetCredit(args.Origin, out var user, out var sentry) ||
            noOrigin && TryGetFireUnder(ent, out user, out sentry))
        {
            killer = user;
            bySentry = sentry;
        }
        else if (credit?.LastHit is { } last)
        {
            killer = last;
            bySentry = credit.LastHitBySentry;
        }

        var reward = GetKillReward(ent, config);
        if (killer is { } k)
        {
            AddPoints(k, reward, bySentry
                ? "mriya-contact-points-reason-sentry-kill"
                : "mriya-contact-points-reason-kill");
        }

        if (credit == null)
            return;

        var assist = (int) MathF.Round(reward * config.AssistFraction);
        foreach (var damager in credit.Damagers)
        {
            if (damager != killer)
                AddPoints(damager, assist, "mriya-contact-points-reason-assist");
        }

        credit.Damagers.Clear();
    }

    /// <summary>
    /// Wave entry reward > per-caste reward > default.
    /// </summary>
    private static int GetKillReward(Entity<ContactWaveMobComponent> ent, ContactEconomyComponent config)
    {
        if (ent.Comp.Reward is { } waveReward)
            return waveReward;

        return config.KillRewards.TryGetValue(ent.Comp.Proto, out var casteReward)
            ? casteReward
            : config.KillReward;
    }

    #endregion

    #region Fire

    private ContactFireCreditComponent StampFire(EntityUid fire, TimeSpan now)
    {
        var credit = EnsureComp<ContactFireCreditComponent>(fire);

        // flamer tile: same collide chain as the shot that spawned it
        if (TryComp(fire, out DamageOnCollideComponent? collide) &&
            collide.Chain is { } collideChain &&
            TryGetFlamerChainCredit(collideChain, out var chainOwner))
        {
            credit.Owner = chainOwner;
            return credit;
        }

        // anything else (grenades, sentry fire...): whoever fired nearest just now
        if (FindFireStarter(_transform.GetMapCoordinates(fire), now, out var owner, out var bySentry))
        {
            credit.Owner = owner;
            credit.BySentry = bySentry;
        }

        return credit;
    }

    /// <summary>
    /// A flamer shot: the marine holding a flamer nearest to its first remaining tile.
    /// </summary>
    private ContactFireCreditComponent StampFlamerChain(EntityUid chain)
    {
        var credit = EnsureComp<ContactFireCreditComponent>(chain);
        var comp = Comp<RMCFlamerChainComponent>(chain);
        if (comp.Tiles.Count == 0)
            return credit;

        var first = comp.Tiles[0].Coordinates;
        var best = FlamerHolderRange;
        var players = EntityQueryEnumerator<ContactPlayerComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out var xform))
        {
            var p = _transform.GetMapCoordinates(uid, xform);
            if (p.MapId != first.MapId)
                continue;

            var dist = (p.Position - first.Position).Length();
            if (dist > best || !HoldsFlamer(uid) || !TryGetUser(uid, out var user))
                continue;

            best = dist;
            credit.Owner = user;
        }

        return credit;
    }

    private bool TryGetFlamerChainCredit(EntityUid collideChain, out NetUserId owner)
    {
        owner = default;
        var chains = EntityQueryEnumerator<RMCFlamerChainComponent>();
        while (chains.MoveNext(out var uid, out var chain))
        {
            if (chain.Chain != collideChain)
                continue;

            // shot this tick: RMC can spawn and damage with the first tile before our Update credits the chain
            var credit = CompOrNull<ContactFireCreditComponent>(uid) ?? StampFlamerChain(uid);
            if (credit.Owner is not { } o)
                return false;

            owner = o;
            return true;
        }

        return false;
    }

    private bool HoldsFlamer(EntityUid player)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (HasComp<RMCFlamerAmmoProviderComponent>(held))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Fire tile damage has no origin (SharedOnCollideSystem), so look for a credited fire under the mob.
    /// A fire Update hasn't seen yet is credited right here.
    /// </summary>
    private bool TryGetFireUnder(EntityUid mob, out NetUserId user, out bool bySentry)
    {
        user = default;
        bySentry = false;

        _fireTiles.Clear();
        _lookup.GetEntitiesInRange(Transform(mob).Coordinates, 0.6f, _fireTiles, LookupFlags.Static | LookupFlags.Sensors);

        var now = _timing.CurTime;
        foreach (var fire in _fireTiles)
        {
            var credit = CompOrNull<ContactFireCreditComponent>(fire) ?? StampFire(fire, now);
            if (credit.Owner is not { } owner)
                continue;

            user = owner;
            bySentry = credit.BySentry;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Nearest marine (held gun) or owned sentry that fired within ShotWindow, up to FireCreditRange away.
    /// </summary>
    private bool FindFireStarter(MapCoordinates pos, TimeSpan now, out NetUserId owner, out bool bySentry)
    {
        owner = default;
        bySentry = false;
        var found = false;
        var best = FireCreditRange;

        var players = EntityQueryEnumerator<ContactPlayerComponent, TransformComponent>();
        while (players.MoveNext(out var uid, out _, out var xform))
        {
            if (!ShotRecently(uid, now) || !TryGetUser(uid, out var user))
                continue;

            var p = _transform.GetMapCoordinates(uid, xform);
            if (p.MapId != pos.MapId)
                continue;

            var dist = (p.Position - pos.Position).Length();
            if (dist > best)
                continue;

            best = dist;
            owner = user;
            bySentry = false;
            found = true;
        }

        var sentries = EntityQueryEnumerator<ContactSentryOwnerComponent, GunComponent, TransformComponent>();
        while (sentries.MoveNext(out var uid, out var sentry, out var gun, out var xform))
        {
            if (gun.NextFire < now - ShotWindow)
                continue;

            var p = _transform.GetMapCoordinates(uid, xform);
            if (p.MapId != pos.MapId)
                continue;

            var dist = (p.Position - pos.Position).Length();
            if (dist > best)
                continue;

            best = dist;
            owner = sentry.Owner;
            bySentry = true;
            found = true;
        }

        return found;
    }

    private bool ShotRecently(EntityUid player, TimeSpan now)
    {
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (TryComp(held, out GunComponent? gun) && gun.NextFire >= now - ShotWindow)
                return true;
        }

        return false;
    }

    #endregion

    #region Heal / revive

    private void OnPlayerDamageChanged(Entity<ContactPlayerComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased ||
            args.DamageDelta is not { } delta ||
            args.Origin is not { } healer ||
            healer == ent.Owner ||
            !HasComp<ContactPlayerComponent>(healer) ||
            GetConfig() is not { } config ||
            config.HealStep <= 0)
        {
            return;
        }

        var healed = -(float) delta.GetTotal();
        if (healed <= 0)
            return;

        var pending = ent.Comp.PendingHeals.GetValueOrDefault(healer) + healed;
        var steps = (int) (pending / config.HealStep);
        ent.Comp.PendingHeals[healer] = pending - steps * config.HealStep;

        if (steps > 0)
            AddPoints(healer, steps * config.HealReward, "mriya-contact-points-reason-heal");
    }

    private void OnPlayerMobStateChanged(Entity<ContactPlayerComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.OldMobState != MobState.Dead ||
            args.NewMobState == MobState.Dead ||
            args.Origin is not { } reviver ||
            reviver == ent.Owner ||
            !HasComp<ContactPlayerComponent>(reviver) ||
            GetConfig() is not { } config)
        {
            return;
        }

        AddPoints(reviver, config.ReviveReward, "mriya-contact-points-reason-revive");
    }

    #endregion
}