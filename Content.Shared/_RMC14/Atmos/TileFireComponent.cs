using Content.Shared._Mriya.Atmos; // Mriya. Оновлення вогню.
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._RMC14.Atmos;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedRMCFlammableSystem), typeof(FireSpreadSystem))]  // Mriya. Додано typeof(FireSpreadSystem)
public sealed partial class TileFireComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId<TileFireComponent>? Id;

    [DataField, AutoNetworkedField]
    public bool ExtinguishInstantly = true;

    [DataField, AutoNetworkedField]
    public float PatExtinguishMultiplier = 1;

    [DataField, AutoNetworkedField]
    public float SprayExtinguishMultiplier = 1;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan SpawnedAt;

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(55);

    [DataField, AutoNetworkedField]
    public TimeSpan BigFireDuration = TimeSpan.FromSeconds(0.5);
    //Mriya start. Оновлення вогню.
    [DataField, AutoNetworkedField]
    public bool CanSpread = true;

    [DataField, AutoNetworkedField]
    public float BaseSpreadChance = 0.35f;

    [DataField, AutoNetworkedField]
    public float SpreadFalloffPerTile = 0.05f;

    [DataField, AutoNetworkedField]
    public float MinSpreadChance;

    [DataField, AutoNetworkedField]
    public float DistancePenaltyPerTile = 0.2f;

    [DataField, AutoNetworkedField]
    public int MaxSpreadDistance = 5;

    [DataField, AutoNetworkedField]
    public float SpreadCheckInterval = 1.5f;

    [DataField, AutoNetworkedField]
    public int MaxSpreadsPerCheck = 2;

    [DataField, AutoNetworkedField]
    public float EmptyTileFactor = 0.6f;

    [DataField, AutoNetworkedField]
    public int DistanceFromOrigin;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextSpreadCheck;

    [DataField, AutoNetworkedField]
    public float CookOffMultiplier = 1f;
    // Mriya end.
}

[Serializable, NetSerializable]
public enum TileFireLayers
{
    Base,
}

[Serializable, NetSerializable]
public enum TileFireVisuals
{
    One,
    Two,
    Three,
    Four,
}
