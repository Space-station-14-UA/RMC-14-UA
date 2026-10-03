// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Announce;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class AnnouncementTitleEffectConfig
{
    [DataField]
    public AnnouncementTitleEffectType Type { get; set; } = AnnouncementTitleEffectType.None;

    [DataField]
    public float Speed { get; set; } = 180f;

    [DataField]
    public float Gap { get; set; } = 48f;
}
