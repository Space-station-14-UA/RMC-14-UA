// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Announce.Effects;

public sealed class FlickerEffect : IAnnouncementVisualEffect
{
    public void Apply(AnnouncementEffectContext context, TimeSpan currentTime)
    {
        var time = (float) currentTime.TotalSeconds;

        foreach (var label in context.Labels)
        {
            var baseColor = label.Modulate;
            var noise = MathF.Sin(time * 100f) * 0.5f + 0.5f;
            if (noise < context.Style.AnimationConfig.FlickerChance)
            {
                baseColor = new Color(
                    baseColor.R * 0.3f,
                    baseColor.G * 0.3f,
                    baseColor.B * 0.3f,
                    baseColor.A);
            }

            label.Modulate = baseColor;
        }
    }
}

