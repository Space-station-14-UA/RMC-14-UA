using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// One buy-once Contact perk (Creedbreaker-style skill), YAML type: contactPerk.
/// Grouped into decks in the window; what it does is its list of effects.
/// </summary>
[Prototype]
public sealed partial class ContactPerkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Deck heading (Frontliner, Gunslinger...).
    /// </summary>
    [DataField(required: true)]
    public LocId Deck;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    [DataField(required: true)]
    public int Cost;

    /// <summary>
    /// Sort order; decks are ordered by their lowest perk order.
    /// </summary>
    [DataField]
    public int Order;

    [DataField]
    public List<ContactPerkEffect> Effects = new();
}

[Serializable, NetSerializable]
public enum ContactLoadoutCategory : byte
{
    Weapon,
    Uniform,
    Armor,
    Helmet,
}

/// <summary>
/// One pickable piece of Contact gear (YAML type: contactLoadoutItem). Free, one per category.
/// Attachments aren't listed: every attachment the chosen weapon's slot whitelist accepts is offered.
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
