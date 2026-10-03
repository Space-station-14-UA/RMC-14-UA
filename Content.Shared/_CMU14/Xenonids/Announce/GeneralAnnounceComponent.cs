// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Announce;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GeneralAnnounceComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, AnnouncementConfig> AnnouncementConfigs = new();
}

[DataDefinition, NetSerializable, Serializable]
public sealed partial class AnnouncementConfig
{
    [DataField]
    public AnnouncementTarget Target = AnnouncementTarget.All;

    [DataField]
    public AnnouncementStyle Style = new();

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public float SoundVolume = 1.0f;

    [DataField]
    public bool RequiresFaction = false;

    [DataField]
    public List<string> RequiredComponents = new();

    [DataField]
    public List<string> ExcludedComponents = new();

    [DataField]
    public float Priority = 0f;

    [DataField]
    public bool CanInterrupt = true;

    [DataField]
    public bool CanBeInterrupted = true;
}
