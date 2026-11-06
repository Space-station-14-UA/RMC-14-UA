using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Retro (Sevastopol) style panel at the top of the screen: field time, wave, next wave, є-бали.
/// </summary>
public sealed class ContactHudOverlay : Overlay
{
    private static readonly Color Bg = Color.FromHex("#0D0D0DD9");
    private static readonly Color Phosphor = Color.FromHex("#0AFE60");
    private static readonly Color Green = Color.FromHex("#087A25");
    private static readonly Color Teal = Color.FromHex("#047D44");
    private static readonly Color White = Color.FromHex("#FFFFFF");
    private static readonly Color Black = Color.FromHex("#000000");
    private static readonly Color Alert = Color.FromHex("#FF4040");

    private const string FontPath = "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf";

    private const float Width = 270f;
    private const float Padding = 8f;
    private const float HeaderHeight = 20f;
    private const float LineHeight = 22f;

    /// <summary>
    /// Field time below this is shown blinking red.
    /// </summary>
    private static readonly TimeSpan FieldWarning = TimeSpan.FromSeconds(30);

    private readonly IGameTiming _timing;
    private readonly ContactHudSystem _hud;
    private readonly Font _font;
    private readonly Font _fontValue;

    private readonly List<(string Label, string Value, Color Color)> _lines = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public ContactHudOverlay(ContactHudSystem hud)
    {
        _hud = hud;
        _timing = IoCManager.Resolve<IGameTiming>();

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont(FontPath, 11);
        _fontValue = cache.GetFont(FontPath, 14);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_hud.Phase is not (ContactHudPhase.Staging or ContactHudPhase.Deployed))
            return;

        var now = _timing.CurTime;
        BuildLines(now);

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        var height = HeaderHeight + Padding * 2 + LineHeight * _lines.Count;

        const float marginRight = 12f;
        const float marginBottom = 12f;

        var left = bounds.Right - Width - marginRight;
        var top = bounds.Bottom - height - marginBottom;
        var box = new UIBox2(left, top, left + Width, top + height);

        // panel + border
        handle.DrawRect(box, Bg);
        handle.DrawRect(box, Green, filled: false);

        // header bar: solid phosphor, black text
        var header = new UIBox2(left, top, left + Width, top + HeaderHeight);
        handle.DrawRect(header, Phosphor);
        handle.DrawString(_font, new Vector2(left + Padding, top + 3f),
            Loc.GetString("mriya-contact-hud-title").ToUpperInvariant(), Black);

        // lines: ">LABEL ........ VALUE"
        var y = top + HeaderHeight + Padding;
        foreach (var (label, value, color) in _lines)
        {
            handle.DrawString(_font, new Vector2(left + Padding, y + 4f), $">{label.ToUpperInvariant()}", Teal);

            if (value.Length > 0)
            {
                var size = handle.GetDimensions(_fontValue, value, 1f);
                handle.DrawString(_fontValue, new Vector2(left + Width - Padding - size.X, y), value, color);
            }

            y += LineHeight;
        }
    }

    private void BuildLines(TimeSpan now)
    {
        _lines.Clear();

        if (_hud.Phase == ContactHudPhase.Staging)
        {
            _lines.Add((Loc.GetString("mriya-contact-hud-staging"), string.Empty, Phosphor));
        }
        else
        {
            if (_hud.FieldEndsAt is { } fieldEndsAt)
            {
                var left = fieldEndsAt - now;
                var color = left > FieldWarning
                    ? Phosphor
                    : (now.TotalSeconds % 1 < 0.5 ? Alert : White);

                _lines.Add((Loc.GetString("mriya-contact-hud-field"), FormatTime(left), color));
            }

            _lines.Add((Loc.GetString("mriya-contact-hud-wave"), _hud.Wave.ToString(), White));

            if (_hud.NextWaveAt is { } nextWaveAt)
                _lines.Add((Loc.GetString("mriya-contact-hud-next-wave"), FormatTime(nextWaveAt - now), Phosphor));
        }

        if (_hud.Balance is { } balance)
            _lines.Add((Loc.GetString("mriya-contact-hud-balance"), balance.ToString(), White));
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }
}