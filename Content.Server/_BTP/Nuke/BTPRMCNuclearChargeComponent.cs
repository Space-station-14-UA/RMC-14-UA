using Robust.Shared.Audio;

namespace Content.Server._BTP.Nuke;

[RegisterComponent, Access(typeof(BTPRMCNuclearChargeSystem))]
public sealed partial class BTPRMCNuclearChargeComponent : Component
{
    [DataField]
    public string DiskSlotId = "btp-rmc-nuke-disk";

    [DataField]
    public TimeSpan ActivationDelay = TimeSpan.FromSeconds(12);

    [DataField]
    public TimeSpan DetonationDelay = TimeSpan.FromMinutes(5);

    [DataField]
    public TimeSpan MapKillDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public SoundSpecifier ThirtySecondWarningSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/30sec_nuke_warning.ogg", AudioParams.Default.WithVolume(-1).WithLoop(true));

    [DataField]
    public SoundSpecifier WarheadThemeSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/warhead_theme.ogg", AudioParams.Default.WithVolume(0));

    [DataField]
    public SoundSpecifier MapExplosionSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/Nuke_explosion_map_sound.ogg", AudioParams.Default.WithVolume(2));

    [DataField]
    public SoundSpecifier FlybyExplosionSound = new SoundPathSpecifier("/Audio/_BTP/Nuke/Alamo_Flyby_Nukesoundeffect.ogg", AudioParams.Default.WithVolume(-1));

    [DataField]
    public string ExplosionType = "BTPNuke";

    [DataField]
    public float ExplosionTotalIntensity = 80000000;

    [DataField]
    public float ExplosionSlope = 25;

    [DataField]
    public float ExplosionMaxTileIntensity = 400;

    [DataField]
    public float DisableDamage = 350;

    public bool Activating;
    public bool Armed;
    public bool Detonated;
    public bool Destroyed;
    public bool ThemeStarted;
    public TimeSpan DetonatesAt;
    public TimeSpan NukeMapAt;
    public EntityUid? WarningSirenStream;
    public EntityUid? WarheadThemeStream;
    public readonly HashSet<int> AnnouncedAtSeconds = new();
}
