using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sich.Hunter.Zoom;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(HunterZoomSystem))]
public sealed partial class HunterZoomComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "ActionHunterZoom";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    /// <summary>
    /// Zoom level applied when active. Matches XenoZoom runner default.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Vector2 Zoom = new(1.25f, 1.25f);

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundOn = new SoundPathSpecifier("/Audio/_Sich/Effects/pred_zoom_on.ogg");

    [DataField, AutoNetworkedField]
    public SoundSpecifier? SoundOff = new SoundPathSpecifier("/Audio/_Sich/Effects/pred_zoom_off.ogg");
}
