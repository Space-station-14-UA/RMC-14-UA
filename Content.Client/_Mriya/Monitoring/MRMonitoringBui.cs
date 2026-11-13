using Content.Shared._Mriya.Monitoring;
using Content.Shared._RMC14.UserInterface;
using Content.Shared.Mobs;
using JetBrains.Annotations;
using Content.Client._RMC14.UserInterface;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Monitoring;

[UsedImplicitly]
public sealed class MRMonitoringBui : BoundUserInterface, IRefreshableBui
{
    private enum Tab
    {
        Roster,
        Tracking,
    }

    private static readonly string[] DirectionKeys =
    {
        "mr-monitoring-direction-n",
        "mr-monitoring-direction-ne",
        "mr-monitoring-direction-e",
        "mr-monitoring-direction-se",
        "mr-monitoring-direction-s",
        "mr-monitoring-direction-sw",
        "mr-monitoring-direction-w",
        "mr-monitoring-direction-nw",
    };

    private MRMonitoringWindow? _window;
    private Tab _tab = Tab.Roster;

    public MRMonitoringBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<MRMonitoringWindow>();
        _window.Title = Loc.GetString("mr-monitoring-title");
        _window.RosterTabButton.Text = Loc.GetString("mr-monitoring-tab-roster");
        _window.TrackingTabButton.Text = Loc.GetString("mr-monitoring-tab-tracking");
        _window.BackButton.Text = Loc.GetString("mr-monitoring-back");

        _window.RosterTabButton.OnPressed += _ => SetTab(Tab.Roster);
        _window.TrackingTabButton.OnPressed += _ => SetTab(Tab.Tracking);
        _window.BackButton.OnPressed += _ => SetTab(Tab.Roster);

        SetTab(Tab.Roster);
        Refresh();
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out MRMonitoringComponent? component))
            return;

        _window.Battery.Charge = component.BatteryPercent / 100f;
        _window.BatteryLabel.Text = Loc.GetString("mr-monitoring-battery-percent",
            ("percent", (int) MathF.Round(component.BatteryPercent)));

        MRMonitoringMarine? selected = null;
        if (component.Selected is { } selectedNet)
        {
            foreach (var candidate in component.Marines)
            {
                if (candidate.Id == selectedNet)
                {
                    selected = candidate;
                    break;
                }
            }
        }

        var hasTarget = selected.HasValue;
        var hasPosition = hasTarget && component.Distance >= 0f;
        _window.Signal.Bars = hasTarget ? GetSignalBars(component.Distance) : 4;

        if (selected is { } marine)
        {
            _window.TargetName.Text = marine.Name;
            _window.TargetState.Text = GetStateText(marine.State);
            _window.TargetState.FontColorOverride = GetStateColor(marine.State);
            _window.TargetArea.Text = marine.Area;
        }
        else
        {
            _window.TargetName.Text = Loc.GetString("mr-monitoring-no-selection");
            _window.TargetState.Text = string.Empty;
            _window.TargetArea.Text = string.Empty;
        }

        _window.Compass.HasTarget = hasPosition;
        _window.Compass.Angle = component.DirectionAngle;
        _window.DirectionLabel.Text = hasPosition
            ? GetDirectionText(component.DirectionAngle)
            : Loc.GetString("mr-monitoring-unknown");
        _window.DistanceLabel.Text = hasPosition
            ? Loc.GetString("mr-monitoring-distance-meters", ("meters", (int) MathF.Round(component.Distance)))
            : Loc.GetString("mr-monitoring-unknown");

        UpdateRoster(component);
    }

    private void UpdateRoster(MRMonitoringComponent component)
    {
        if (_window == null)
            return;

        var container = _window.RosterContainer;
        var count = component.Marines.Count;
        for (var i = 0; i < count; i++)
        {
            MRMonitoringRosterRow row;
            if (i < container.ChildCount)
            {
                row = (MRMonitoringRosterRow) container.GetChild(i);
            }
            else
            {
                row = new MRMonitoringRosterRow();
                row.OnPressed += _ => OnRosterRowPressed(row);
                container.AddChild(row);
            }

            var marine = component.Marines[i];
            row.Marine = marine.Id;
            row.SetData(marine.Name, GetStateText(marine.State), marine.Area, GetStateColor(marine.State));
            row.IsSelected = component.Selected == marine.Id;
        }

        container.RemoveChildrenAfter(count);
        _window.RosterEmpty.Visible = count == 0;
    }

    private void OnRosterRowPressed(MRMonitoringRosterRow row)
    {
        if (row.Marine == default)
            return;

        SendPredictedMessage(new MRMonitoringSelectMarineMsg(row.Marine));
        SetTab(Tab.Tracking);
    }

    private void SetTab(Tab tab)
    {
        _tab = tab;

        if (_window == null)
            return;

        _window.RosterPage.Visible = _tab == Tab.Roster;
        _window.TrackingPage.Visible = _tab == Tab.Tracking;
        _window.RosterTabButton.IsSelected = _tab == Tab.Roster;
        _window.TrackingTabButton.IsSelected = _tab == Tab.Tracking;
    }

    private static int GetSignalBars(float distance)
    {
        if (distance < 0f)
            return 0;

        if (distance <= 25f)
            return 4;

        if (distance <= 50f)
            return 3;

        if (distance <= 100f)
            return 2;

        return 1;
    }

    private static string GetDirectionText(float angle)
    {
        var degrees = (90f - angle * (180f / MathF.PI) + 360f) % 360f;
        var index = (int) MathF.Round(degrees / 45f) % 8;
        return $"{Loc.GetString(DirectionKeys[index])} · {degrees:0}°";
    }

    private static string GetStateText(MobState state)
    {
        return state switch
        {
            MobState.Alive => Loc.GetString("mr-monitoring-state-alive"),
            MobState.Critical => Loc.GetString("mr-monitoring-state-critical"),
            MobState.Dead => Loc.GetString("mr-monitoring-state-dead"),
            _ => Loc.GetString("mr-monitoring-state-invalid"),
        };
    }

    private static Color GetStateColor(MobState state)
    {
        return state switch
        {
            MobState.Alive => Color.FromHex("#6CFF9A"),
            MobState.Critical => Color.FromHex("#FFD166"),
            MobState.Dead => Color.FromHex("#FF6B6B"),
            _ => Color.FromHex("#8FA89A"),
        };
    }
}
