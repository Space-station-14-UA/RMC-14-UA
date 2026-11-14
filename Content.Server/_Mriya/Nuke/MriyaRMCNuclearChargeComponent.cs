using Robust.Shared.Audio;

namespace Content.Server._Mriya.Nuke;

/// <summary>
/// Stores the configuration and runtime state for the Mriya strategic nuclear charge.
/// </summary>
[RegisterComponent, Access(typeof(MriyaRMCNuclearChargeSystem), typeof(MriyaNukeCommand))]
public sealed partial class MriyaRMCNuclearChargeComponent : Component
{
    /// <summary>
    /// Item slot used to hold the nuclear authentication disk.
    /// </summary>
    [DataField]
    public string DiskSlotId = "mriya-rmc-nuke-disk";

    /// <summary>
    /// Time required for an authorized user to complete the activation sequence.
    /// </summary>
    [DataField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Countdown duration after the charge is armed.
    /// </summary>
    [DataField]
    public TimeSpan DetonationDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Delay between the visual detonation starting and the map-wide nuclear cleanup.
    /// </summary>
    [DataField]
    public TimeSpan MapKillDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Looping siren played on the affected map after the three-minute warning.
    /// </summary>
    [DataField]
    public SoundSpecifier ThirtySecondWarningSound = new SoundPathSpecifier("/Audio/_Mriya/Nuke/30sec_nuke_warning.ogg", AudioParams.Default.WithVolume(-1).WithLoop(true));

    /// <summary>
    /// Global music cue started shortly before detonation.
    /// </summary>
    [DataField]
    public SoundSpecifier WarheadThemeSound = new SoundPathSpecifier("/Audio/_Mriya/Nuke/warhead_theme.ogg", AudioParams.Default.WithVolume(0));

    /// <summary>
    /// Explosion sound heard by entities on the affected map.
    /// </summary>
    [DataField]
    public SoundSpecifier MapExplosionSound = new SoundPathSpecifier("/Audio/_Mriya/Nuke/Nuke_explosion_map_sound.ogg", AudioParams.Default.WithVolume(2));

    /// <summary>
    /// Flyby explosion sound heard by entities away from the affected map.
    /// </summary>
    [DataField]
    public SoundSpecifier FlybyExplosionSound = new SoundPathSpecifier("/Audio/_Mriya/Nuke/Alamo_Flyby_Nukesoundeffect.ogg", AudioParams.Default.WithVolume(-1));

    /// <summary>
    /// Explosion prototype used for the visual blast wave.
    /// </summary>
    [DataField]
    public string ExplosionType = "MRNuke";

    /// <summary>
    /// Total visual explosion intensity.
    /// </summary>
    [DataField]
    public float ExplosionTotalIntensity = 80000000;

    /// <summary>
    /// Visual explosion falloff slope.
    /// </summary>
    [DataField]
    public float ExplosionSlope = 25;

    /// <summary>
    /// Maximum visual explosion intensity per tile.
    /// </summary>
    [DataField]
    public float ExplosionMaxTileIntensity = 400;

    /// <summary>
    /// Damage threshold at which physical damage defuses and destroys the charge.
    /// </summary>
    [DataField]
    public float DisableDamage = 350;

    /// <summary>
    /// Whether an activation do-after is currently in progress.
    /// </summary>
    public bool Activating;

    /// <summary>
    /// Whether the charge has been armed and is counting down.
    /// </summary>
    public bool Armed;

    /// <summary>
    /// Whether the detonation sequence has already started.
    /// </summary>
    public bool Detonated;

    /// <summary>
    /// Whether the charge was destroyed or defused before detonation.
    /// </summary>
    public bool Destroyed;

    /// <summary>
    /// Whether the final music cue has already started.
    /// </summary>
    public bool ThemeStarted;

    /// <summary>
    /// Incremented whenever a new activation sequence starts or is invalidated.
    /// </summary>
    public int ActivationSequence;

    /// <summary>
    /// Game time at which the charge detonates.
    /// </summary>
    public TimeSpan DetonatesAt;

    /// <summary>
    /// Game time at which the map-wide nuclear cleanup should run.
    /// </summary>
    public TimeSpan NukeMapAt;

    /// <summary>
    /// Audio stream entity for the looping warning siren.
    /// </summary>
    public EntityUid? WarningSirenStream;

    /// <summary>
    /// Audio stream entity for the final music cue.
    /// </summary>
    public EntityUid? WarheadThemeStream;

    /// <summary>
    /// Countdown thresholds that have already produced an announcement.
    /// </summary>
    public readonly HashSet<int> AnnouncedAtSeconds = new();
}
