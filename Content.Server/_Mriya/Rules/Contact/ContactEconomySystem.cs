using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Marines;
using Content.Shared.Damage;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Є-бали: per-player balance for the Contact store.
/// Keyed by NetUserId so it survives death and respawn on the ship.
/// The client gets its balance on every change, plus a snapshot when attached (via ContactRuleSystem).
/// </summary>
public sealed class ContactEconomySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly Dictionary<NetUserId, int> _balances = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        SubscribeLocalEvent<ContactWaveMobComponent, MobStateChangedEvent>(OnWaveMobStateChanged);
        SubscribeLocalEvent<ContactPlayerComponent, DamageChangedEvent>(OnPlayerDamageChanged);
        SubscribeLocalEvent<ContactPlayerComponent, MobStateChangedEvent>(OnPlayerMobStateChanged);
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
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (GetConfig() is not { } config || !HasComp<MarineComponent>(ev.Mob))
            return;

        EnsureComp<ContactPlayerComponent>(ev.Mob);
        _balances.TryAdd(ev.Player.UserId, config.StartingPoints);
        SendBalanceTo(ev.Player);
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

    public int GetBalance(EntityUid player)
    {
        return TryGetUser(player, out var user) ? GetBalance(user) : 0;
    }

    public void AddPoints(EntityUid player, int amount, LocId? reason = null)
    {
        if (amount <= 0 || !TryGetUser(player, out var user))
            return;

        _balances[user] = GetBalance(user) + amount;
        SendBalance(player);

        var msg = reason is { } r
            ? Loc.GetString("mriya-contact-points-gain-reason", ("amount", amount), ("reason", Loc.GetString(r)))
            : Loc.GetString("mriya-contact-points-gain", ("amount", amount));
        _popup.PopupEntity(msg, player, player, PopupType.Small);
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

    private void OnWaveMobStateChanged(Entity<ContactWaveMobComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            args.Origin is not { } killer ||
            !HasComp<ContactPlayerComponent>(killer) ||
            GetConfig() is not { } config)
        {
            return;
        }

        var reward = ent.Comp.Reward ?? config.KillReward;
        AddPoints(killer, reward, "mriya-contact-points-reason-kill");
    }

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
}