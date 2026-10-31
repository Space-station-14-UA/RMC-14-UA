using Robust.Shared.Prototypes;
using Content.Shared._Mriya.Rules.Contact;

namespace Content.Server._Mriya.Rules.Contact;

[RegisterComponent, Access(typeof(ContactRuleSystem))]
public sealed partial class ContactRuleComponent : Component
{
    [DataField]
    public TimeSpan FieldStart = TimeSpan.FromSeconds(150);

    [DataField]
    public TimeSpan TerminalFieldBonus = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan WaveInterval = TimeSpan.FromSeconds(50);

    [DataField]
    public TimeSpan TerminalWaveReduction = TimeSpan.FromSeconds(20);

    [DataField]
    public int FinalWave = 100;

    [DataField]
    public ProtoId<ContactWavesPrototype> Waves = "MRContactWavesDefault";

    /// <summary>Fractional spawn carry-over per Spawns entry index.</summary>
    [DataField]
    public Dictionary<int, float> SpawnAccumulators = new();

    [DataField]
    public ContactPhase Phase = ContactPhase.Staging;

    [DataField]
    public ContactResult Result = ContactResult.None;

    [DataField]
    public TimeSpan? FieldEndsAt;

    [DataField]
    public TimeSpan? NextWaveAt;

    [DataField]
    public int Wave;

    [DataField]
    public TimeSpan MarineCheckEvery = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan? NextMarineCheck;

    [DataField]
    public float DropshipPreflightMinutes = 2;

    [DataField]
    public float DespawnRange = 100f;

    [DataField]
    public TimeSpan DespawnDelay = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan DespawnCheckEvery = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan? NextDespawnCheck;

    [DataField]
    public float SpawnRange = 150f;
}

[RegisterComponent, Access(typeof(ContactRuleSystem))]
public sealed partial class ContactTerminalComponent : Component
{
    [DataField]
    public bool Activated;
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
}
