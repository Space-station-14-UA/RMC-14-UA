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
    /// Fallback kill reward when the wave entry doesnt set one
    /// </summary>
    [DataField]
    public int KillReward = 5;

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