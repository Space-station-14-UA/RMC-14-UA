using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Name;

[RegisterComponent, NetworkedComponent]
public sealed partial class XenoRankNamesComponent : Component
{
    [DataField]
    public Dictionary<int, LocId> RankNames = new()
    {
        {0, "rmc-xeno-young"},
        {2, "rmc-xeno-mature"},
        {3, "rmc-xeno-elder"},
        {4, "rmc-xeno-ancient"},
        {5, "rmc-xeno-prime"},
        {6, "rmc-xeno-apex"}, // Sich. Розширені назви
        {7, "rmc-xeno-royal"}, // Sich. Розширені назви
        {8, "rmc-xeno-legendary"}, // Sich. Розширені назви
        {9, "rmc-xeno-forever"} // Sich. Розширені назви
    };
}
