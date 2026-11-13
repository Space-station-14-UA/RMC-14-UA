using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Monitoring;

public sealed class MRMonitoringCompass : Control
{
    private static readonly Color Back = Color.FromHex("#04120A");
    private static readonly Color Ring = Color.FromHex("#2E9E57");
    private static readonly Color Grid = Color.FromHex("#123D20");
    private static readonly Color Tick = Color.FromHex("#1E7A3C");
    private static readonly Color MajorTick = Color.FromHex("#4FE07E");
    private static readonly Color Needle = Color.FromHex("#7CFFA8");
    private static readonly Color NeedleGlow = Color.FromHex("#33FF77");

    private float _angle;
    private bool _hasTarget;

    public float Angle
    {
        get => _angle;
        set => _angle = value;
    }

    public bool HasTarget
    {
        get => _hasTarget;
        set => _hasTarget = value;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var center = new Vector2(size.X / 2f, size.Y / 2f);
        var radius = MathF.Min(size.X, size.Y) / 2f - 4f * UIScale;
        if (radius <= 0f)
            return;

        handle.DrawCircle(center, radius, Back);
        handle.DrawCircle(center, radius, Ring, false);
        handle.DrawCircle(center, radius * 0.64f, Grid, false);

        var inner = radius * 0.16f;
        var outer = radius * 0.9f;
        handle.DrawLine(center - new Vector2(0f, outer), center - new Vector2(0f, inner), Grid);
        handle.DrawLine(center + new Vector2(0f, inner), center + new Vector2(0f, outer), Grid);
        handle.DrawLine(center - new Vector2(outer, 0f), center - new Vector2(inner, 0f), Grid);
        handle.DrawLine(center + new Vector2(inner, 0f), center + new Vector2(outer, 0f), Grid);

        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4f;
            var direction = new Vector2(MathF.Cos(angle), -MathF.Sin(angle));
            var start = center + direction * (radius * 0.84f);
            var end = center + direction * (radius * 0.95f);
            handle.DrawLine(start, end, i % 2 == 0 ? MajorTick : Tick);
        }

        if (!_hasTarget)
            return;

        var screenDirection = new Vector2(MathF.Cos(_angle), -MathF.Sin(_angle));
        var tip = center + screenDirection * (radius * 0.72f);
        var tail = center - screenDirection * (radius * 0.2f);
        var perpendicular = new Vector2(-screenDirection.Y, screenDirection.X) * UIScale;

        handle.DrawLine(tail, tip, NeedleGlow);
        handle.DrawLine(tail + perpendicular, tip + perpendicular, Needle);
        handle.DrawLine(tail - perpendicular, tip - perpendicular, Needle);

        var arrowBase = tip - screenDirection * (radius * 0.18f);
        var arrowWing = new Vector2(-screenDirection.Y, screenDirection.X) * (radius * 0.09f);
        handle.DrawLine(tip, arrowBase + arrowWing, Needle);
        handle.DrawLine(tip, arrowBase - arrowWing, Needle);

        handle.DrawCircle(center, radius * 0.05f, Needle);
    }
}
