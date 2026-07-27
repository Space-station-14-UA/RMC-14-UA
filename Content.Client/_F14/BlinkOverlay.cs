using Content.Shared._F14;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;


namespace Content.Client._F14;

/// <summary>
/// Клієнтський оверлей, що кладе чорний прямокутник замість усього.
/// </summary>
public sealed class BlinkOverlay : Overlay
{
    /// <summary>
    /// Максимальний пріорітет рендеру, рендерить прямокутний понад усім
    /// </summary>
    private const int BlinkOverlayZIndex = 100000;

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _playerMan;

    /// <summary>
    /// Простір рендерингу оверлея (WorldSpace перекриває усе).
    /// </summary>
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    /// <summary>
    /// Створює новий екземпляр оверлея кліпання та ініціалізує залежності.
    /// </summary>
    public BlinkOverlay()
    {
        _entMan = IoCManager.Resolve<IEntityManager>();
        _playerMan = IoCManager.Resolve<IPlayerManager>();
        ZIndex = BlinkOverlayZIndex;
    }

    /// <summary>
    /// Малює чорний прямокутник на весь видимий екран, якщо очі локального гравця закриті.
    /// </summary>
    /// <param name="args">Аргументи малювання оверлея.</param>
    protected override void Draw(in OverlayDrawArgs args)
    {
        var player = _playerMan.LocalSession?.AttachedEntity;
        if (player == null) return;

        if (_entMan.TryGetComponent<BlinkingComponent>(player.Value, out var blink) && blink.IsBlinking)
        {
            args.WorldHandle.UseShader(null);
            args.WorldHandle.DrawRect(args.WorldBounds, Color.Black);
        }
    }
}