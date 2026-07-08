using Content.Client.Eui;
using Content.Client.Stylesheets;
using Content.Shared.Eui;
using Content.Shared.Mriya.Sponsors;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using System.Linq;
using System.Numerics;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Mriya.Sponsors.UI;

[UsedImplicitly]
public sealed partial class SponsorListEui : BaseEui
{
    private readonly Menu _menu;

    public SponsorListEui()
    {
        IoCManager.InjectDependencies(this);

        _menu = new Menu();
        _menu.OnClose += CloseEverything;
    }

    public override void Closed()
    {
        base.Closed();
        SendMessage(new CloseEuiMessage());
        CloseEverything();
    }

    private void CloseEverything()
    {
        _menu.Close();
    }

    public override void Opened()
    {
        _menu.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not SponsorListEuiState s)
            return;

        _menu.SponsorsList.RemoveAllChildren();

        if (s.Sponsors == null || s.Sponsors.Count == 0)
        {
            _menu.SponsorsList.AddChild(new Label
            {
                Text = Loc.GetString("sponsors-eui-loading-or-empty"),
                StyleClasses = { StyleClass.LabelHeading }
            });
            return;
        }

        var groupedSponsors = s.Sponsors.GroupBy(sp => sp.TopRankName);

        foreach (var group in groupedSponsors)
        {
            var rankName = group.Key;

            var groupColor = group.First().TopRankColor;

            var header = new Label
            {
                Text = rankName,
                FontColorOverride = groupColor,
                StyleClasses = { StyleClass.LabelHeading }
            };

            if (_menu.SponsorsList.ChildCount > 0)
            {
                header.Margin = new Thickness(0, 10, 0, 0);
            }

            _menu.SponsorsList.AddChild(header);

            foreach (var sponsor in group)
            {
                var nameLabel = new Label
                {
                    Text = sponsor.UserName,
                    FontColorOverride = groupColor
                };

                _menu.SponsorsList.AddChild(nameLabel);
            }
        }
    }

    private sealed class Menu : DefaultWindow
    {
        public readonly BoxContainer SponsorsList;

        public Menu()
        {
            Title = Loc.GetString("sponsors-eui-menu-title");
            SponsorsList = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true
            };

            var scroll = new ScrollContainer
            {
                VerticalExpand = true,
                Children = { SponsorsList }
            };
            Contents.AddChild(scroll);
        }

        protected override Vector2 ContentsMinimumSize => new Vector2(400, 500);
    }
}
