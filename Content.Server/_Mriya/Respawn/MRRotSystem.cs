using Content.Server.GameTicking.Rules;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.GameTicking.Components;
using Content.Shared.Whitelist;

namespace Content.Server._Mriya.Respawn;

/// <summary>
/// Applies the rule's RotTime as RMC14 unrevivable delay. New spawns are picked up within CheckEvery.
/// </summary>
public sealed class MRRotRuleSystem : GameRuleSystem<MRRotRuleComponent>
{
    [Dependency] private readonly RMCUnrevivableSystem _unrevivable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    protected override void ActiveTick(EntityUid uid, MRRotRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, comp, gameRule, frameTime);

        var now = Timing.CurTime;
        if (now < comp.NextCheck)
            return;

        comp.NextCheck = now + comp.CheckEvery;

        var query = EntityQueryEnumerator<RMCRevivableComponent>();
        while (query.MoveNext(out var mob, out var revivable))
        {
            if (revivable.UnrevivableDelay == comp.RotTime ||
                !_whitelist.IsWhitelistPass(comp.Whitelist, mob))
            {
                continue;
            }

            _unrevivable.SetUnrevivableDelay((mob, revivable), comp.RotTime);
        }
    }
}