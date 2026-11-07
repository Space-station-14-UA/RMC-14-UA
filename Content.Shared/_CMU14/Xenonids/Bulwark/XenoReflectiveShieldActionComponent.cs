using Robust.Shared.GameStates;

namespace Content.Shared._CMU14.Xenonids.Bulwark;

[RegisterComponent, NetworkedComponent]
[Access(typeof(XenoBulwarkSystem))]
public sealed partial class XenoReflectiveShieldActionComponent : Component;
