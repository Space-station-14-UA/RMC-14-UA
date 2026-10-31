using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.ATM;

[RegisterComponent]
public sealed partial class ATMComponent : Component
{
    [DataField]
    public ProtoId<ATMShopPrototype> Shop = "RMCATMShop";

    [DataField]
    public SoundSpecifier PurchaseSound = new SoundPathSpecifier("/Audio/_Mriya/terminal.ogg");

    public int TerminalNumber = 4;

    public string LinkCode = string.Empty;
}

[RegisterComponent]
public sealed partial class BankAccountComponent : Component
{
    [DataField]
    public int Balance = 100;

    [DataField]
    public TimeSpan GainEvery = TimeSpan.FromMinutes(1);

    [DataField]
    public int GainAmount = 30;

    public TimeSpan NextGain;
}
