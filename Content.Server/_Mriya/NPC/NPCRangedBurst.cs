using Content.Server.NPC.Components;
using Robust.Shared.Random;

namespace Content.Server._Mriya.NPC;

public static class NPCRangedBurst
{
    public static bool IsPaused(NPCRangedCombatComponent comp, TimeSpan now)
    {
        return comp.BurstSize > 0 && comp.NextBurst > now;
    }

    public static void OnShot(NPCRangedCombatComponent comp, TimeSpan now, IRobustRandom random)
    {
        if (comp.BurstSize <= 0)
            return;

        comp.BurstCounter++;

        if (comp.BurstCounter < comp.BurstSize)
            return;

        comp.BurstCounter = 0;
        var pause = comp.BurstPause + random.NextFloat(0f, MathF.Max(comp.BurstPauseJitter, 0f));
        comp.NextBurst = now + TimeSpan.FromSeconds(MathF.Max(pause, 0f));
    }
}
