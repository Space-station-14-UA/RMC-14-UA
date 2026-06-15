using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._BTP.Nuke;

[RegisterComponent, Access(typeof(BTPIntelNukeSystem))]
public sealed partial class BTPIntelNukeObjectiveComponent : Component
{
    [DataField]
    public FixedPoint2 RequiredIntelPoints = FixedPoint2.New(8);

    [DataField]
    public int RequiredTowers = 2;

    [DataField]
    public TimeSpan DecodeDuration = TimeSpan.FromMinutes(5);

    [DataField]
    public EntProtoId ChargePrototype = "BTPRMCNuclearCharge";

    public BTPIntelNukeStage Stage = BTPIntelNukeStage.WaitingForIntel;
    public TimeSpan DecodeProgress;
    public TimeSpan LastUpdatedAt;
    public TimeSpan NextTowerStatusAt;
    public int LastReportedActiveTowers = -1;
    public readonly HashSet<int> DecodeAnnouncedAtSeconds = new();
}

public enum BTPIntelNukeStage
{
    WaitingForIntel,
    WaitingForTowers,
    Decoding,
    ChargeAuthorized,
}
