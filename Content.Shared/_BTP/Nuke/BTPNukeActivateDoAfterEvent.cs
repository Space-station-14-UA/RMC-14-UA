using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._BTP.Nuke;

[Serializable, NetSerializable]
public sealed partial class BTPNukeActivateDoAfterEvent : SimpleDoAfterEvent;
