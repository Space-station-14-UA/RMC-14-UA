using System.Numerics;
using Content.Client.Resources;
using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Retro (Sevastopol) style panel in the bottom right: field, wave, next wave, shuttle, evacuation, є-бали.
/// Turns blue while the local player stands in an evacuation zone.
/// </summary>
public sealed class ContactHudOverlay : Overlay
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

    private const float Width = 300f;
    private const float Padding = 8f;
    private const float HeaderHeight = 20f;
    private const float LineHeight = 22f;
    private const float MarginRight = 2f;
    private const float MarginBottom = 45f;

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

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public ContactHudOverlay(ContactHudSystem hud)
    {
        _hud = hud;
        _ents = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _transform = _ents.System<SharedTransformSystem>();

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont(FontPath, 11);
        _fontValue = cache.GetFont(FontPath, 14);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_hud.Phase is not (ContactHudPhase.Staging or ContactHudPhase.Deployed))
            return;

        var now = _timing.CurTime;
        var inZone = IsLocalPlayerInZone();
        var accent = inZone ? Blue : Phosphor;
        var border = inZone ? BlueDark : Green;

        BuildLines(now, inZone, accent);

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;

        var height = HeaderHeight + Padding * 2 + LineHeight * _lines.Count;
        var left = bounds.Right - Width - MarginRight;
        var top = bounds.Bottom - height - MarginBottom;
        var box = new UIBox2(left, top, left + Width, top + height);

        handle.DrawRect(box, Bg);
        handle.DrawRect(box, border, filled: false);

        var header = new UIBox2(left, top, left + Width, top + HeaderHeight);
        handle.DrawRect(header, accent);
        var title = inZone ? "mriya-contact-hud-title-extraction" : "mriya-contact-hud-title";
        handle.DrawString(_font, new Vector2(left + Padding, top + 3f), Loc.GetString(title).ToUpperInvariant(), Black);

        var y = top + HeaderHeight + Padding;
        foreach (var (label, value, color) in _lines)
        {
            handle.DrawString(_font, new Vector2(left + Padding, y + 4f), $">{label.ToUpperInvariant()}", inZone ? BlueDark : Teal);

            if (value.Length > 0)
            {
                var size = handle.GetDimensions(_fontValue, value, 1f);
                handle.DrawString(_fontValue, new Vector2(left + Width - Padding - size.X, y), value, color);
            }

            y += LineHeight;
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

    private void BuildLines(TimeSpan now, bool inZone, Color accent)
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

            AddExtractionLines(now, inZone);
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

    private void AddExtractionLines(TimeSpan now, bool inZone)
    {
        var endsAt = _hud.ExtractionEndsAt;
        var time = endsAt is { } e ? FormatTime(e - now) : string.Empty;
        var label = Loc.GetString(inZone ? "mriya-contact-hud-extraction-zone" : "mriya-contact-hud-extraction");

        string? value = _hud.ExtractionState switch
        {
            ContactExtractionState.Available when inZone =>
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
            _lines.Add((label, value.ToUpperInvariant(), inZone ? Blue : White));
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }
}