using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Marks a mob spawned by a Contact wave. Used for population caps and damage buffs.
/// </summary>
[RegisterComponent, Access(typeof(ContactRuleSystem))]
public sealed partial class ContactWaveMobComponent : Component
{
    [DataField]
    public EntProtoId Proto;

    [DataField]
    public float DamageMultiplier = 1f;

    [DataField]
    public TimeSpan? LonelySince;
}