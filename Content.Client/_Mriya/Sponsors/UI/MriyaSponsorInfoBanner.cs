using Content.Client.Administration.UI.CustomControls;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Mriya.Sponsors.UI;

/// <summary>
/// A banner button that opens the sponsor list window.
/// </summary>
public sealed class MriyaSponsorInfoBanner : BoxContainer
{
    public MriyaSponsorInfoBanner()
    {
        var buttons = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal
        };
        AddChild(buttons);

        var creditsButton = new CommandButton { Text = Loc.GetString("sponsors-open-panel") };
        creditsButton.Command = "sponsorwindow";
        buttons.AddChild(creditsButton);
    }
}

