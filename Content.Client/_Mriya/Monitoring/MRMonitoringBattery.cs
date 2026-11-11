using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Monitoring;

public sealed class MRMonitoringBattery : Control
{
    private const int CellCount = 4;

    private static readonly Color Outline = Color.FromHex("#2E9E57");
    private static readonly Color Empty = Color.FromHex("#0B2413");
    private static readonly Color Full = Color.FromHex("#5CFF8A");
    private static readonly Color Mid = Color.FromHex("#FFD166");
    private static readonly Color Low = Color.FromHex("#FF5C5C");

    private float _charge = 1f;

    public float Charge
    {
        get => _charge;
        set => _charge = Math.Clamp(value, 0f, 1f);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var scale = UIScale;
        var body = new UIBox2(1f * scale, 2f * scale, size.X - 5f * scale, size.Y - 2f * scale);
        if (body.Width <= 0f || body.Height <= 0f)
            return;

        handle.DrawRect(body, Outline, false);

        var nib = new UIBox2(body.Right + 1f * scale, body.Top + body.Height * 0.3f, body.Right + 4f * scale, body.Bottom - body.Height * 0.3f);
        handle.DrawRect(nib, Outline);

        var inset = 2f * scale;
        var content = new UIBox2(body.Left + inset, body.Top + inset, body.Right - inset, body.Bottom - inset);
        if (content.Width <= 0f || content.Height <= 0f)
            return;

        var gap = 1f * scale;
        var cellWidth = (content.Width - gap * (CellCount - 1)) / CellCount;
        if (cellWidth <= 0f)
            return;

        var filledColor = _charge > 0.5f ? Full : _charge > 0.2f ? Mid : Low;
        for (var i = 0; i < CellCount; i++)
        {
            var left = content.Left + i * (cellWidth + gap);
            var cell = new UIBox2(left, content.Top, left + cellWidth, content.Bottom);
            handle.DrawRect(cell, Empty);

            var threshold = (i + 1f) / CellCount;
            if (_charge < threshold - 1f / CellCount)
                continue;

            var fill = Math.Clamp(_charge * CellCount - i, 0f, 1f);
            if (fill <= 0f)
                continue;

            handle.DrawRect(new UIBox2(cell.Left, cell.Top, cell.Left + cell.Width * fill, cell.Bottom), filledColor);
        }
    }
}
