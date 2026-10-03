// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using System;

namespace Content.Client._CMU14.Announce.Effects;

public interface IAnnouncementVisualEffect
{
    void Apply(AnnouncementEffectContext context, TimeSpan currentTime);
}
