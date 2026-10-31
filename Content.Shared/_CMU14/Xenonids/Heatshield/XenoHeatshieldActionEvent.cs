using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenonids.Heatshield;

public sealed partial class XenoVomitBileActionEvent : WorldTargetActionEvent;

public sealed partial class XenoSelfImmolateActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class XenoSelfImmolateDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class XenoThermoregulationActionEvent : InstantActionEvent;
