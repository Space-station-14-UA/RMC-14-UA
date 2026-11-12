using Content.Shared._Mriya.Rules.Contact;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.Rules.Contact;

[RegisterComponent, Access(typeof(ContactRuleSystem))]
public sealed partial class ContactRuleComponent : Component
{
    #region Field

    [DataField]
    public TimeSpan FieldStart = TimeSpan.FromSeconds(150);

    [DataField]
    public TimeSpan TerminalFieldBonus = TimeSpan.FromSeconds(45);

    /// <summary>
    /// CMU announcement preset for Contact's big on-screen announcements.
    /// </summary>
    [DataField]
    public string AnnouncementPreset = "MRARES";

    /// <summary>
    /// Contact planet pool, forced onto Distress at round start. Empty = normal Distress selection.
    /// </summary>
    [DataField]
    public List<EntProtoId> Planets = new();

    /// <summary>
    /// Field collapse: everyone with a body on the planet is gibbed with this effect.
    /// </summary>
    [DataField]
    public EntProtoId CollapseGibEffect = "MRGibEffectHuman";

    #endregion

    #region Waves

    /// <summary>
    /// From this wave on, the extraction offer can't be skipped. 0 = never.
    /// </summary>
    [DataField]
    public int FinalWave = 100;

    [DataField]
    public TimeSpan WaveInterval = TimeSpan.FromSeconds(75);

    [DataField]
    public TimeSpan TerminalWaveReduction = TimeSpan.FromSeconds(10);

    [DataField]
    public ProtoId<ContactWavesPrototype> Waves = "MRContactWavesDefault";

    /// <summary>
    /// Wave mobs spawn on a free planet tile at least this far from every living marine on the colony...
    /// </summary>
    [DataField]
    public float SpawnMinRange = 7f;

    /// <summary>
    /// ...and at most this far from one of them.
    /// </summary>
    [DataField]
    public float SpawnMaxRange = 20f;

    /// <summary>
    /// Waves prefer tiles no marine can see within this range (walls/occluders in between).
    /// Falls back to a visible tile if no hidden one is found.
    /// </summary>
    [DataField]
    public float SpawnSightRange = 15f;

    /// <summary>
    /// Random tiles tried per mob before giving up.
    /// </summary>
    [DataField]
    public int SpawnAttempts = 20;

    /// <summary>
    /// Wave mobs with no player within this range for DespawnDelay are deleted.
    /// </summary>
    [DataField]
    public float DespawnRange = 100f;

    [DataField]
    public TimeSpan DespawnDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan DespawnCheckEvery = TimeSpan.FromSeconds(5);

    #endregion

    [DataField]
    public TimeSpan MarineCheckEvery = TimeSpan.FromSeconds(2);

    [DataField]
    public float DropshipPreflightMinutes = 2;

    #region Runtime

    [DataField]
    public ContactPhase Phase = ContactPhase.Staging;

    [DataField]
    public ContactResult Result = ContactResult.None;

    [DataField]
    public TimeSpan? FieldEndsAt;

    /// <summary>
    /// Extraction offer / evacuation: anchors offline, field timer frozen.
    /// </summary>
    [DataField]
    public bool FieldUnavailable;

    [DataField]
    public TimeSpan? NextWaveAt;

    [DataField]
    public int Wave;

    [DataField]
    public int LastBuffWave;

    /// <summary>
    /// Fractional spawn carry-over per Spawns entry index.
    /// </summary>
    [DataField]
    public Dictionary<int, float> SpawnAccumulators = new();

    [DataField]
    public TimeSpan? NextMarineCheck;

    [DataField]
    public TimeSpan? NextDespawnCheck;

    #endregion
}

public enum ContactPhase : byte
{
    Staging,
    Deployed,
    Ended,
}

public enum ContactResult : byte
{
    None,
    FieldCollapsed,
    MarinesLost,
    Evacuated,
    ExtractionFailed,
}