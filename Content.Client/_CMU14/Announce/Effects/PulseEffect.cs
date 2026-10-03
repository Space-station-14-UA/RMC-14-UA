// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Announce.Effects;

public sealed class PulseEffect : IAnnouncementVisualEffect
{
    public void Apply(AnnouncementEffectContext context, TimeSpan currentTime)
    {
        foreach (var label in context.Labels)
        {
            var baseColor = label.Modulate;
            var alpha = baseColor.A * context.State.PulseAlpha;
            label.Modulate = new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
        }
    }
}
