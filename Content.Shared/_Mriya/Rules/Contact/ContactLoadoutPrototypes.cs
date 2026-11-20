using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// One buyable Contact skill, mapped onto an RMC skill (YAML type: contactSkill).
/// Levels BaseLevel..MaxLevel; everything above BaseLevel costs skill points.
/// </summary>
[Prototype]
public sealed partial class ContactSkillPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The RMC skill this sets (RMCSkillFirearms, RMCSkillMedical, ...).
    /// </summary>
    [DataField(required: true)]
    public EntProtoId<SkillDefinitionComponent> Skill;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>
    /// Free level everyone gets.
    /// </summary>
    [DataField]
    public int BaseLevel;

    [DataField(required: true)]
    public int MaxLevel;

    /// <summary>
    /// Costs[n] = points to go from level n to n + 1 (absolute level). Shorter than MaxLevel: the last cost repeats.
    /// </summary>
    [DataField(required: true)]
    public List<int> Costs = new();

    /// <summary>
    /// Sort order in the window.
    /// </summary>
    [DataField]
    public int Order;
}

[Serializable, NetSerializable]
public enum ContactLoadoutCategory : byte
{
    Weapon,
    Uniform,
    Armor,
    Helmet,
    Attachment,
}

/// <summary>
/// One pickable piece of Contact gear (YAML type: contactLoadoutItem). Free, one per category.
/// Attachments are offered for the chosen weapon's slots whose whitelist they pass.
/// </summary>
[Prototype]
public sealed partial class ContactLoadoutItemPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ContactLoadoutCategory Category;

    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>
    /// Shown name; null = the entity's name.
    /// </summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    /// Weapons: given with it (magazines, shell boxes), put into storage.
    /// </summary>
    [DataField]
    public List<EntProtoId> Extras = new();

    [DataField]
    public int Order;
}
