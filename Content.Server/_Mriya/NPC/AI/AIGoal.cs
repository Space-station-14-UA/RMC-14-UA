using JetBrains.Annotations;

namespace Content.Server._Mriya.NPC.AI;

[ImplicitDataDefinitionForInheritors, MeansImplicitUse]
public abstract partial class AIGoal
{
    [DataField("name")]
    public string Name = "Goal";

    [DataField("desiredState")]
    public AIState DesiredState = new();

    public virtual void Initialize(IEntitySystemManager sysManager)
    {
        sysManager.DependencyCollection.InjectDependencies(this);
    }

    public virtual bool IsRelevant(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return true;
    }

    public abstract float GetPriority(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan);
}
