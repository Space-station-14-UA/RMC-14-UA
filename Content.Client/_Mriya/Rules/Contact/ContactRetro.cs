using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// The store's green terminal look, shared with the games (same palette / boxes / buttons as ContactStoreWindow).
/// </summary>
public static class ContactRetro
{
    public static readonly Color Bg = Color.FromHex("#0D0D0D");
    public static readonly Color Phosphor = Color.FromHex("#0AFE60");
    public static readonly Color Green = Color.FromHex("#087A25");
    public static readonly Color Teal = Color.FromHex("#047D44");
    public static readonly Color Deep = Color.FromHex("#0A4A26");
    public static readonly Color Dim = Color.FromHex("#1E3A28");
    public static readonly Color White = Color.FromHex("#FFFFFF");
    public static readonly Color Black = Color.FromHex("#000000");
    public static readonly Color Red = Color.FromHex("#FF4D4D");
    public static readonly Color Gold = Color.FromHex("#E0B040");

    public const string FontPath = "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf";

    public static StyleBoxFlat Box(Color bg, Color? border = null, float thickness = 0, float margin = 6)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = bg,
            BorderColor = border ?? bg,
            BorderThickness = new Thickness(thickness),
            ContentMarginLeftOverride = margin,
            ContentMarginRightOverride = margin,
            ContentMarginTopOverride = margin / 2,
            ContentMarginBottomOverride = margin / 2,
        };
    }

    public static Label Text(string text, Color color, Font font, bool expand = false)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = color,
            FontOverride = font,
            HorizontalExpand = expand,
            ClipText = expand,
        };
    }

    public static BoxContainer HBox(int separation = 6)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    public static BoxContainer VBox(int separation = 4)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    /// <summary>
    /// ">LABEL" button. Normal: green outline. Hover: dark green fill. Selected / pressed: white. Disabled: dim.
    /// </summary>
    public sealed class RetroButton : Button
    {
        private bool _selected;
        private readonly bool _ready;

        public bool Selected
        {
            get => _selected;
            set
            {
                _selected = value;
                Apply();
            }
        }

        public RetroButton(string text, Font font)
        {
            Text = text;
            Label.FontOverride = font;
            _ready = true;
            Apply();
        }

        protected override void DrawModeChanged()
        {
            base.DrawModeChanged();
            Apply();
        }

        private void Apply()
        {
            if (!_ready)
                return;

            Color bg, border, fg;
            if (DrawMode == DrawModeEnum.Disabled)
            {
                bg = Bg;
                border = Dim;
                fg = Dim;
            }
            else if (_selected || DrawMode == DrawModeEnum.Pressed)
            {
                bg = White;
                border = White;
                fg = Black;
            }
            else if (DrawMode == DrawModeEnum.Hover)
            {
                bg = Deep;
                border = Phosphor;
                fg = Phosphor;
            }
            else
            {
                bg = Bg;
                border = Green;
                fg = Phosphor;
            }

            StyleBoxOverride = Box(bg, border, 2, 8);
            Label.FontColorOverride = fg;
        }
    }
}
