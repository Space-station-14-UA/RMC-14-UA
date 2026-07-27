using Robust.Client.Graphics;

namespace Content.Client._F14;

public sealed class BlinkingSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlayMan.HasOverlay<BlinkOverlay>())
            _overlayMan.AddOverlay(new BlinkOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<BlinkOverlay>();
    }
}