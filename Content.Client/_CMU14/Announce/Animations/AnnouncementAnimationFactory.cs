// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
using Content.Shared._CMU14.Announce;
using Robust.Shared.Random;

namespace Content.Client._CMU14.Announce.Animations;

public static class AnnouncementAnimationFactory
{
    public static IAnnouncementAnimation Create(AnnouncementStyle style, IRobustRandom random)
    {
        return style.AnimationConfig.Animation switch
        {
            AnnouncementAnimation.None => new NoneAnimation(),
            AnnouncementAnimation.Typewriter => new TypewriterAnimation(),
            AnnouncementAnimation.Glitch => new GlitchAnimation(),
            AnnouncementAnimation.Slide => new SlideAnimation(),
            AnnouncementAnimation.Zoom => new ZoomAnimation(),
            AnnouncementAnimation.Bounce => new BounceAnimation(),
            AnnouncementAnimation.Fade => new FadeAnimation(),
            AnnouncementAnimation.Pulse => new PulseAnimation(),
            AnnouncementAnimation.Heartbeat => new HeartbeatAnimation(),
            AnnouncementAnimation.Warp => new WarpAnimation(),
            _ => new NoneAnimation()
        };
    }
}
