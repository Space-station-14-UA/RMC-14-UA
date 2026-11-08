using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// A store tab: ammo, long arms, short arms, melee, special, medicine, armor, clothing...
/// </summary>
[Prototype]
public sealed partial class ContactStoreCategoryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Lower = further left in the tab bar.
    /// </summary>
    [DataField]
    public int Priority;

    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    /// Shows an "all / compatible with your weapons" toggle in this folder.
    /// </summary>
    [DataField]
    public bool CompatibilityFilter;

}

/// <summary>
/// One thing you can buy. The single source of unit prices, also used by kit resupply.
/// </summary>
[Prototype]
public sealed partial class ContactStoreEntryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public ProtoId<ContactStoreCategoryPrototype> Category;

    [DataField(required: true)]
    public int Price;

    /// <summary>
    /// Overrides the entity name in the store.
    /// </summary>
    [DataField]
    public LocId? Name;

    /// <summary>
    /// Shows up in the "for you" tab's quick medicine section.
    /// </summary>
    [DataField]
    public bool Quick;

    [DataField]
    public int MaxPerPurchase = 10;
}

/// <summary>
/// A kit handed out by the loadout vendor. Resupply refills it to these counts,
/// paying the store price of each missing item. Items without a store entry can't be resupplied.
/// </summary>
[Prototype]
public sealed partial class ContactKitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// The vendor entry (usually a bundle) that grants this kit.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId VendorEntry;

    [DataField(required: true)]
    public List<ContactKitItem> Items = new();
}

[DataDefinition]
public sealed partial class ContactKitItem
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField]
    public int Count = 1;
}