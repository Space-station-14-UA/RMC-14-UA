using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sich.Hunter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Serializable]
public sealed partial class HunterRitualDuelComponent : Component
{
    /// <summary>
    /// The hunter who claimed this entity for ritual purposes
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid Hunter;

    /// <summary>
    /// Current state of the ritual
    /// </summary>
    [DataField, AutoNetworkedField]
    public HunterRitualState State = HunterRitualState.None;

    /// <summary>
    /// When the entity was captured
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CapturedAt;

    /// <summary>
    /// When the duel started (if applicable)
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan DuelStartedAt;

    /// <summary>
    /// Sound played when claiming a captive
    /// </summary>
    [DataField]
    public SoundSpecifier ClaimSound = new SoundPathSpecifier("/Audio/Effects/beep1.ogg");

    /// <summary>
    /// Sound played when starting a duel
    /// </summary>
    [DataField]
    public SoundSpecifier DuelSound = new SoundPathSpecifier("/Audio/Effects/beep2.ogg");

    /// <summary>
    /// Sound played when releasing a captive
    /// </summary>
    [DataField]
    public SoundSpecifier ReleaseSound = new SoundPathSpecifier("/Audio/Effects/beep3.ogg");
}

[Serializable, NetSerializable]
public enum HunterRitualState : byte
{
    None,
    Captive,
    DuelActive,
    Complete
}