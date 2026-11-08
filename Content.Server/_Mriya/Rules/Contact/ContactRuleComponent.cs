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
    public TimeSpan TerminalFieldBonus = TimeSpan.FromSeconds(30);

    /// <summary>
    /// CMU announcement preset for Contact's big on-screen announcements.
    /// </summary>
    [DataField]
    public string AnnouncementPreset = "MRContactTerminal";

    #endregion

    #region Waves

    /// <summary>
    /// From this wave on, the extraction offer can't be skipped. 0 = never.
    /// </summary>
    [DataField]
    public int FinalWave = 100;

    [DataField]
    public TimeSpan WaveInterval = TimeSpan.FromSeconds(50);

    [DataField]
    public TimeSpan TerminalWaveReduction = TimeSpan.FromSeconds(20);

    [DataField]
    public ProtoId<ContactWavesPrototype> Waves = "MRContactWavesDefault";

    /// <summary>
    /// Only spawn points within this range of a living player are used.
    /// </summary>
    [DataField]
    public float SpawnRange = 150f;

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