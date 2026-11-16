using JetBrains.Annotations;

namespace Content.Server._Mriya.NPC.AI;

[ImplicitDataDefinitionForInheritors, MeansImplicitUse]
public abstract partial class AIAction
{
    [DataField("name")]
    public string Name = "Action";

    [DataField("cost")]
    public float Cost = 1f;

    [DataField("preconditions")]
    public Dictionary<string, string> Preconditions = new();

    [DataField("effects")]
    public Dictionary<string, string> Effects = new();

    public virtual void Initialize(IEntitySystemManager sysManager)
    {
        sysManager.DependencyCollection.InjectDependencies(this);
    }

    public bool PreconditionsMet(AIState state)
    {
        foreach (var (key, value) in Preconditions)
        {
            if (!state.Facts.TryGetValue(key, out var current) || current != value)
                return false;
        }

        return true;
    }

    public virtual bool IsProcedurallyValid(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
        return true;
    }

    public virtual void Start(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
    }

    public abstract AIActionStatus Update(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan, float frameTime);

    public virtual void Shutdown(EntityUid owner, AIPlannerComponent comp, IEntityManager entMan)
    {
    }
}

public enum AIActionStatus : byte
{
    Continuing,

    Finished,

    Failed,
}
