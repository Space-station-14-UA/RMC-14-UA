// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Content.Shared._CMU14.Announce;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Announce.Animations;

public sealed record AnnouncementAnimationContext(
    ActiveAnnouncement State,
    AnnouncementStyle Style,
    string[] OriginalText,
    string[] CleanText,
    RichTextLabel[] Labels,
    int TitleOffset,
    Func<string, AnnouncementStyle, FormattedMessage> FormatMessage,
    Action SetAllLabels,
    Control? VisualContainer,
    IRobustRandom Random);
