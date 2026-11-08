using System.Numerics;
using Content.Client._CMU14.Announce;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared._CMU14.Announce;
using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Retro (Sevastopol) style panel in the bottom right of the game viewport:
/// field, wave, next wave, evacuation, shuttle, є-бали. Blue while the local player stands in an evacuation zone.
/// A CMU CRTOverlay child draws scanlines/noise on top.
/// </summary>
public sealed class ContactHudControl : Control
{
    private static readonly Color Bg = Color.FromHex("#0D0D0DD9");
    private static readonly Color Phosphor = Color.FromHex("#0AFE60");
    private static readonly Color Green = Color.FromHex("#087A25");
    private static readonly Color Teal = Color.FromHex("#047D44");
    private static readonly Color Blue = Color.FromHex("#3FA9F5");
    private static readonly Color BlueDark = Color.FromHex("#1C5A8C");
    private static readonly Color White = Color.FromHex("#FFFFFF");
    private static readonly Color Black = Color.FromHex("#000000");
    private static readonly Color Alert = Color.FromHex("#FF4040");

    private const string FontPath = "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf";

    private const float PanelWidth = 300f;
    private const float Padding = 8f;
    private const float HeaderHeight = 20f;
    private const float LineHeight = 22f;
    private const float MarginRight = 12f;
    private const float MarginBottom = 12f;

    /// <summary>
    /// Field time below this is shown blinking red.
    /// </summary>
    private static readonly TimeSpan FieldWarning = TimeSpan.FromSeconds(30);

    private readonly IEntityManager _ents;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly ContactHudSystem _hud;
    private readonly Font _font;
    private readonly Font _fontValue;

    private readonly List<(string Label, string Value, Color Color)> _lines = new();
    private bool _inZone;

    public ContactHudControl(ContactHudSystem hud)
    {
        _hud = hud;
        _ents = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _transform = _ents.System<SharedTransformSystem>();

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont(FontPath, 11);
        _fontValue = cache.GetFont(FontPath, 14);

        MouseFilter = MouseFilterMode.Ignore;

        // drawn after Draw(), so it sits on top of the panel
        AddChild(new CRTOverlay
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Settings = new CRTSettings
            {
                Enabled = true,
                ShowScanlines = true,
                ScanlineSpacing = 3f,
                ScanlineThickness = 1f,
                ScanlineAlpha = 0.18f,
                ScanlineColor = Black,
                ShowNoise = true,
                NoiseIntensity = 0.08f,
                ShowVignette = true,
                VignetteIntensity = 0.2f,
                ShowChromaticAberration = false,
                GlowColor = Phosphor,
            },
        });
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var now = _timing.CurTime;
        _inZone = IsLocalPlayerInZone();
        BuildLines(now, _inZone ? Blue : Phosphor);

        var height = HeaderHeight + Padding * 2 + LineHeight * _lines.Count;
        SetSize = new Vector2(PanelWidth, height);

        // bottom right of the game viewport (falls back to the whole screen)
        var right = Parent?.Size.X ?? 0f;
        var bottom = Parent?.Size.Y ?? 0f;
        if (UserInterfaceManager.ActiveScreen?.GetWidget<MainViewport>() is { } viewport)
        {
            right = viewport.GlobalPosition.X + viewport.Size.X;
            bottom = viewport.GlobalPosition.Y + viewport.Size.Y;
        }

        LayoutContainer.SetPosition(this, new Vector2(right - PanelWidth - MarginRight, bottom - height - MarginBottom));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var s = UIScale;
        var accent = _inZone ? Blue : Phosphor;
        var border = _inZone ? BlueDark : Green;
        var width = PanelWidth * s;
        var box = PixelSizeBox;

        handle.DrawRect(box, Bg);
        handle.DrawRect(box, border, filled: false);

        handle.DrawRect(new UIBox2(0, 0, width, HeaderHeight * s), accent);
        var title = _inZone ? "mriya-contact-hud-title-extraction" : "mriya-contact-hud-title";
        handle.DrawString(_font, new Vector2(Padding * s, 3f * s), Loc.GetString(title).ToUpperInvariant(), s, Black);

        var y = (HeaderHeight + Padding) * s;
        foreach (var (label, value, color) in _lines)
        {
            handle.DrawString(_font, new Vector2(Padding * s, y + 4f * s), $">{label.ToUpperInvariant()}", s, _inZone ? BlueDark : Teal);

            if (value.Length > 0)
            {
                var size = handle.GetDimensions(_fontValue, value, s);
                handle.DrawString(_fontValue, new Vector2(width - Padding * s - size.X, y), value, s, color);
            }

            y += LineHeight * s;
        }
    }

    private bool IsLocalPlayerInZone()
    {
        if (_hud.ExtractionState is ContactExtractionState.None or ContactExtractionState.Done ||
            _player.LocalEntity is not { } local)
        {
            return false;
        }

        var pos = _transform.GetMapCoordinates(local);
        foreach (var net in _hud.ExtractionZones)
        {
            var zone = _transform.ToMapCoordinates(_ents.GetCoordinates(net));
            if (zone.MapId == pos.MapId && (zone.Position - pos.Position).Length() <= _hud.ExtractionRadius)
                return true;
        }

        return false;
    }

    private void BuildLines(TimeSpan now, Color accent)
    {
        _lines.Clear();

        if (_hud.Phase == ContactHudPhase.Staging)
        {
            _lines.Add((Loc.GetString("mriya-contact-hud-staging"), string.Empty, accent));
        }
        else
        {
            // Field
            if (_hud.FieldUnavailable)
            {
                _lines.Add((Loc.GetString("mriya-contact-hud-field"),
                    Loc.GetString("mriya-contact-hud-unavailable").ToUpperInvariant(), Teal));
            }
            else if (_hud.FieldEndsAt is { } fieldEndsAt)
            {
                var left = fieldEndsAt - now;
                var color = left > FieldWarning
                    ? accent
                    : (now.TotalSeconds % 1 < 0.5 ? Alert : White);

                _lines.Add((Loc.GetString("mriya-contact-hud-field"), FormatTime(left), color));
            }

            _lines.Add((Loc.GetString("mriya-contact-hud-wave"), _hud.Wave.ToString(), White));

            if (_hud.NextWaveAt is { } nextWaveAt)
                _lines.Add((Loc.GetString("mriya-contact-hud-next-wave"), FormatTime(nextWaveAt - now), accent));

            AddExtractionLines(now);
        }

        // Shuttle
        if (_hud.ShuttleState is { } shuttle)
        {
            var state = Loc.GetString(shuttle switch
            {
                ContactShuttleState.WaitingForBoarding => "mriya-contact-hud-shuttle-waiting",
                ContactShuttleState.Countdown => "mriya-contact-hud-shuttle-countdown",
                ContactShuttleState.FlyingDown or ContactShuttleState.Returning => "mriya-contact-hud-shuttle-flying",
                ContactShuttleState.Unloading => "mriya-contact-hud-shuttle-unloading",
                ContactShuttleState.Refueling => "mriya-contact-hud-shuttle-refueling",
                _ => "mriya-contact-hud-shuttle-ready",
            }).ToUpperInvariant();

            var value = _hud.ShuttleEndsAt is { } endsAt ? $"{state} {FormatTime(endsAt - now)}" : state;
            _lines.Add((Loc.GetString("mriya-contact-hud-shuttle"), value, accent));
        }

        if (_hud.Balance is { } balance)
            _lines.Add((Loc.GetString("mriya-contact-hud-balance"), balance.ToString(), White));
    }

    private void AddExtractionLines(TimeSpan now)
    {
        var endsAt = _hud.ExtractionEndsAt;
        var time = endsAt is { } e ? FormatTime(e - now) : string.Empty;
        var label = Loc.GetString(_inZone ? "mriya-contact-hud-extraction-zone" : "mriya-contact-hud-extraction");

        string? value = _hud.ExtractionState switch
        {
            ContactExtractionState.Available when _inZone =>
                Loc.GetString("mriya-contact-hud-extraction-people",
                    ("inZone", _hud.ExtractionInZone), ("needed", _hud.ExtractionNeeded)),
            ContactExtractionState.Available =>
                $"{Loc.GetString("mriya-contact-hud-extraction-available")} {time}",
            ContactExtractionState.Countdown =>
                $"{Loc.GetString("mriya-contact-hud-extraction-countdown")} {time}",
            ContactExtractionState.Holding when endsAt is { } holdEnd && holdEnd > now =>
                $"{Loc.GetString("mriya-contact-hud-extraction-holding")} {time}",
            ContactExtractionState.Holding =>
                Loc.GetString("mriya-contact-hud-extraction-waiting"),
            ContactExtractionState.ShuttleInbound =>
                Loc.GetString("mriya-contact-hud-extraction-inbound"),
            ContactExtractionState.Boarding =>
                $"{Loc.GetString("mriya-contact-hud-extraction-boarding")} {time}",
            ContactExtractionState.Departing =>
                Loc.GetString("mriya-contact-hud-extraction-departing"),
            _ => null,
        };

        if (value != null)
            _lines.Add((label, value.ToUpperInvariant(), _inZone ? Blue : White));
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }
}