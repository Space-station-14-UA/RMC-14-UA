// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Content.Shared._CMU14.Announce;
using System.Collections.Generic;

namespace Content.Client._CMU14.Announce.Effects;

public static class AnnouncementEffectsRegistry
{
    public static IEnumerable<IAnnouncementVisualEffect> BuildEffects(AnnouncementStyle style)
    {
        if (style.SpriteConfig.SpriteGlow)
            yield return new GlowEffect();

        if (style.AnimationConfig.FlickerChance > 0)
            yield return new FlickerEffect();

        if (style.AnimationConfig.Animation == AnnouncementAnimation.Fade)
            yield return new FadeEffect();

        if (style.AnimationConfig.Animation == AnnouncementAnimation.Pulse || style.AnimationConfig.Animation == AnnouncementAnimation.Heartbeat)
            yield return new PulseEffect();

        if (style.TitleConfig.Effect.Type == AnnouncementTitleEffectType.AssaultPulse)
            yield return new TitleAssaultPulseEffect();

        if (style.TitleConfig.Effect.Type == AnnouncementTitleEffectType.AssaultScroll)
            yield return new TitleAssaultScrollEffect();
    }
}

