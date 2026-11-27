using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Mriya.Explosion;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CookOffComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MinCookTime = 3f;

    [DataField, AutoNetworkedField]
    public float MaxCookTime = 6f;

    [DataField, AutoNetworkedField]
    public bool Cooking;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan CookUntil;

    [DataField, AutoNetworkedField]
    public bool Exploded;

    [DataField, AutoNetworkedField]
    public float SwellAmount = 1.15f;

    [DataField, AutoNetworkedField]
    public bool SpawnFireAfter = true;

    [DataField, AutoNetworkedField]
    public EntProtoId FireProto = "RMCTileFire";

    [DataField, AutoNetworkedField]
    public int FireRange = 2;

    [DataField, AutoNetworkedField]
    public int? FireIntensity;

    [DataField, AutoNetworkedField]
    public int? FireDuration;

    [DataField, AutoNetworkedField]
    public bool ExtinguishFireAfter;

    [DataField, AutoNetworkedField]
    public int ExtinguishRange = 2;
}
