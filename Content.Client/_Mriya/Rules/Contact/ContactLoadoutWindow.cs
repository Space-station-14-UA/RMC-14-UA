using System.Linq;
using System.Numerics;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Attachable.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Contact skills/loadout window. Tab 1: point-buy skills. Tab 2: one pick per gear category + the weapon's attachments.
/// Apply saves the preset and sends it; the server re-checks everything.
/// </summary>
public sealed class ContactLoadoutWindow : DefaultWindow
{
    private static readonly ContactLoadoutCategory[] GearCategories =
    {
        ContactLoadoutCategory.Weapon,
        ContactLoadoutCategory.Uniform,
        ContactLoadoutCategory.Armor,
        ContactLoadoutCategory.Helmet,
    };

    private readonly ContactLoadoutSystem _system;
    private readonly IPrototypeManager _prototypes;
    private readonly IComponentFactory _factory;

    private readonly Label _points = new();
    private readonly BoxContainer _skillList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _gearList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _attachmentList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _shipOnly = new() { Visible = false };
    private readonly Button _apply = new();

    private readonly Dictionary<string, int> _skills = new();
    private readonly Dictionary<ContactLoadoutCategory, string> _items = new();
    private readonly Dictionary<string, string> _attachments = new();
    private int _maxPoints;

    private List<EntityPrototype>? _attachables;

    public ContactLoadoutWindow(ContactLoadoutSystem system, IPrototypeManager prototypes, IComponentFactory factory)
    {
        _system = system;
        _prototypes = prototypes;
        _factory = factory;

        Title = Loc.GetString("mriya-contact-loadout-title");
        MinSize = new Vector2(540, 560);

        var tabs = new TabContainer { VerticalExpand = true };

        var skillsTab = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        skillsTab.AddChild(_points);
        skillsTab.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _skillList },
        });

        var gearTab = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        gearTab.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Children =
                    {
                        _gearList,
                        new Label { Text = Loc.GetString("mriya-contact-loadout-attachments"), Margin = new Thickness(0, 8, 0, 2) },
                        _attachmentList,
                    },
                },
            },
        });

        tabs.AddChild(skillsTab);
        tabs.AddChild(gearTab);
        TabContainer.SetTabTitle(skillsTab, Loc.GetString("mriya-contact-loadout-tab-skills"));
        TabContainer.SetTabTitle(gearTab, Loc.GetString("mriya-contact-loadout-tab-gear"));

        _shipOnly.Text = Loc.GetString("mriya-contact-loadout-ship-only");
        _apply.Text = Loc.GetString("mriya-contact-loadout-apply");
        _apply.OnPressed += _ =>
        {
            _system.Apply(new ContactLoadoutApplyEvent
            {
                Skills = new Dictionary<string, int>(_skills),
                Items = new Dictionary<ContactLoadoutCategory, string>(_items),
                Attachments = new Dictionary<string, string>(_attachments),
            });
            Close();
        };

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children = { tabs, _shipOnly, _apply },
        });
    }

    /// <summary>
    /// Fill from the saved preset; levels above what the points allow are dropped back down.
    /// </summary>
    public void Load(ContactLoadoutApplyEvent preset, int points, bool canApply)
    {
        _skills.Clear();
        _items.Clear();
        _attachments.Clear();

        foreach (var skill in SortedSkills())
        {
            _skills[skill.ID] = ContactLoadoutHelpers.ClampLevel(skill, preset.Skills.GetValueOrDefault(skill.ID, skill.BaseLevel));
        }

        foreach (var (category, id) in preset.Items)
        {
            if (_prototypes.TryIndex<ContactLoadoutItemPrototype>(id, out var item) && item.Category == category)
                _items[category] = id;
        }

        foreach (var (slot, id) in preset.Attachments)
        {
            _attachments[slot] = id;
        }

        _maxPoints = points;
        while (Spent() > _maxPoints && LowerMostExpensive())
        {
        }

        _apply.Disabled = !canApply;
        _shipOnly.Visible = !canApply;

        RebuildSkills();
        RebuildGear();
        RebuildAttachments();
    }

    public void SetPoints(int points)
    {
        _maxPoints = points;
        RebuildSkills();
    }

    #region Skills

    private IEnumerable<ContactSkillPrototype> SortedSkills()
    {
        return _prototypes.EnumeratePrototypes<ContactSkillPrototype>().OrderBy(s => s.Order).ThenBy(s => s.ID);
    }

    private int Spent()
    {
        return ContactLoadoutHelpers.TotalCost(_prototypes, _skills);
    }

    private bool LowerMostExpensive()
    {
        foreach (var skill in SortedSkills().OrderByDescending(s => ContactLoadoutHelpers.SkillCost(s, _skills[s.ID])))
        {
            if (_skills[skill.ID] <= skill.BaseLevel)
                continue;

            _skills[skill.ID]--;
            return true;
        }

        return false;
    }

    private void RebuildSkills()
    {
        var spent = Spent();
        _points.Text = Loc.GetString("mriya-contact-loadout-points", ("left", _maxPoints - spent), ("total", _maxPoints));

        _skillList.RemoveAllChildren();
        foreach (var skill in SortedSkills())
        {
            var id = skill.ID;
            var level = _skills[id];
            var next = ContactLoadoutHelpers.NextCost(skill, level);

            var minus = new Button { Text = "-", Disabled = level <= skill.BaseLevel, MinWidth = 28 };
            minus.OnPressed += _ =>
            {
                _skills[id] = level - 1;
                RebuildSkills();
            };

            var plus = new Button { Text = "+", Disabled = level >= skill.MaxLevel || spent + next > _maxPoints, MinWidth = 28 };
            plus.OnPressed += _ =>
            {
                _skills[id] = level + 1;
                RebuildSkills();
            };

            var name = new Label
            {
                Text = Loc.GetString(skill.Name),
                HorizontalExpand = true,
                ToolTip = skill.Description is { } desc ? Loc.GetString(desc) : null,
            };

            _skillList.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 1),
                Children =
                {
                    name,
                    minus,
                    new Label { Text = $"{level}/{skill.MaxLevel}", MinWidth = 40, Align = Label.AlignMode.Center },
                    plus,
                    new Label
                    {
                        Text = level >= skill.MaxLevel ? string.Empty : Loc.GetString("mriya-contact-loadout-cost", ("cost", next)),
                        MinWidth = 70,
                        Align = Label.AlignMode.Right,
                    },
                },
            });
        }
    }

    #endregion

    #region Gear

    private IEnumerable<ContactLoadoutItemPrototype> Items(ContactLoadoutCategory category)
    {
        return _prototypes.EnumeratePrototypes<ContactLoadoutItemPrototype>()
            .Where(i => i.Category == category)
            .OrderBy(i => i.Order)
            .ThenBy(i => i.ID);
    }

    private string ItemName(ContactLoadoutItemPrototype item)
    {
        if (item.Name is { } name)
            return Loc.GetString(name);

        return _prototypes.TryIndex(item.Proto, out var proto) ? proto.Name : item.ID;
    }

    private void RebuildGear()
    {
        _gearList.RemoveAllChildren();
        foreach (var category in GearCategories)
        {
            var cat = category;
            var options = new List<string?> { null };
            options.AddRange(Items(cat).Select(i => i.ID));

            var button = MakeOption(options, _items.GetValueOrDefault(cat), "mriya-contact-loadout-keep",
                id => _prototypes.TryIndex<ContactLoadoutItemPrototype>(id, out var item) ? ItemName(item) : id,
                id =>
                {
                    if (id == null)
                        _items.Remove(cat);
                    else
                        _items[cat] = id;

                    if (cat == ContactLoadoutCategory.Weapon)
                        RebuildAttachments();
                });

            _gearList.AddChild(Row(Loc.GetString($"mriya-contact-loadout-category-{cat.ToString().ToLowerInvariant()}"), button));
        }
    }

    /// <summary>
    /// Every spawnable attachment prototype, cached: the slot whitelists pick from these.
    /// </summary>
    private List<EntityPrototype> Attachables()
    {
        return _attachables ??= _prototypes.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && !p.HideSpawnMenu && p.TryGetComponent(out AttachableComponent? _, _factory))
            .OrderBy(p => p.Name)
            .ToList();
    }

    /// <summary>
    /// One picker per unlocked attachment slot of the chosen weapon, listing every attachment its whitelist accepts.
    /// </summary>
    private void RebuildAttachments()
    {
        _attachmentList.RemoveAllChildren();

        if (!_items.TryGetValue(ContactLoadoutCategory.Weapon, out var weaponId) ||
            !_prototypes.TryIndex<ContactLoadoutItemPrototype>(weaponId, out var weapon) ||
            !_prototypes.TryIndex(weapon.Proto, out var gun) ||
            !gun.TryGetComponent(out AttachableHolderComponent? holder, _factory))
        {
            _attachmentList.AddChild(new Label { Text = Loc.GetString("mriya-contact-loadout-no-attachments") });
            return;
        }

        foreach (var (slotId, slot) in holder.Slots)
        {
            if (slot.Locked || slot.Whitelist is not { } whitelist)
                continue;

            var slotKey = slotId;
            var options = new List<string?> { null };
            foreach (var attachment in Attachables())
            {
                if (ContactLoadoutHelpers.MatchesProto(whitelist, attachment, _factory))
                    options.Add(attachment.ID);
            }

            // nothing fits this slot
            if (options.Count == 1)
                continue;

            if (_attachments.TryGetValue(slotKey, out var chosen) && !options.Contains(chosen))
                _attachments.Remove(slotKey);

            var button = MakeOption(options, _attachments.GetValueOrDefault(slotKey), "mriya-contact-loadout-none",
                id => _prototypes.TryIndex<EntityPrototype>(id, out var proto) ? proto.Name : id,
                id =>
                {
                    if (id == null)
                        _attachments.Remove(slotKey);
                    else
                        _attachments[slotKey] = id;
                });

            _attachmentList.AddChild(Row(Loc.GetString(slotKey), button));
        }
    }

    /// <summary>
    /// Option button over ids; null = the "empty" entry.
    /// </summary>
    private OptionButton MakeOption(List<string?> ids, string? selected, LocId emptyText, Func<string, string> nameOf, Action<string?> onSelected)
    {
        var button = new OptionButton { HorizontalExpand = true };
        for (var i = 0; i < ids.Count; i++)
        {
            button.AddItem(ids[i] is { } id ? nameOf(id) : Loc.GetString(emptyText), i);
            if (ids[i] == selected)
                button.SelectId(i);
        }

        button.OnItemSelected += args =>
        {
            button.SelectId(args.Id);
            onSelected(ids[args.Id]);
        };

        return button;
    }

    private static BoxContainer Row(string label, Control control)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 2),
            Children =
            {
                new Label { Text = label, MinWidth = 160 },
                control,
            },
        };
    }

    #endregion
}
