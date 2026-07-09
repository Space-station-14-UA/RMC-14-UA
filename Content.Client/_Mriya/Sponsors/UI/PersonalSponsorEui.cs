using Content.Client.Eui;
using Content.Client.Stylesheets;
using Content.Shared.Eui;
using Content.Shared.Mriya.Sponsors;
using Content.Shared.Preferences.Loadouts;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Mriya.Sponsors.UI;

/// <summary>
/// EUI window for personal sponsor settings, allowing users to view their active tiers, customize colors, and view available sponsor items.
/// </summary>
[UsedImplicitly]
public sealed partial class PersonalSponsorEui : BaseEui
{
    private readonly PersonalSponsorWindow _window;

    public PersonalSponsorEui()
    {
        IoCManager.InjectDependencies(this);

        _window = new PersonalSponsorWindow(this);
        _window.OnClose += CloseEverything;
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        SendMessage(new CloseEuiMessage());
        CloseEverything();
    }

    private void CloseEverything()
    {
        _window.Close();
    }

    /// <summary>
    /// Handles the EUI state update based on the received <see cref="PersonalSponsorSettingsEuiState"/>.
    /// </summary>
    /// <param name="state">The new state.</param>
    public override void HandleState(EuiStateBase state)
    {
        if (state is not PersonalSponsorSettingsEuiState s)
            return;

        _window.UpdateState(s);
    }

    /// <summary>
    /// Sends a message to the server to save the user's personal sponsor settings, including ghost and OOC colors and selected rank IDs.
    /// </summary>
    /// <param name="ghostColor">The saved custom ghost color.</param>
    /// <param name="oocColor">The saved custom OOC chat color.</param>
    /// <param name="ghostRankId">The saved selected rank ID for the ghost color.</param>
    /// <param name="oocRankId">The saved selected rank ID for the OOC chat color.</param>
    public void SaveSettings(string? ghostColor, string? oocColor, int? ghostRankId, int? oocRankId)
    {
        SendMessage(new PersonalSponsorEuiMsg.UpdateSettings
        {
            NewGhostColor = ghostColor,
            NewOocColor = oocColor,
            SelectedGhostRankId = ghostRankId,
            SelectedOocRankId = oocRankId
        });
    }

    /// <summary>
    /// Represents the personal sponsor settings window, allowing users to view their active tiers, customize colors, and view available sponsor items.
    /// </summary>
    private sealed class PersonalSponsorWindow : DefaultWindow
    {
        private const int OptionNone = -1;
        private const int OptionCustom = -2;

        private readonly PersonalSponsorEui _eui;

        public readonly Label NameLabel;
        public readonly BoxContainer RanksContainer;

        public readonly Label GhostLabel;
        public readonly OptionButton GhostDropdown;
        public readonly ColorSelectorSliders GhostColorPicker;

        public readonly Label OocLabel;
        public readonly OptionButton OocDropdown;
        public readonly ColorSelectorSliders OocColorPicker;

        public readonly Button SaveButton;

        public PersonalSponsorWindow(PersonalSponsorEui eui)
        {
            _eui = eui;
            Title = Loc.GetString("sponsors-eui-personal-title");
            MinSize = new Vector2(450, 550);

            var playerManager = IoCManager.Resolve<IPlayerManager>();
            var playerName = playerManager.LocalSession?.Name ?? Loc.GetString("sponsors-eui-personal-player-fallback");

            GhostLabel = new Label
            {
                Text = Loc.GetString("sponsors-eui-personal-ghost-color"),
                StyleClasses = { StyleNano.StyleClassLabelHeading }
            };

            OocLabel = new Label
            {
                Text = Loc.GetString("sponsors-eui-personal-ooc-color"),
                StyleClasses = { StyleNano.StyleClassLabelHeading }
            };

            NameLabel = new Label
            {
                Text = playerName,
                HorizontalAlignment = HAlignment.Center,
                StyleClasses = { StyleNano.StyleClassLabelHeading }
            };

            var tabs = new TabContainer { VerticalExpand = true };

        // ranks tab
            RanksContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true,
                Margin = new Thickness(5)
            };

            var overviewScroll = new ScrollContainer
            {
                VerticalExpand = true,
                Children = { RanksContainer }
            };

            var overviewBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children = { new Label { Text = Loc.GetString("sponsors-eui-personal-active-ranks"), Margin = new Thickness(0, 0, 0, 10) }, overviewScroll }
            };
            TabContainer.SetTabTitle(overviewBox, Loc.GetString("sponsors-eui-personal-tab-overview"));

        // colors tab
            // Ghost color section
            GhostDropdown = new OptionButton { HorizontalExpand = true };
            GhostColorPicker = new ColorSelectorSliders
            {
                SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv,
                Visible = false
            };

            GhostColorPicker.OnColorChanged += color =>
            {
                GhostLabel.FontColorOverride = color;
            };

            GhostDropdown.OnItemSelected += args =>
            {
                GhostDropdown.SelectId(args.Id);
                GhostColorPicker.Visible = args.Id == OptionCustom;
            };

            var ghostSection = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(0, 0, 0, 15),
                Children =
                {
                    GhostLabel,
                    GhostDropdown,
                    GhostColorPicker
                }
            };

            // OOC color section
            OocDropdown = new OptionButton { HorizontalExpand = true };
            OocColorPicker = new ColorSelectorSliders
            {
                SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv,
                Visible = false
            };

            OocColorPicker.OnColorChanged += color =>
            {
                OocLabel.FontColorOverride = color;
            };

            OocDropdown.OnItemSelected += args =>
            {
                OocDropdown.SelectId(args.Id);
                OocColorPicker.Visible = args.Id == OptionCustom;
            };

            var oocSection = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children =
                {
                    OocLabel,
                    OocDropdown,
                    OocColorPicker
                }
            };

            var colorsScroll = new ScrollContainer
            {
                VerticalExpand = true,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        Children = { ghostSection, oocSection }
                    }
                }
            };
            TabContainer.SetTabTitle(colorsScroll, Loc.GetString("sponsors-eui-personal-tab-colors"));

        // loadouts tab
            var loadoutsScroll = new ScrollContainer { VerticalExpand = true };
            var loadoutsGrid = new GridContainer
            {
                HorizontalExpand = true,
                Columns = 4,
                Margin = new Thickness(10)
            };

            var protoManager = IoCManager.Resolve<IPrototypeManager>();
            var clientSponsorManager = IoCManager.Resolve<IClientSponsorManager>();

            foreach (var loadout in protoManager.EnumeratePrototypes<LoadoutPrototype>())
            {
                if (!string.IsNullOrEmpty(loadout.SponsorTag) && clientSponsorManager.HasTag(loadout.SponsorTag))
                {
                    var loadoutControl = new SponsorLoadoutControl(loadout);
                    loadoutsGrid.AddChild(loadoutControl);
                }
            }

            loadoutsScroll.AddChild(loadoutsGrid);
            TabContainer.SetTabTitle(loadoutsScroll, Loc.GetString("sponsors-eui-personal-tab-loadouts"));

            tabs.AddChild(overviewBox);
            tabs.AddChild(colorsScroll);
            tabs.AddChild(loadoutsScroll);

            SaveButton = new Button
            {
                Text = Loc.GetString("sponsors-eui-personal-save"),
                HorizontalAlignment = HAlignment.Right
            };
            SaveButton.OnPressed += OnSavePressed;

            Contents.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children = { NameLabel, tabs, SaveButton }
            });
        }

        /// <summary>
        /// Updates the window based on the received state.
        /// </summary>
        /// <param name="state">The new state.</param>
        public void UpdateState(PersonalSponsorSettingsEuiState state)
        {
            var topRank = state.AllowedRanks.FirstOrDefault();
            if (topRank.Name != null)
            {
                NameLabel.FontColorOverride = Color.FromHex(topRank.DefaultColor);
            }

            RanksContainer.RemoveAllChildren();
            if (state.AllowedRanks.Count == 0)
            {
                RanksContainer.AddChild(new Label { Text = Loc.GetString("sponsors-eui-personal-no-ranks"), StyleClasses = { StyleNano.StyleClassItalic} });
            }
            else
            {
                foreach (var rank in state.AllowedRanks)
                {
                    var color = Color.FromHex(rank.DefaultColor);

                    var panel = new PanelContainer
                    {
                        PanelOverride = new StyleBoxFlat
                        {
                            BackgroundColor = color.WithAlpha(0.15f),
                            BorderColor = color.WithAlpha(0.5f),
                            BorderThickness = new Thickness(1)
                        },
                        Margin = new Thickness(0, 0, 0, 5)
                    };

                    panel.AddChild(new Label
                    {
                        Text = rank.Name,
                        FontColorOverride = color,
                        Margin = new Thickness(10, 5),
                        HorizontalAlignment = HAlignment.Center
                    });

                    RanksContainer.AddChild(panel);
                }
            }

            PopulateDropdown(GhostDropdown, state.CanSetCustomGhostColor, state.AllowedRanks, true);
            PopulateDropdown(OocDropdown, state.CanSetCustomOocColor, state.AllowedRanks, false);

            if (state.SelectedGhostRankId != null)
            {
                GhostDropdown.SelectId(state.SelectedGhostRankId.Value);
                GhostColorPicker.Visible = false;
                var ghostColor = state.AllowedRanks.FirstOrDefault(x => x.Id == state.SelectedGhostRankId).FixedGhostColor;
                if (ghostColor != null)
                    GhostLabel.FontColorOverride = Color.FromHex(ghostColor);
                else
                    GhostLabel.FontColorOverride = null;
            }
            else if (!string.IsNullOrEmpty(state.CurrentGhostColor) && state.CanSetCustomGhostColor)
            {
                GhostDropdown.SelectId(OptionCustom);
                GhostColorPicker.Visible = true;
                GhostColorPicker.Color = Color.FromHex(state.CurrentGhostColor);
                GhostLabel.FontColorOverride = Color.FromHex(state.CurrentGhostColor);
            }
            else
            {
                GhostDropdown.SelectId(OptionNone);
                GhostColorPicker.Visible = false;
                GhostLabel.FontColorOverride = null;
            }

            if (state.SelectedOocRankId != null)
            {
                OocDropdown.SelectId(state.SelectedOocRankId.Value);
                OocColorPicker.Visible = false;
                var oocColor = state.AllowedRanks.FirstOrDefault(x => x.Id == state.SelectedOocRankId).FixedOocColor;
                if (oocColor != null)
                    OocLabel.FontColorOverride = Color.FromHex(oocColor);
                else
                    OocLabel.FontColorOverride = null;
            }
            else if (!string.IsNullOrEmpty(state.CurrentOocColor) && state.CanSetCustomOocColor)
            {
                OocDropdown.SelectId(OptionCustom);
                OocColorPicker.Visible = true;
                OocColorPicker.Color = Color.FromHex(state.CurrentOocColor);
                OocLabel.FontColorOverride = Color.FromHex(state.CurrentOocColor);
            }
            else
            {
                OocDropdown.SelectId(OptionNone);
                OocColorPicker.Visible = false;
                OocLabel.FontColorOverride = null;
            }
        }

        /// <summary>
        /// Generates a dropdown menu for color selection, including "None" and "Custom" options, as well as colors from available ranks.
        /// </summary>
        /// <param name="dropdown">The reference to the dropdown component.</param>
        /// <param name="canSetCustom">Indicates whether the user can set a custom color.</param>
        /// <param name="ranks">The available ranks for the user.</param>
        /// <param name="isGhost">Indicates whether this selection is for a ghost.</param>
        private void PopulateDropdown(OptionButton dropdown, bool canSetCustom, List<PersonalSponsorRankInfo> ranks, bool isGhost)
        {
            dropdown.Clear();
            dropdown.AddItem(Loc.GetString("sponsors-eui-personal-dropdown-none"), OptionNone);

            if (canSetCustom)
            {
                dropdown.AddItem(Loc.GetString("sponsors-eui-personal-dropdown-custom"), OptionCustom);
            }

            foreach (var rank in ranks)
            {
                var fixedColor = isGhost ? rank.FixedGhostColor : rank.FixedOocColor;
                if (!string.IsNullOrEmpty(fixedColor))
                {
                    dropdown.AddItem(Loc.GetString("sponsors-eui-personal-dropdown-rank", ("rank", rank.Name)), rank.Id);
                }
            }
        }

        private void OnSavePressed(BaseButton.ButtonEventArgs args)
        {
            int? ghostRankId = null;
            string? customGhostColor = null;

            if (GhostDropdown.SelectedId == OptionCustom)
                customGhostColor = GhostColorPicker.Color.ToHex();
            else if (GhostDropdown.SelectedId > 0)
                ghostRankId = GhostDropdown.SelectedId;

            int? oocRankId = null;
            string? customOocColor = null;

            if (OocDropdown.SelectedId == OptionCustom)
                customOocColor = OocColorPicker.Color.ToHex();
            else if (OocDropdown.SelectedId > 0)
                oocRankId = OocDropdown.SelectedId;

            _eui.SaveSettings(customGhostColor, customOocColor, ghostRankId, oocRankId);
        }
    }
}
