using Robust.Shared.Prototypes;

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

    // TODO Mriya: replace with a wave prototype (wave range -> spawn table, hp/armor multipliers)
    [DataField]
    public List<EntProtoId> WaveSpawns = new() { "CMXenoDrone" };

    [DataField]
    public int BaseWaveSize = 2;

    [DataField]
    public float WaveSizePerWave = 0.5f;

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
