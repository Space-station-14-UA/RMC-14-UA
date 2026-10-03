// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using System;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Announce;

[Serializable, NetSerializable]
public sealed record AnnouncementStyleOverride
{
    public AnnouncementAnimation? Animation { get; init; }
    public RealisticAnimations? AnimationEnhancements { get; init; }

    public Color? PrimaryColor { get; init; }
    public Color? TitleColor { get; init; }
    public Color? BackgroundColor { get; init; }
    public float? BackgroundAlpha { get; init; }

    public AnnouncementPosition? Position { get; init; }
    public AnnouncementSpritePosition? SpritePosition { get; init; }

    public bool? ShowSpeakerName { get; init; }
    public Color? SpeakerNameColor { get; init; }
    public float? SpeakerNameFontSize { get; init; }
    public AnnouncementSpeakerNamePosition? SpeakerNamePosition { get; init; }

    public float? SpriteScale { get; init; }
    public float? SpriteSpacing { get; init; }
}
