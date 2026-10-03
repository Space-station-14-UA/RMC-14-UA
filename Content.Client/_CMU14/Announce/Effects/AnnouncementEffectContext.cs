// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Content.Shared._CMU14.Announce;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._CMU14.Announce.Effects;

public readonly record struct AnnouncementEffectContext(
    AnnouncementStyle Style,
    ActiveAnnouncement State,
    IReadOnlyList<RichTextLabel> Labels,
    bool HasTitle);
