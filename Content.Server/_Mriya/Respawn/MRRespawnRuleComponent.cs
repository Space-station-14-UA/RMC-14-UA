using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.Respawn;

/// <summary>
/// Game rule: ghosts get an action to return to the lobby and join again.
/// Add it to any preset, or start it mid-round (addgamerule MRRespawnRule) and tune it with mriyarespawn:*.
/// </summary>
[RegisterComponent, Access(typeof(MRRespawnRuleSystem))]
public sealed partial class MRRespawnRuleComponent : Component
{
    /// <summary>
    /// Respawns per player for this rule. -1 = unlimited.
    /// </summary>
    [DataField]
    public int MaxRespawns = -1;

    /// <summary>
    /// Time a player has to be a ghost before the action works.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    [DataField]
    public EntProtoId Action = "MRActionRespawn";

    /// <summary>
    /// Respawns used per player.
    /// </summary>
    [ViewVariables]
    public Dictionary<NetUserId, int> Used = new();
}

/// <summary>
/// On ghosts while an MRRespawnRule is active.
/// </summary>
[RegisterComponent, Access(typeof(MRRespawnRuleSystem))]
public sealed partial class MRRespawnGhostComponent : Component
{
    [ViewVariables]
    public TimeSpan GhostedAt;

    [ViewVariables]
    public EntityUid? Action;
}