using Robust.Client.Graphics;
using Robust.Shared.GameObjects;

namespace Content.Client._F14;

/// <summary>
/// Клієнтська система кліпання, яка реєструє оверлей темноти при ініціалізації.
/// </summary>
public sealed class BlinkingSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    /// <summary>
    /// Ініціалізує систему та додає оверлей кліпання до менеджера оверлеїв.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        if (!_overlayMan.HasOverlay<BlinkOverlay>())
            _overlayMan.AddOverlay(new BlinkOverlay());
    }

    /// <summary>
    /// Видаляє оверлей кліпання при вимкненні системи.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();
        _overlayMan.RemoveOverlay<BlinkOverlay>();
    }
}