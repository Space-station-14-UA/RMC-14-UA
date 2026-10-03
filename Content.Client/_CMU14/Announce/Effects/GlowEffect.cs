// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Announce.Effects;

public sealed class GlowEffect : IAnnouncementVisualEffect
{
    public void Apply(AnnouncementEffectContext context, TimeSpan currentTime)
    {
        var time = (float) currentTime.TotalSeconds;
        var intensity = context.Style.SpriteConfig.SpriteGlowIntensity;

        foreach (var label in context.Labels)
        {
            var baseColor = label.Modulate;
            var glow = MathF.Sin(time * 3f) * 0.5f + 0.5f;
            var glowFactor = 1.0f + (glow * intensity);

            label.Modulate = new Color(
                Math.Min(baseColor.R * glowFactor, 1.0f),
                Math.Min(baseColor.G * glowFactor, 1.0f),
                Math.Min(baseColor.B * glowFactor, 1.0f),
                baseColor.A);
        }
    }
}

