using Content.Client.Eui;
using Content.Client.Stylesheets;
using Content.Shared.Eui;
using Content.Shared.Mriya.Sponsors;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;
using static Content.Shared.Mriya.Sponsors.AdminSponsorsEuiMsg;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.Mriya.Sponsors.UI;

[UsedImplicitly]
public sealed partial class AdminSponsorsEui : BaseEui
{
    private readonly Menu _menu;
    private readonly List<DefaultWindow> _subWindows = new();

    private Dictionary<int, AdminSponsorsEuiState.SponsorRankData> _ranks = new();

    public AdminSponsorsEui()
    {
        IoCManager.InjectDependencies(this);

        _menu = new Menu(this);
        _menu.AddSponsorButton.OnPressed += _ => OpenEditWindow(null);
        _menu.AddSponsorRankButton.OnPressed += _ => OpenRankEditWindow(null);
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
        foreach (var subWindow in _subWindows.ToArray())
        {
            subWindow.Close();
        }
        _menu.Close();
    }

    private void OpenEditWindow(AdminSponsorsEuiState.SponsorData? data)
    {
        var window = new EditSponsorWindow(this, data);
        window.SaveButton.OnPressed += _ => SaveSponsorPressed(window);
        window.OpenCentered();
        window.OnClose += () => _subWindows.Remove(window);

        if (data != null && window.RemoveButton != null)
        {
            window.RemoveButton.OnPressed += _ =>
            {
                SendMessage(new RemoveSponsor { UserId = window.SourceData!.Value.UserId });
                window.Close();
            };
        }

        _subWindows.Add(window);
    }

    private void OpenRankEditWindow(KeyValuePair<int, AdminSponsorsEuiState.SponsorRankData>? rank)
    {
        var window = new EditSponsorRankWindow(this, rank);
        window.SaveButton.OnPressed += _ => SaveSponsorRankPressed(window);
        window.OpenCentered();
        window.OnClose += () => _subWindows.Remove(window);

        if (rank != null && window.RemoveButton != null)
        {
            window.RemoveButton.OnPressed += _ =>
            {
                SendMessage(new RemoveSponsorRank { Id = window.SourceId!.Value });
                window.Close();
            };
        }

        _subWindows.Add(window);
    }

    private void SaveSponsorPressed(EditSponsorWindow popup)
    {
        var selectedRanks = popup.RankCheckboxes
            .Where(kv => kv.Value.Pressed)
            .Select(kv => kv.Key)
            .ToList();

        if (popup.SourceData is { } src)
        {
            SendMessage(new UpdateSponsor
            {
                UserId = src.UserId,
                RankIds = selectedRanks,
                SelectedGhostColor = string.IsNullOrWhiteSpace(popup.GhostColorEdit.Text) ? null : popup.GhostColorEdit.Text,
                SelectedOocColor = string.IsNullOrWhiteSpace(popup.OocColorEdit.Text) ? null : popup.OocColorEdit.Text
            });
        }
        else
        {
            DebugTools.AssertNotNull(popup.NameEdit);
            SendMessage(new AddSponsor
            {
                UserNameOrId = popup.NameEdit!.Text,
                RankIds = selectedRanks
            });
        }

        popup.Close();
    }

    private void SaveSponsorRankPressed(EditSponsorRankWindow popup)
    {
        var tagsList = popup.TagsEdit.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (popup.SourceId is { } src)
        {
            SendMessage(new UpdateSponsorRank
            {
                Id = src,
                Name = popup.NameEdit.Text,
                DefaultColor = popup.ColorEdit.Color,
                Priority = popup.PrioritySpin.Value,
                ShowInSponsorWindow = popup.ShowInWindowCheck.Pressed,
                CanSetGhostColor = popup.CanGhostCheck.Pressed,
                CanSetOocColor = popup.CanOocCheck.Pressed,
                DefaultGhostColor = string.IsNullOrWhiteSpace(popup.DefaultGhostEdit.Text) ? null : popup.DefaultGhostEdit.Text,
                DefaultOocColor = string.IsNullOrWhiteSpace(popup.DefaultOocEdit.Text) ? null : popup.DefaultOocEdit.Text,
                Tags = tagsList
            });
        }
        else
        {
            SendMessage(new AddSponsorRank
            {
                Name = popup.NameEdit.Text,
                DefaultColor = popup.ColorEdit.Color,
                Priority = popup.PrioritySpin.Value,
                ShowInSponsorWindow = popup.ShowInWindowCheck.Pressed,
                CanSetGhostColor = popup.CanGhostCheck.Pressed,
                CanSetOocColor = popup.CanOocCheck.Pressed,
                DefaultGhostColor = string.IsNullOrWhiteSpace(popup.DefaultGhostEdit.Text) ? null : popup.DefaultGhostEdit.Text,
                DefaultOocColor = string.IsNullOrWhiteSpace(popup.DefaultOocEdit.Text) ? null : popup.DefaultOocEdit.Text,
                Tags = tagsList
            });
        }

        popup.Close();
    }

    public override void Opened()
    {
        _menu.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not AdminSponsorsEuiState s || s.IsLoading)
            return;

        _ranks = s.SponsorRanks;

        _menu.SponsorsList.RemoveAllChildren();

        var groupedSponsors = new Dictionary<int, List<AdminSponsorsEuiState.SponsorData>>();

        foreach (var sponsor in s.Sponsors)
        {
            int topRankId = -1;
            int minPriority = int.MaxValue;

            foreach (var rId in sponsor.RankIds)
            {
                if (_ranks.TryGetValue(rId, out var rankData))
                {
                    if (rankData.Priority < minPriority)
                    {
                        minPriority = rankData.Priority;
                        topRankId = rId;
                    }
                }
            }

            if (!groupedSponsors.ContainsKey(topRankId))
                groupedSponsors[topRankId] = new List<AdminSponsorsEuiState.SponsorData>();

            groupedSponsors[topRankId].Add(sponsor);
        }

        var sortedRankIds = groupedSponsors.Keys
            .OrderBy(k => k == -1 ? 1 : 0)
            .ThenBy(k => k == -1 ? 0 : _ranks[k].Priority)
            .ToList();

        // 3. Відмальовуємо групи
        foreach (var rankId in sortedRankIds)
        {
            var group = groupedSponsors[rankId];
            string headerText;
            Color headerColor;

            if (rankId != -1 && _ranks.TryGetValue(rankId, out var rankDef))
            {
                headerText = rankDef.Name;
                headerColor = rankDef.DefaultColor;
            }
            else
            {
                headerText = Loc.GetString("sponsors-eui-admin-no-rank-group");
                headerColor = Color.DarkGray;
            }

            var headerLabel = new Label
            {
                Text = headerText,
                FontColorOverride = headerColor,
                StyleClasses = { StyleNano.StyleClassLabelHeading}
            };

            if (_menu.SponsorsList.ChildCount > 0)
                headerLabel.Margin = new Thickness(0, 15, 0, 5);
            else
                headerLabel.Margin = new Thickness(0, 0, 0, 5);

            _menu.SponsorsList.AddChild(headerLabel);

            var grid = new GridContainer { Columns = 3, HorizontalExpand = true };
            _menu.SponsorsList.AddChild(grid);

            foreach (var sponsor in group.OrderBy(d => d.UserName))
            {
                var name = sponsor.UserName ?? sponsor.UserId.ToString();
                var nameLabel = new Label { Text = name, FontColorOverride = headerColor };
                grid.AddChild(nameLabel);

                string ranksText;
                if (sponsor.RankIds.Count > 0)
                {
                    var rankNames = sponsor.RankIds
                        .Where(id => _ranks.ContainsKey(id))
                        .OrderBy(id => _ranks[id].Priority)
                        .Select(id => _ranks[id].Name);

                    ranksText = string.Join(", ", rankNames);
                }
                else
                {
                    ranksText = Loc.GetString("sponsors-eui-edit-no-rank-text");
                }

                var rankControl = new Label
                {
                    Text = ranksText,
                    HorizontalAlignment = Control.HAlignment.Center,
                    HorizontalExpand = true,
                    FontColorOverride = headerColor
                };
                grid.AddChild(rankControl);

                var editButton = new Button { Text = Loc.GetString("sponsors-eui-edit-title-button") };
                editButton.OnPressed += _ => OpenEditWindow(sponsor);
                grid.AddChild(editButton);
            }
        }

        _menu.SponsorsRanksList.RemoveAllChildren();
        foreach (var kv in s.SponsorRanks.OrderBy(r => r.Value.Priority))
        {
            var rank = kv.Value;
            var infoText = Loc.GetString("sponsors-eui-admin-rank-info", ("name", rank.Name), ("priority", rank.Priority));

            _menu.SponsorsRanksList.AddChild(new Label { Text = infoText, FontColorOverride = rank.DefaultColor });

            var editButton = new Button { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-button") };
            editButton.OnPressed += _ => OpenRankEditWindow(kv);
            _menu.SponsorsRanksList.AddChild(editButton);
        }
    }

    private sealed class Menu : DefaultWindow
    {
        public readonly BoxContainer SponsorsList;
        public readonly GridContainer SponsorsRanksList;
        public readonly Button AddSponsorButton;
        public readonly Button AddSponsorRankButton;

        public Menu(AdminSponsorsEui ui)
        {
            Title = Loc.GetString("sponsors-eui-menu-title");

            var tab = new TabContainer();

            AddSponsorButton = new Button { Text = Loc.GetString("sponsors-eui-menu-add-sponsor-button"), HorizontalAlignment = HAlignment.Right };
            AddSponsorRankButton = new Button { Text = Loc.GetString("sponsors-eui-menu-add-sponsor-rank-button"), HorizontalAlignment = HAlignment.Right };

            SponsorsList = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true };
            var adminVBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children = { new ScrollContainer { VerticalExpand = true, Children = { SponsorsList } }, AddSponsorButton }
            };
            TabContainer.SetTabTitle(adminVBox, Loc.GetString("sponsors-eui-menu-sponsors-tab-title"));

            SponsorsRanksList = new GridContainer { Columns = 2, VerticalExpand = true };
            var rankVBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children = { new ScrollContainer { VerticalExpand = true, Children = { SponsorsRanksList } }, AddSponsorRankButton }
            };
            TabContainer.SetTabTitle(rankVBox, Loc.GetString("sponsors-eui-menu-sponsor-ranks-tab-title"));

            tab.AddChild(adminVBox);
            tab.AddChild(rankVBox);
            Contents.AddChild(tab);
        }

        protected override Vector2 ContentsMinimumSize => new Vector2(600, 400);
    }

    private sealed class EditSponsorWindow : DefaultWindow
    {
        public readonly AdminSponsorsEuiState.SponsorData? SourceData;
        public readonly LineEdit? NameEdit;
        public readonly Dictionary<int, CheckBox> RankCheckboxes = new();

        public readonly LineEdit GhostColorEdit;
        public readonly LineEdit OocColorEdit;

        public readonly Button SaveButton;
        public readonly Button? RemoveButton;

        public EditSponsorWindow(AdminSponsorsEui ui, AdminSponsorsEuiState.SponsorData? data)
        {
            MinSize = new Vector2(400, 500);
            SourceData = data;

            Control nameControl;
            if (data is { } dat)
            {
                var name = dat.UserName ?? dat.UserId.ToString();
                Title = Loc.GetString("sponsors-eui-edit-sponsor-window-title", ("name", name));
                nameControl = new Label { Text = name };
            }
            else
            {
                Title = Loc.GetString("sponsors-eui-menu-add-sponsor-button");
                nameControl = NameEdit = new LineEdit { PlaceHolder = Loc.GetString("sponsors-eui-edit-sponsor-window-name-placeholder") };
            }

            var ranksVBox = new BoxContainer { Orientation = LayoutOrientation.Vertical };
            foreach (var (rId, rank) in ui._ranks.OrderBy(r => r.Value.Priority))
            {
                var cb = new CheckBox
                {
                    Text = rank.Name,
                    Pressed = data?.RankIds.Contains(rId) ?? false
                };
                RankCheckboxes[rId] = cb;
                ranksVBox.AddChild(cb);
            }

            GhostColorEdit = new LineEdit { PlaceHolder = Loc.GetString("sponsors-eui-edit-sponsor-window-ghost-color-placeholder"), Text = data?.SelectedGhostColor ?? "" };
            OocColorEdit = new LineEdit { PlaceHolder = Loc.GetString("sponsors-eui-edit-sponsor-window-ooc-color-placeholder"), Text = data?.SelectedOocColor ?? "" };

            SaveButton = new Button { Text = Loc.GetString("sponsors-eui-edit-sponsor-window-save-button"), HorizontalAlignment = HAlignment.Right };

            var bottomButtons = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
            if (data != null)
            {
                RemoveButton = new Button { Text = Loc.GetString("sponsors-eui-edit-sponsor-window-remove-flag-button") };
                bottomButtons.AddChild(RemoveButton);
            }
            bottomButtons.AddChild(SaveButton);

            Contents.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Children =
                {
                    nameControl,
                    new Label { Text = Loc.GetString("sponsors-eui-edit-sponsor-window-ranks-label"), StyleClasses = { StyleNano.StyleClassLabelHeading }, Margin = new Thickness(0, 10, 0, 5) },
                    new ScrollContainer { VerticalExpand = true, MinSize = new Vector2(0, 150), Children = { ranksVBox } },
                    new Label { Text = Loc.GetString("sponsors-eui-edit-sponsor-window-override-colors-label"), Margin = new Thickness(0, 10, 0, 5) },
                    GhostColorEdit,
                    OocColorEdit,
                    new Control { VerticalExpand = true },
                    bottomButtons
                }
            });
        }
    }

    private sealed class EditSponsorRankWindow : DefaultWindow
    {
        public readonly int? SourceId;
        public readonly LineEdit NameEdit;
        public readonly ColorSelectorSliders ColorEdit;

        public readonly SpinBox PrioritySpin;
        public readonly CheckBox ShowInWindowCheck;
        public readonly CheckBox CanGhostCheck;
        public readonly CheckBox CanOocCheck;
        public readonly LineEdit DefaultGhostEdit;
        public readonly LineEdit DefaultOocEdit;
        public readonly LineEdit TagsEdit;

        public readonly Button SaveButton;
        public readonly Button? RemoveButton;

        public EditSponsorRankWindow(AdminSponsorsEui ui, KeyValuePair<int, AdminSponsorsEuiState.SponsorRankData>? data)
        {
            Title = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-title");
            MinSize = new Vector2(500, 600);
            SourceId = data?.Key;
            var rank = data?.Value;

            NameEdit = new LineEdit { PlaceHolder = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-name-placeholder"), Text = rank?.Name ?? "" };
            ColorEdit = new ColorSelectorSliders { Color = rank?.DefaultColor ?? Color.White, SelectorType = ColorSelectorSliders.ColorSelectorType.Hsv };

            PrioritySpin = new SpinBox { Value = rank?.Priority ?? 0, ToolTip = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-priority-tooltip") };
            ShowInWindowCheck = new CheckBox { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-show-in-window-check"), Pressed = rank?.ShowInSponsorWindow ?? true };

            CanGhostCheck = new CheckBox { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-can-ghost-check"), Pressed = rank?.CanSetGhostColor ?? false };
            DefaultGhostEdit = new LineEdit { PlaceHolder = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-default-ghost-placeholder"), Text = rank?.DefaultGhostColor ?? "" };

            CanOocCheck = new CheckBox { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-can-ooc-check"), Pressed = rank?.CanSetOocColor ?? false };
            DefaultOocEdit = new LineEdit { PlaceHolder = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-default-ooc-placeholder"), Text = rank?.DefaultOocColor ?? "" };

            TagsEdit = new LineEdit { PlaceHolder = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-tags-placeholder"), Text = rank != null ? string.Join(", ", rank.Value.Tags) : "" };

            SaveButton = new Button { Text = Loc.GetString("sponsors-eui-menu-save-sponsor-rank-button"), HorizontalAlignment = HAlignment.Right };
            var bottomButtons = new BoxContainer { Orientation = LayoutOrientation.Horizontal };
            if (data != null)
            {
                RemoveButton = new Button { Text = Loc.GetString("sponsors-eui-menu-remove-sponsor-rank-button") };
                bottomButtons.AddChild(RemoveButton);
            }
            bottomButtons.AddChild(SaveButton);

            var contentBox = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                SeparationOverride = 5,
                Children =
                {
                    new Label { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-basic-settings-label"), StyleClasses = { StyleNano.StyleClassLabelHeading } },
                    NameEdit,
                    new BoxContainer { Orientation = LayoutOrientation.Horizontal, Children = { new Label{Text= Loc.GetString("sponsors-eui-edit-sponsor-rank-window-priority-label")}, PrioritySpin } },
                    ShowInWindowCheck,
                    TagsEdit,

                    new Label { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-base-color-label"), Margin = new Thickness(0, 10, 0, 0) },
                    ColorEdit,

                    new Label { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-ghost-settings-label"), StyleClasses = { StyleNano.StyleClassLabelHeading }, Margin = new Thickness(0, 10, 0, 0) },
                    CanGhostCheck,
                    DefaultGhostEdit,

                    new Label { Text = Loc.GetString("sponsors-eui-edit-sponsor-rank-window-ooc-settings-label"), StyleClasses = { StyleNano.StyleClassLabelHeading }, Margin = new Thickness(0, 10, 0, 0) },
                    CanOocCheck,
                    DefaultOocEdit,

                    new Control { MinSize = new Vector2(0, 15) }, // Spacer
                    bottomButtons
                }
            };

            Contents.AddChild(new ScrollContainer { VerticalExpand = true, Children = { contentBox } });
        }
    }
}
