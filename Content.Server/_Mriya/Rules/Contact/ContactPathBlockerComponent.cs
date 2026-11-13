namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Invisible helper that blocks one overhang tile of a large prop for the pathfinder.
/// </summary>
[RegisterComponent]
public sealed partial class ContactPathBlockerComponent : Component
{
    /// <summary>
    /// The prop this helper stands in for. Xeno AI smashes this instead of the helper.
    /// </summary>
    [ViewVariables]
    public EntityUid? Source;
}

/// <summary>
/// Put on a large prop that got helpers, deletes them when the prop goes away.
/// </summary>
[RegisterComponent]
public sealed partial class ContactPathBlockerSourceComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Blockers = new();
}