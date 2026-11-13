using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Monitoring;

public sealed class MRMonitoringSignal : Control
{
    private const int BarCount = 4;

    private static readonly Color Active = Color.FromHex("#5CFF8A");
    private static readonly Color Inactive = Color.FromHex("#123D20");

    private int _bars;

    public int Bars
    {
        get => _bars;
        set => _bars = Math.Clamp(value, 0, BarCount);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var scale = UIScale;
        var gap = 2f * scale;
        var barWidth = (size.X - gap * (BarCount - 1)) / BarCount;
        if (barWidth <= 0f)
            return;

        for (var i = 0; i < BarCount; i++)
        {
            var height = (size.Y - 2f * scale) * (i + 1f) / BarCount;
            var left = i * (barWidth + gap);
            var bottom = size.Y - 1f * scale;
            var bar = new UIBox2(left, bottom - height, left + barWidth, bottom);
            handle.DrawRect(bar, i < _bars ? Active : Inactive);
        }
    }
}
