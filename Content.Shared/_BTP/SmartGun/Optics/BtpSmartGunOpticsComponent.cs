using System.Numerics;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._BTP.SmartGun.Optics;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BtpSmartGunOpticsSystem))]
public sealed partial class BtpSmartGunOpticsComponent : Component, IClothingSlots
{
    [DataField, AutoNetworkedField]
    public EntProtoId ActionId = "BTPActionToggleM52Optics";

    [DataField, AutoNetworkedField]
    public EntityUid? Action;

    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public SlotFlags Slots { get; set; } = SlotFlags.EYES;

    [DataField, AutoNetworkedField]
    public Vector2 Zoom = new(1.25f, 1.25f);

    [DataField, AutoNetworkedField]
    public float PvsIncrease = 0.6f;
}
