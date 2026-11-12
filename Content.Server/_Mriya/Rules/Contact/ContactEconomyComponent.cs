using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Points rewards. Lives on the MRContactRule entity; economy is only active while that rule is
/// </summary>
[RegisterComponent, Access(typeof(ContactEconomySystem))]
public sealed partial class ContactEconomyComponent : Component
{
    [DataField]
    public int StartingPoints = 0;

    /// <summary>
    /// Fallback kill reward when neither the wave entry nor KillRewards set one
    /// </summary>
    [DataField]
    public int KillReward = 5;

    /// <summary>
    /// Kill reward per xeno prototype (harder castes give more). Wave entry reward still overrides this
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, int> KillRewards = new();

    /// <summary>
    /// Share of the kill reward everyone else who damaged the xeno gets
    /// </summary>
    [DataField]
    public float AssistFraction = 0.5f;

    /// <summary>
    /// Things a marine can pick up and deploy whose kills count for that marine (sentries, tesla coils)
    /// </summary>
    [DataField]
    public EntityWhitelist SentryWhitelist = new()
    {
        Components = new[] { "Sentry" },
    };

    /// <summary>
    /// Points per HealStep damage healed on someone else
    /// </summary>
    [DataField]
    public int HealReward = 3;

    [DataField]
    public float HealStep = 20f;

    [DataField]
    public int ReviveReward = 25;

    [DataField]
    public int TerminalReward = 15;
}

/// <summary>
/// Marks a player mob that takes part in the Contact economy (heal/revive tracking)
/// </summary>
[RegisterComponent, Access(typeof(ContactEconomySystem))]
public sealed partial class ContactPlayerComponent : Component
{
    /// <summary>
    /// Healing done by others to this mob that hasn't been converted to points yet, per healer
    /// </summary>
    [ViewVariables]
    public Dictionary<EntityUid, float> PendingHeals = new();
}

/// <summary>
/// On a wave mob: who has damaged it, for kill and assist rewards
/// </summary>
[RegisterComponent, Access(typeof(ContactEconomySystem))]
public sealed partial class ContactKillCreditComponent : Component
{
    [ViewVariables]
    public HashSet<NetUserId> Damagers = new();

    /// <summary>
    /// Last credited hit: gets the kill when the death has no origin (burning, fire tiles)
    /// </summary>
    [ViewVariables]
    public NetUserId? LastHit;

    [ViewVariables]
    public bool LastHitBySentry;
}

/// <summary>
/// On a sentry: the marine who last carried it, gets its kills
/// </summary>
[RegisterComponent, Access(typeof(ContactEconomySystem))]
public sealed partial class ContactSentryOwnerComponent : Component
{
    [ViewVariables]
    public NetUserId Owner;
}

/// <summary>
/// On a tile fire: whoever (probably) started it. Owner null = checked, nobody found (map fires, xeno fires)
/// </summary>
[RegisterComponent, Access(typeof(ContactEconomySystem))]
public sealed partial class ContactFireCreditComponent : Component
{
    [ViewVariables]
    public NetUserId? Owner;

    [ViewVariables]
    public bool BySentry;
}