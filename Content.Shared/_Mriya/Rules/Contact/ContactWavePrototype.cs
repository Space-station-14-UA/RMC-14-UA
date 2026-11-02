using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Spawn and buff tables for Contact waves.
/// </summary>
[Prototype]
public sealed partial class ContactWavesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public List<ContactWaveSpawnEntry> Spawns = new();

    /// <summary>
    /// The entry with the highest Wave that is &lt;= current wave applies. Multipliers are absolute, not stacked.
    /// </summary>
    [DataField]
    public List<ContactWaveBuffEntry> Buffs = new();
}

[DataDefinition]
public sealed partial class ContactWaveSpawnEntry
{
    [DataField(required: true)]
    public EntProtoId Proto;

    /// <summary>
    /// First wave this mob appears in.
    /// </summary>
    [DataField]
    public int StartWave = 1;

    /// <summary>
    /// Spawns per wave at StartWave. Fractions accumulate: 0.5 = one every other wave.
    /// </summary>
    [DataField]
    public float Rate = 1f;

    /// <summary>
    /// Added to Rate for every wave after StartWave.
    /// </summary>
    [DataField]
    public float RatePerWave;

    /// <summary>
    /// Max alive at once for this proto. -1 = no cap.
    /// </summary>
    [DataField]
    public int MaxAlive = -1;

    /// <summary>muhney for killing this mob. Null = ContactEconomy KillReward.</summary>
    [DataField]
    public int? Reward;
}

[DataDefinition]
public sealed partial class ContactWaveBuffEntry
{
    [DataField(required: true)]
    public int Wave;

    [DataField]
    public float Health = 1f;

    [DataField]
    public float Damage = 1f;

    /// <summary>
    /// Custom announcement for this tier. Null = generic message with percentages.
    /// </summary>
    [DataField]
    public LocId? Announcement;
}