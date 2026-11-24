namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact skills and loadout. Lives on the MRContactRule entity.
/// Points = BasePoints + every milestone the player has ever survived (saved on the server).
/// </summary>
[RegisterComponent, Access(typeof(ContactLoadoutSystem))]
public sealed partial class ContactLoadoutComponent : Component
{
    [DataField]
    public int BasePoints = 16;

    /// <summary>
    /// Wave survived -> bonus points, unlocked for good the first time a player is alive when that wave ends.
    /// </summary>
    [DataField]
    public Dictionary<int, int> Milestones = new()
    {
        { 20, 2 },
        { 40, 2 },
    };
}

/// <summary>
/// On a marine during Contact: the window was offered, and what the loadout gave (deleted when re-picked).
/// </summary>
[RegisterComponent, Access(typeof(ContactLoadoutSystem))]
public sealed partial class ContactLoadoutStateComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Given = new();

    /// <summary>
    /// Items given by perks (store tablet, etc), replaced on every apply.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> PerkItems = new();
}
