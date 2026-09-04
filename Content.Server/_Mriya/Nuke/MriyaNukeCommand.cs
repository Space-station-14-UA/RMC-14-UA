using Content.Server.Administration;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared.Administration;
using Robust.Shared.Prototypes;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Robust.Shared.Toolshed;

namespace Content.Server._Mriya.Nuke;

[ToolshedCommand, AdminCommand(AdminFlags.VarEdit)]
public sealed class MriyaNukeCommand : ToolshedCommand
{
    private static readonly EntProtoId ChargePrototype = "MRNuclearCharge";

    [CommandImplementation("unlock")]
    public void Unlock(IInvocationContext context)
    {
        if (!UnlockTechOption(context))
            return;

        AuthorizeCharge(context);
        context.WriteLine(Loc.GetString("mriya-nuke-command-unlocked"));
    }

    [CommandImplementation("unlocktech")]
    public void UnlockTech(IInvocationContext context)
    {
        if (UnlockTechOption(context))
            context.WriteLine(Loc.GetString("mriya-nuke-command-tech-unlocked"));
    }

    [CommandImplementation("decrypt")]
    public void Decrypt(IInvocationContext context)
    {
        AuthorizeCharge(context);
        context.WriteLine(Loc.GetString("mriya-nuke-command-decrypted"));
    }

    [CommandImplementation("detonationtime")]
    public void SetDetonationTime(IInvocationContext context, int seconds)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(1, seconds));
        var timing = IoCManager.Resolve<IGameTiming>();
        var changed = 0;

        var query = EntityManager.EntityQueryEnumerator<MriyaRMCNuclearChargeComponent>();
        while (query.MoveNext(out _, out var charge))
        {
            charge.DetonationDelay = delay;
            if (charge.Armed)
            {
                charge.DetonatesAt = timing.CurTime + delay;
                charge.AnnouncedAtSeconds.Clear();
            }

            changed++;
        }

        context.WriteLine(Loc.GetString("mriya-nuke-command-detonation-time-set", ("seconds", (int) delay.TotalSeconds), ("count", changed)));
    }

    [CommandImplementation("decryptiontime")]
    public void SetDecryptionTime(IInvocationContext context, int seconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(1, seconds));
        var intel = Sys<IntelSystem>();
        var tree = intel.EnsureTechTree();
        var objective = EntityManager.EnsureComponent<MriyaIntelNukeObjectiveComponent>(tree.Owner);
        objective.DecodeDuration = duration;
        if (objective.DecodeProgress > duration)
            objective.DecodeProgress = duration;

        objective.DecodeAnnouncedAtSeconds.Clear();
        context.WriteLine(Loc.GetString("mriya-nuke-command-decryption-time-set", ("seconds", (int) duration.TotalSeconds)));
    }

    [CommandImplementation("timelock")]
    public void SetTimeLock(IInvocationContext context, int seconds)
    {
        if (SetTechOptionTimeLock(context, TimeSpan.FromSeconds(Math.Max(0, seconds))))
            context.WriteLine(Loc.GetString("mriya-nuke-command-timelock-set", ("seconds", Math.Max(0, seconds))));
    }

    private bool UnlockTechOption(IInvocationContext context)
    {
        var intel = Sys<IntelSystem>();
        var tree = intel.EnsureTechTree();
        var changed = false;

        foreach (var tier in tree.Comp.Tree.Options)
        {
            for (var i = 0; i < tier.Count; i++)
            {
                var option = tier[i];
                if (!DeliversCharge(option))
                    continue;

                tier[i] = option with
                {
                    Disabled = false,
                    TimeLock = TimeSpan.Zero,
                };
                changed = true;
            }
        }

        if (!changed)
        {
            context.WriteLine(Loc.GetString("mriya-nuke-command-option-not-found"));
            return false;
        }

        EntityManager.Dirty(tree);
        intel.UpdateTree(tree);
        return true;
    }

    private bool SetTechOptionTimeLock(IInvocationContext context, TimeSpan timeLock)
    {
        var intel = Sys<IntelSystem>();
        var tree = intel.EnsureTechTree();
        var changed = false;

        foreach (var tier in tree.Comp.Tree.Options)
        {
            for (var i = 0; i < tier.Count; i++)
            {
                var option = tier[i];
                if (!DeliversCharge(option))
                    continue;

                tier[i] = option with
                {
                    Disabled = false,
                    TimeLock = timeLock,
                };
                changed = true;
            }
        }

        if (!changed)
        {
            context.WriteLine(Loc.GetString("mriya-nuke-command-option-not-found"));
            return false;
        }

        EntityManager.Dirty(tree);
        intel.UpdateTree(tree);
        return true;
    }

    private void AuthorizeCharge(IInvocationContext context)
    {
        var intel = Sys<IntelSystem>();
        var tree = intel.EnsureTechTree();
        var objective = EntityManager.EnsureComponent<MriyaIntelNukeObjectiveComponent>(tree.Owner);
        objective.Stage = MriyaIntelNukeStage.ChargeAuthorized;
        objective.DecodeProgress = objective.DecodeDuration;
    }

    private static bool DeliversCharge(TechOption option)
    {
        foreach (var ev in option.Events)
        {
            if (ev is TechLogisticsDeliveryEvent delivery &&
                delivery.Object == ChargePrototype)
            {
                return true;
            }
        }

        return false;
    }
}
