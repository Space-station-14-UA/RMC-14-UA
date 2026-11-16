namespace Content.Server.NPC.Components;

public sealed partial class NPCRangedCombatComponent
{
    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public int BurstSize;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BurstPause = 0.8f;

    [DataField]
    [ViewVariables(VVAccess.ReadWrite)]
    public float BurstPauseJitter = 0.3f;

    [ViewVariables]
    public int BurstCounter;

    [ViewVariables]
    public TimeSpan NextBurst;
}
