using Content.Shared._Mriya.Rules.Contact;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact evacuation. Lives on the MRContactRule entity next to ContactRule.
/// After every EveryWaves waves: a BreakTime pause (anchors offline, field frozen, reinforcements still allowed).
/// More than half the colony within ZoneRadius of an LZ -> StartCountdown -> Holding (faster waves, shuttle reserved)
/// -> shuttle comes once refueled -> Boarding -> takeoff: anyone alive aboard = win.
/// </summary>
[RegisterComponent, Access(typeof(ContactRuleSystem))]
public sealed partial class ContactExtractionComponent : Component
{
    [DataField]
    public int EveryWaves = 10;

    /// <summary>
    /// Pause before the next wave while extraction is offered.
    /// </summary>
    [DataField]
    public TimeSpan BreakTime = TimeSpan.FromSeconds(45);

    [DataField]
    public float ZoneRadius = 10f;

    [DataField]
    public TimeSpan StartCountdown = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Defend the LZ this long before the shuttle is called.
    /// </summary>
    [DataField]
    public TimeSpan HoldTime = TimeSpan.FromSeconds(150);

    /// <summary>
    /// Wave interval from the start of the hold until the end of the round.
    /// </summary>
    [DataField]
    public TimeSpan HoldWaveInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Time on the LZ before the shuttle takes off.
    /// </summary>
    [DataField]
    public TimeSpan BoardTime = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    /// <summary>
    /// After the shuttle has left the planet, wait this long before counting who made it.
    /// </summary>
    [DataField]
    public TimeSpan DepartCheckDelay = TimeSpan.FromSeconds(15);

    #region Runtime

    /// <summary>
    /// Final wave: when the window runs out, evacuation starts anyway.
    /// </summary>
    [ViewVariables]
    public bool Forced;

    [ViewVariables]
    public ContactExtractionState State = ContactExtractionState.None;

    [ViewVariables]
    public TimeSpan WindowEndsAt;

    [ViewVariables]
    public TimeSpan? StateEndsAt;

    [ViewVariables]
    public TimeSpan NextCheck;

    /// <summary>
    /// The LZ that reached quorum (the evacuation point).
    /// </summary>
    [ViewVariables]
    public EntityUid? Zone;

    [ViewVariables]
    public int InZone;

    [ViewVariables]
    public int Needed;

    /// <summary>
    /// Last HUD values broadcast; only send when these change.
    /// </summary>
    [ViewVariables]
    public (ContactExtractionState, int, int, TimeSpan?) LastSent;

    #endregion
}