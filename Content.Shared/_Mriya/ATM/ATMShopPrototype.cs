using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.ATM;

[Prototype("atmShop")]
public sealed partial class ATMShopPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ATMShopCategory> Categories = new();
}

[DataDefinition]
public sealed partial class ATMShopCategory
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public List<ATMShopEntry> Entries = new();
}

[DataDefinition]
public sealed partial class ATMShopEntry
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public EntProtoId Case;

    [DataField(required: true)]
    public int Cost;

    [DataField]
    public EntProtoId? Preview;
}
