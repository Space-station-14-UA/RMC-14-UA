using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;

namespace Content.Shared._Mriya.Terminal;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedMriyaTerminalSystem))]
public sealed partial class MriyaTerminalComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsInput = false;

    [DataField, AutoNetworkedField]
    public List<string> Messages = new();

    [DataField, AutoNetworkedField]
    public string? AuthorizedName;

    [DataField]
    public string IdCardSlotId = "id_card";

    [DataField]
    public SoundSpecifier ClickSound = new SoundCollectionSpecifier("Keyboard");
}
