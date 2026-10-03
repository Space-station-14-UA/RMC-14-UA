// Ported from Colonial Marines Universe (https://github.com/AU-14/ColonialMarinesUniverse), licensed under AGPL-3.0.
namespace Content.Client._CMU14.Announce.Animations;

public interface IAnnouncementAnimation
{
    void Reset(AnnouncementAnimationContext context);
    AnnouncementAnimationStatus Update(AnnouncementAnimationContext context, float deltaTime);
}
