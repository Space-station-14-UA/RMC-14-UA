using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.ATM;

[Serializable, NetSerializable]
public enum ATMUIKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ATMBuiCategory
{
    public string Name;
    public List<ATMBuiEntry> Entries;

    public ATMBuiCategory(string name, List<ATMBuiEntry> entries)
    {
        Name = name;
        Entries = entries;
    }
}

[Serializable, NetSerializable]
public sealed class ATMBuiEntry
{
    public string Name;
    public int Cost;
    public EntProtoId Preview;

    public ATMBuiEntry(string name, int cost, EntProtoId preview)
    {
        Name = name;
        Cost = cost;
        Preview = preview;
    }
}

[Serializable, NetSerializable]
public sealed class ATMBuiState : BoundUserInterfaceState
{
    public int Balance;
    public string UserName;
    public string MapName;
    public int TerminalNumber;
    public string LinkCode;
    public List<ATMBuiCategory> Categories;

    public ATMBuiState(int balance, string userName, string mapName, int terminalNumber, string linkCode, List<ATMBuiCategory> categories)
    {
        Balance = balance;
        UserName = userName;
        MapName = mapName;
        TerminalNumber = terminalNumber;
        LinkCode = linkCode;
        Categories = categories;
    }
}

[Serializable, NetSerializable]
public sealed class ATMBuyMsg(int category, int entry) : BoundUserInterfaceMessage
{
    public readonly int Category = category;
    public readonly int Entry = entry;
}

[Serializable, NetSerializable]
public sealed class ATMWithdrawMsg(int amount) : BoundUserInterfaceMessage
{
    public readonly int Amount = amount;
}
