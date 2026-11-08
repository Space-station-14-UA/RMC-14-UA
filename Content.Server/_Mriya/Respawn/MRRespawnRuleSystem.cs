using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Shared._Mriya.Respawn;
using Content.Shared.Actions;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Mriya.Respawn;

/// <summary>
/// Gives ghosts a "return to lobby" action while an MRRespawnRule is active, with an optional per-player limit and delay.
/// Not tied to any gamemode.
/// </summary>
public sealed class MRRespawnRuleSystem : GameRuleSystem<MRRespawnRuleComponent>
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    private readonly List<ICommonSession> _toRespawn = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<MRRespawnGhostComponent, MRRespawnActionEvent>(OnRespawnAction);
    }

    protected override void Started(EntityUid uid, MRRespawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        // started mid-round: ghosts that already exist get the action too
        var ghosts = EntityQueryEnumerator<GhostComponent, ActorComponent>();
        while (ghosts.MoveNext(out var ghost, out _, out _))
        {
            GiveAction(ghost, component);
        }
    }

    private Entity<MRRespawnRuleComponent>? GetActive()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out _))
        {
            return (uid, comp);
        }

        return null;
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!HasComp<GhostComponent>(ev.Entity) || GetActive() is not { } rule)
            return;

        GiveAction(ev.Entity, rule.Comp);
    }

    private void GiveAction(EntityUid ghost, MRRespawnRuleComponent rule)
    {
        var comp = EnsureComp<MRRespawnGhostComponent>(ghost);
        comp.GhostedAt = Timing.CurTime;

        if (comp.Action == null)
            _actions.AddAction(ghost, ref comp.Action, rule.Action);
    }

    private void OnRespawnAction(Entity<MRRespawnGhostComponent> ent, ref MRRespawnActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (GetActive() is not { } rule || !TryComp(ent, out ActorComponent? actor))
        {
            _popup.PopupEntity(Loc.GetString("mriya-respawn-unavailable"), ent, ent);
            return;
        }

        var session = actor.PlayerSession;

        var wait = ent.Comp.GhostedAt + rule.Comp.Delay - Timing.CurTime;
        if (wait > TimeSpan.Zero)
        {
            _popup.PopupEntity(Loc.GetString("mriya-respawn-wait", ("seconds", (int) Math.Ceiling(wait.TotalSeconds))), ent, ent);
            return;
        }

        var used = rule.Comp.Used.GetValueOrDefault(session.UserId);
        if (rule.Comp.MaxRespawns >= 0 && used >= rule.Comp.MaxRespawns)
        {
            _popup.PopupEntity(Loc.GetString("mriya-respawn-none-left"), ent, ent);
            return;
        }

        rule.Comp.Used[session.UserId] = used + 1;

        var message = rule.Comp.MaxRespawns < 0
            ? Loc.GetString("mriya-respawn-done-unlimited")
            : Loc.GetString("mriya-respawn-done", ("left", rule.Comp.MaxRespawns - used - 1));
        _chat.DispatchServerMessage(session, message);

        _toRespawn.Add(session);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_toRespawn.Count == 0)
            return;

        foreach (var session in _toRespawn)
        {
            GameTicker.Respawn(session);
        }

        _toRespawn.Clear();
    }

    #region Admin

    public bool SetLimit(int limit)
    {
        if (GetActive() is not { } rule)
            return false;

        rule.Comp.MaxRespawns = limit;
        return true;
    }

    public bool SetDelay(TimeSpan delay)
    {
        if (GetActive() is not { } rule)
            return false;

        rule.Comp.Delay = delay;
        return true;
    }

    /// <summary>
    /// Resets used respawns for one player, or everyone if null.
    /// </summary>
    public bool Reset(NetUserId? user)
    {
        if (GetActive() is not { } rule)
            return false;

        if (user is { } id)
            rule.Comp.Used.Remove(id);
        else
            rule.Comp.Used.Clear();

        return true;
    }

    public bool TryGetStatus(out int limit, out TimeSpan delay)
    {
        limit = 0;
        delay = TimeSpan.Zero;
        if (GetActive() is not { } rule)
            return false;

        limit = rule.Comp.MaxRespawns;
        delay = rule.Comp.Delay;
        return true;
    }

    #endregion
}