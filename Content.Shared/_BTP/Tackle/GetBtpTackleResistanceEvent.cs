using Content.Shared.Inventory;

namespace Content.Shared._BTP.Tackle;

[ByRefEvent]
public record struct GetBtpTackleResistanceEvent(
    SlotFlags TargetSlots,
    float ChanceMultiplier = 1f,
    float StunMultiplier = 1f
) : IInventoryRelayEvent;
