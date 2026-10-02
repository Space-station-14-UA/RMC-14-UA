using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Nuke;

[Serializable, NetSerializable]
public sealed partial class MriyaNukeActivateDoAfterEvent : SimpleDoAfterEvent
{
    public int Sequence;
}
