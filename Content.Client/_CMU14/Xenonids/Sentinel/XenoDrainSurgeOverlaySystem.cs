using Robust.Client.Graphics;

namespace Content.Client._CMU14.Xenonids.Sentinel;

public sealed partial class XenoDrainSurgeOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override void Initialize()
    {
        _overlay.AddOverlay(new XenoDrainSurgeOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _overlay.RemoveOverlay<XenoDrainSurgeOverlay>();
    }
}
