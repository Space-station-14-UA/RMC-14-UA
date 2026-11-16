using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.NPC;

public static class NPCLineOfSight
{
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";

    public static bool IsSeeThrough(EntityUid blocker, IEntityManager entMan)
    {
        return entMan.System<TagSystem>().HasTag(blocker, WindowTag);
    }
}
