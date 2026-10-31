namespace Content.Server._Mriya.Rules.Contact;

[RegisterComponent, Access(typeof(ContactTerminalSystem))]
public sealed partial class ContactTerminalComponent : Component
{
    [DataField]
    public bool Activated;

    /// <summary>
    /// Loc key to override the auto-assigned name (set in YAML or on the map).
    /// </summary>
    [DataField]
    public LocId? CustomName;

    /// <summary>
    /// Resolved display name, used in announcements.
    /// </summary>
    [ViewVariables]
    public string TerminalName = string.Empty;

    /// <summary>
    /// Time until the terminal can be activated again. Null = one-shot.
    /// </summary>
    [DataField]
    public TimeSpan? Cooldown = 120;

    [DataField]
    public TimeSpan? ReactivateAt;
}