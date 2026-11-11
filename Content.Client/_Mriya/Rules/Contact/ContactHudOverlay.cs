using System.Numerics;
using Content.Client._CMU14.Announce;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Screens;
using Content.Shared._CMU14.Announce;
using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Retro (Sevastopol) style Contact panel: field, wave, next wave, evacuation, shuttle, anchor arrow, є-бали.
/// Header buttons: collapse, move (drag, saved per HUD layout), options (which lines to show).
/// Only the header takes mouse input; the rest lets clicks through to the game.
/// Blue while the local player stands in an evacuation zone. A CMU CRTOverlay child draws scanlines on top.
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
    private const float ButtonSize = 14f;
    private const float ButtonGap = 4f;

    // default placement when no position is saved yet
    private const float SepRight = 12f;
    private const float SepTop = -50f;
    private const float DefLeft = 60f;
    private const float DefTop = 95f;

    /// <summary>
    /// Field time below this is shown blinking red.
    /// </summary>
    private static readonly TimeSpan FieldWarning = TimeSpan.FromSeconds(30);

    private readonly IEntityManager _ents;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly IConfigurationManager _cfg;
    private readonly IEyeManager _eye;
    private readonly SharedTransformSystem _transform;
    private readonly ContactHudSystem _hud;
    private readonly Font _font;
    private readonly Font _fontValue;

    private readonly record struct HudLine(string Label, string Value, Color Color, Vector2? Arrow);

    private readonly List<HudLine> _lines = new();
    private bool _inZone;
    private bool _collapsed;
    private bool _separated;
    private ContactHudLines _hidden;

    private bool _dragging;
    private Vector2 _dragOffset;
    private Vector2? _dragPos;

    private ContactHudOptionsWindow? _options;

    public ContactHudControl(ContactHudSystem hud)
    {
        _hud = hud;
        _ents = IoCManager.Resolve<IEntityManager>();
        _player = IoCManager.Resolve<IPlayerManager>();
        _timing = IoCManager.Resolve<IGameTiming>();
        _cfg = IoCManager.Resolve<IConfigurationManager>();
        _eye = IoCManager.Resolve<IEyeManager>();
        _transform = _ents.System<SharedTransformSystem>();

        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont(FontPath, 11);
        _fontValue = cache.GetFont(FontPath, 14);

        // header buttons need the mouse; HasPoint limits it to the header
        MouseFilter = MouseFilterMode.Stop;

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
                ScanlineAlpha = 0.08f,
                ScanlineColor = Black,
                ScanlineSpeed = 0f,
                ScanlineWaveAmplitude = 0f,
                ScanlineFlickerIntensity = 0f,
                ScanlineGlitchChance = 0f,
                ShowNoise = false,
                ShowVignette = true,
                VignetteIntensity = 0.12f,
                VignettePulseAmplitude = 0f,
                ShowChromaticAberration = false,
                FlickerChance = 0f,
                FlashChance = 0f,
                GlowColor = Phosphor,
            },
        });
    }

    #region Layout

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var now = _timing.CurTime;
        _collapsed = _cfg.GetCVar(ContactCVars.HudCollapsed);
        _hidden = (ContactHudLines) _cfg.GetCVar(ContactCVars.HudHiddenLines);
        _inZone = IsLocalPlayerInZone();

        _lines.Clear();
        if (!_collapsed)
            BuildLines(now, _inZone ? Blue : Phosphor);

        var height = HeaderHeight + (_lines.Count > 0 ? Padding * 2 + LineHeight * _lines.Count : 0f);
        SetSize = new Vector2(PanelWidth, height);

        var screen = UserInterfaceManager.ActiveScreen;
        _separated = screen is SeparatedChatGameScreen;

        var pos = _dragPos ?? GetSavedPosition() ?? GetDefaultPosition(screen);
        LayoutContainer.SetPosition(this, ClampToParent(pos, height));
    }

    private Vector2? GetSavedPosition()
    {
        var x = _cfg.GetCVar(_separated ? ContactCVars.HudSeparatedX : ContactCVars.HudDefaultX);
        var y = _cfg.GetCVar(_separated ? ContactCVars.HudSeparatedY : ContactCVars.HudDefaultY);
        return x < 0 || y < 0 ? null : new Vector2(x, y);
    }

    /// <summary>
    /// Separated (SS13) layout: top right of the viewport; default layout: top left.
    /// </summary>
    private Vector2 GetDefaultPosition(UIScreen? screen)
    {
        var left = 0f;
        var top = 0f;
        var right = Parent?.Size.X ?? 0f;
        if (screen?.GetWidget<MainViewport>() is { } viewport)
        {
            left = viewport.GlobalPosition.X;
            top = viewport.GlobalPosition.Y;
            right = viewport.GlobalPosition.X + viewport.Size.X;
        }

        var x = _separated ? right - PanelWidth - SepRight : left + DefLeft;
        var y = top + (_separated ? SepTop : DefTop);
        return new Vector2(x, y);
    }

    private Vector2 ClampToParent(Vector2 pos, float height)
    {
        if (Parent is not { } parent)
            return pos;

        var maxX = Math.Max(0f, parent.Size.X - PanelWidth);
        var maxY = Math.Max(0f, parent.Size.Y - height);
        return new Vector2(Math.Clamp(pos.X, 0f, maxX), Math.Clamp(pos.Y, 0f, maxY));
    }

    #endregion

    #region Input

    /// <summary>
    /// Only the header takes the mouse, clicks on the rest go to the game.
    /// </summary>
    protected override bool HasPoint(Vector2 point)
    {
        return point.X >= 0 && point.X <= PanelWidth && point.Y >= 0 && point.Y <= HeaderHeight;
    }

    /// <summary>
    /// Header buttons from the right: 0 options, 1 move, 2 collapse. UI pixels, local.
    /// </summary>
    private static UIBox2 ButtonRect(int index)
    {
        var right = PanelWidth - Padding / 2 - index * (ButtonSize + ButtonGap);
        var top = (HeaderHeight - ButtonSize) / 2;
        return new UIBox2(right - ButtonSize, top, right, top + ButtonSize);
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        var p = args.RelativePosition;
        if (ButtonRect(2).Contains(p))
        {
            _cfg.SetCVar(ContactCVars.HudCollapsed, !_collapsed);
            _cfg.SaveToFile();
            args.Handle();
        }
        else if (ButtonRect(1).Contains(p))
        {
            _dragging = true;
            _dragOffset = p;
            _dragPos = Position;
            args.Handle();
        }
        else if (ButtonRect(0).Contains(p))
        {
            ToggleOptions();
            args.Handle();
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (!_dragging)
            return;

        var parentPos = Parent?.GlobalPosition ?? Vector2.Zero;
        _dragPos = args.GlobalPosition - parentPos - _dragOffset;
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;
        if (_dragPos is { } dragPos)
        {
            var pos = ClampToParent(dragPos, Size.Y);
            _cfg.SetCVar(_separated ? ContactCVars.HudSeparatedX : ContactCVars.HudDefaultX, pos.X);
            _cfg.SetCVar(_separated ? ContactCVars.HudSeparatedY : ContactCVars.HudDefaultY, pos.Y);
            _cfg.SaveToFile();
        }

        _dragPos = null;
        args.Handle();
    }

    private void ToggleOptions()
    {
        if (_options is { IsOpen: true })
        {
            _options.Close();
            return;
        }

        _options ??= new ContactHudOptionsWindow(_cfg);
        _options.OpenCentered();
    }

    #endregion

    #region Draw

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
        DrawButtons(handle, s);

        var y = (HeaderHeight + Padding) * s;
        foreach (var line in _lines)
        {
            handle.DrawString(_font, new Vector2(Padding * s, y + 4f * s), $">{line.Label.ToUpperInvariant()}", s, _inZone ? BlueDark : Teal);

            var valueX = width - Padding * s;
            if (line.Value.Length > 0)
            {
                var size = handle.GetDimensions(_fontValue, line.Value, s);
                valueX -= size.X;
                handle.DrawString(_fontValue, new Vector2(valueX, y), line.Value, s, line.Color);
            }

            if (line.Arrow is { } arrow)
                DrawArrow(handle, new Vector2(valueX - 12f * s, y + LineHeight * s / 2), arrow, 7f * s, line.Color);

            y += LineHeight * s;
        }
    }

    private void DrawButtons(DrawingHandleScreen handle, float s)
    {
        // collapse: "-" open, "+" collapsed
        var collapse = Scaled(ButtonRect(2), s);
        handle.DrawRect(collapse, Black, filled: false);
        var c = collapse.Center;
        var w = collapse.Width * 0.3f;
        handle.DrawLine(c - new Vector2(w, 0), c + new Vector2(w, 0), Black);
        if (_collapsed)
            handle.DrawLine(c - new Vector2(0, w), c + new Vector2(0, w), Black);

        // move: 4-way cross
        var move = Scaled(ButtonRect(1), s);
        handle.DrawRect(move, Black, filled: false);
        c = move.Center;
        handle.DrawLine(c - new Vector2(w, 0), c + new Vector2(w, 0), Black);
        handle.DrawLine(c - new Vector2(0, w), c + new Vector2(0, w), Black);

        // options: three lines
        var options = Scaled(ButtonRect(0), s);
        handle.DrawRect(options, Black, filled: false);
        c = options.Center;
        for (var i = -1; i <= 1; i++)
        {
            var dy = i * options.Height * 0.22f;
            handle.DrawLine(c + new Vector2(-w, dy), c + new Vector2(w, dy), Black);
        }
    }

    private static UIBox2 Scaled(UIBox2 box, float s)
    {
        return new UIBox2(box.Left * s, box.Top * s, box.Right * s, box.Bottom * s);
    }

    private static void DrawArrow(DrawingHandleScreen handle, Vector2 center, Vector2 dir, float size, Color color)
    {
        var perp = new Vector2(-dir.Y, dir.X);
        var verts = new[]
        {
            center + dir * size,
            center - dir * size * 0.6f + perp * size * 0.6f,
            center - dir * size * 0.6f - perp * size * 0.6f,
        };

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, verts, color);
    }

    #endregion

    #region Lines

    private void Add(ContactHudLines flag, string label, string value, Color color, Vector2? arrow = null)
    {
        if ((_hidden & flag) != 0)
            return;

        _lines.Add(new HudLine(label, value, color, arrow));
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
        if (_hud.Phase == ContactHudPhase.Staging)
        {
            _lines.Add(new HudLine(Loc.GetString("mriya-contact-hud-staging"), string.Empty, accent, null));
        }
        else
        {
            // Field
            if (_hud.FieldUnavailable)
            {
                Add(ContactHudLines.Field, Loc.GetString("mriya-contact-hud-field"),
                    Loc.GetString("mriya-contact-hud-unavailable").ToUpperInvariant(), Teal);
            }
            else if (_hud.FieldEndsAt is { } fieldEndsAt)
            {
                var left = fieldEndsAt - now;
                var color = left > FieldWarning
                    ? accent
                    : (now.TotalSeconds % 1 < 0.5 ? Alert : White);

                Add(ContactHudLines.Field, Loc.GetString("mriya-contact-hud-field"), FormatTime(left), color);
            }

            Add(ContactHudLines.Wave, Loc.GetString("mriya-contact-hud-wave"), _hud.Wave.ToString(), White);

            if (_hud.NextWaveAt is { } nextWaveAt)
                Add(ContactHudLines.NextWave, Loc.GetString("mriya-contact-hud-next-wave"), FormatTime(nextWaveAt - now), accent);

            AddExtractionLines(now);
            if (!AddZoneLine(accent))
                AddAnchorLine(accent);
        }

        AddShuttleLines(now, accent);

        if (_hud.Balance is { } balance)
            Add(ContactHudLines.Balance, Loc.GetString("mriya-contact-hud-balance"), balance.ToString(), White);
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
            Add(ContactHudLines.Extraction, label, value.ToUpperInvariant(), _inZone ? Blue : White);
    }

    /// <summary>
    /// Arrow + distance to the nearest anchor that can still be activated, on the player's map.
    /// The arrow follows the camera rotation.
    /// </summary>
    private void AddAnchorLine(Color accent)
    {
        if ((_hidden & ContactHudLines.Anchor) != 0 ||
            _player.LocalEntity is not { } local ||
            !_ents.TryGetComponent(local, out TransformComponent? xform) ||
            xform.MapUid is not { } map)
        {
            return;
        }

        var netMap = _ents.GetNetEntity(map);
        var pos = _transform.GetWorldPosition(xform);

        Vector2? best = null;
        var bestLen = float.MaxValue;
        foreach (var anchor in _hud.Anchors)
        {
            if (anchor.Map != netMap)
                continue;

            var delta = anchor.Position - pos;
            var len = delta.Length();
            if (len >= bestLen)
                continue;

            best = delta;
            bestLen = len;
        }

        if (best is not { } bestDelta)
            return;

        Vector2? arrow = null;
        if (bestLen > 0.5f)
        {
            var rotated = _eye.CurrentEye.Rotation.RotateVec(bestDelta);
            arrow = Vector2.Normalize(new Vector2(rotated.X, -rotated.Y));
        }

        Add(ContactHudLines.Anchor, Loc.GetString("mriya-contact-hud-anchor"),
            Loc.GetString("mriya-contact-hud-anchor-distance", ("meters", (int) MathF.Round(bestLen))), accent, arrow);
    }

    /// <summary>
    /// Arrow + distance to the evacuation LZ while an extraction is offered or running:
    /// the nearest LZ while it's offered, the chosen one after (the server only sends that one).
    /// False when there's nothing on the player's map, so the anchor line shows instead.
    /// </summary>
    private bool AddZoneLine(Color accent)
    {
        if (_hud.ExtractionState is ContactExtractionState.None or ContactExtractionState.Done ||
            _player.LocalEntity is not { } local)
        {
            return false;
        }

        var pos = _transform.GetMapCoordinates(local);

        Vector2? best = null;
        var bestLen = float.MaxValue;
        foreach (var net in _hud.ExtractionZones)
        {
            var zone = _transform.ToMapCoordinates(_ents.GetCoordinates(net));
            if (zone.MapId != pos.MapId)
                continue;

            var delta = zone.Position - pos.Position;
            var len = delta.Length();
            if (len >= bestLen)
                continue;

            best = delta;
            bestLen = len;
        }

        if (best is not { } bestDelta)
            return false;

        Add(ContactHudLines.Anchor, Loc.GetString("mriya-contact-hud-lz"),
            Loc.GetString("mriya-contact-hud-anchor-distance", ("meters", (int) MathF.Round(bestLen))),
            accent, ToArrow(bestDelta, bestLen));

        return true;
    }

    /// <summary>
    /// World delta -> screen direction, following the camera rotation. No arrow when standing on it.
    /// </summary>
    private Vector2? ToArrow(Vector2 delta, float length)
    {
        if (length <= 0.5f)
            return null;

        var rotated = _eye.CurrentEye.Rotation.RotateVec(delta);
        return Vector2.Normalize(new Vector2(rotated.X, -rotated.Y));
    }

    private void AddShuttleLines(TimeSpan now, Color accent)
    {
        if (_hud.ShuttleState is not { } shuttle)
            return;

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
        var label = _hud.ShuttleTransport is { } transport
            ? Loc.GetString("mriya-contact-hud-shuttle-transport", ("name", transport))
            : Loc.GetString("mriya-contact-hud-shuttle");

        Add(ContactHudLines.Shuttle, label, value, accent);

        if (_hud.ShuttleNeeded > 0)
        {
            var aboard = shuttle == ContactShuttleState.Ready
                ? _hud.ShuttleAboard.ToString()
                : $"{_hud.ShuttleAboard}/{_hud.ShuttleNeeded}";

            var color = shuttle != ContactShuttleState.Ready && _hud.ShuttleAboard >= _hud.ShuttleNeeded ? accent : White;
            Add(ContactHudLines.Aboard, Loc.GetString("mriya-contact-hud-shuttle-aboard"), aboard, color);
        }

        if (_hud.ShuttleGunship is { } gunship)
            Add(ContactHudLines.Gunship, gunship, Loc.GetString("mriya-contact-hud-shuttle-manual").ToUpperInvariant(), Teal);
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
            time = TimeSpan.Zero;

        return $"{(int) time.TotalMinutes:00}:{time.Seconds:00}";
    }

    #endregion
}