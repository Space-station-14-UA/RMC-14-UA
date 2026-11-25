using System.Numerics;
using Content.Client._CMU14.Announce;
using Content.Client.UserInterface.Controls;
using Content.Shared._CMU14.Announce;
using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client._Mriya.Rules.Contact.ContactRetro;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Standalone games window for slot machines / blackjack tables. Same terminal look and CRT as the store.
/// </summary>
public sealed class ContactCasinoWindow : FancyWindow
{
    private readonly PanelContainer _screen;

    public ContactCasinoView View { get; private set; } = default!;

    public ContactCasinoWindow()
    {
        MinSize = new Vector2(820, 600);

        _screen = new PanelContainer
        {
            PanelOverride = Box(Bg, margin: 10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var screen = _screen;

        var crtStack = new Control { HorizontalExpand = true, VerticalExpand = true };
        crtStack.AddChild(screen);
        crtStack.AddChild(new CRTOverlay
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
                VignetteIntensity = 0.25f,
                ShowChromaticAberration = false,
                GlowColor = Phosphor,
            },
        });
        ContentsContainer.AddChild(crtStack);
    }

    /// <summary>
    /// CreateWindow needs a parameterless constructor, so the games are added here.
    /// </summary>
    public void Initialize(ContactCasinoComponent casino, string title)
    {
        Title = title;
        View = new ContactCasinoView(casino);
        _screen.AddChild(View);
    }
}
