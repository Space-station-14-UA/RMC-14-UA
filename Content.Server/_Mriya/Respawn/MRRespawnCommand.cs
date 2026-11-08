using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Player;
using Robust.Shared.Toolshed;

namespace Content.Server._Mriya.Respawn;

/// <summary>
/// mriyarespawn:limit N - respawns per player (-1 = unlimited)
/// mriyarespawn:delay S - seconds as a ghost before respawning is allowed
/// mriyarespawn:reset - reset everyone's used respawns
/// mriyarespawn:resetplayer P - reset one player's used respawns
/// mriyarespawn:status
/// Needs an active MRRespawnRule (addgamerule MRRespawnRule).
/// </summary>
[ToolshedCommand(Name = "mriyarespawn"), AdminCommand(AdminFlags.Round)]
public sealed class MRRespawnCommand : ToolshedCommand
{
    private const string NoRule = "No active MRRespawnRule. Start one with: addgamerule MRRespawnRule";

    [CommandImplementation("limit")]
    public void Limit(IInvocationContext ctx, int limit)
    {
        if (!Sys<MRRespawnRuleSystem>().SetLimit(limit))
        {
            ctx.WriteLine(NoRule);
            return;
        }

        ctx.WriteLine(limit < 0 ? "Respawns: unlimited" : $"Respawns per player: {limit}");
    }

    [CommandImplementation("delay")]
    public void Delay(IInvocationContext ctx, float seconds)
    {
        if (!Sys<MRRespawnRuleSystem>().SetDelay(TimeSpan.FromSeconds(Math.Max(0, seconds))))
        {
            ctx.WriteLine(NoRule);
            return;
        }

        ctx.WriteLine($"Respawn delay: {Math.Max(0, seconds)} s");
    }

    [CommandImplementation("reset")]
    public void Reset(IInvocationContext ctx)
    {
        ctx.WriteLine(Sys<MRRespawnRuleSystem>().Reset(null) ? "Respawns reset for everyone" : NoRule);
    }

    [CommandImplementation("resetplayer")]
    public void ResetPlayer(IInvocationContext ctx, ICommonSession player)
    {
        ctx.WriteLine(Sys<MRRespawnRuleSystem>().Reset(player.UserId) ? $"Respawns reset for {player.Name}" : NoRule);
    }

    [CommandImplementation("status")]
    public void Status(IInvocationContext ctx)
    {
        if (!Sys<MRRespawnRuleSystem>().TryGetStatus(out var limit, out var delay))
        {
            ctx.WriteLine(NoRule);
            return;
        }

        ctx.WriteLine($"Limit: {(limit < 0 ? "unlimited" : limit.ToString())}, delay: {delay.TotalSeconds} s");
    }
}