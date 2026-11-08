using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Mriya. Breaching version of Wizden's MeleeOperator: attacks the obstacle through the same
/// NPCMeleeCombatComponent, but reads the target with TryGetValue and never removes the key.
/// Wizden's MeleeOperator removes its key on shutdown, so a plan computed just before that crashes on startup.
/// </summary>
public sealed partial class ContactSmashOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    [DataField(required: true)]
    public string TargetKey = default!;

    public override void Startup(NPCBlackboard blackboard)
    {
        base.Startup(blackboard);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
            return;

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var melee = _entManager.EnsureComponent<NPCMeleeCombatComponent>(owner);
        melee.Target = target;
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        base.Update(blackboard, frameTime);

        // obstacle gone = done; ContactXenoAISystem clears the smash flag
        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager) ||
            !_entManager.EntityExists(target))
        {
            return HTNOperatorStatus.Finished;
        }

        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<NPCMeleeCombatComponent>(owner, out var combat))
            return HTNOperatorStatus.Failed;

        return combat.Status is CombatStatus.Normal or CombatStatus.TargetOutOfRange
            ? HTNOperatorStatus.Continuing
            : HTNOperatorStatus.Failed;
    }

    public override void TaskShutdown(NPCBlackboard blackboard, HTNOperatorStatus status)
    {
        base.TaskShutdown(blackboard, status);

        // only the combat component; the target key stays (see summary)
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        _entManager.RemoveComponent<NPCMeleeCombatComponent>(owner);
    }
}