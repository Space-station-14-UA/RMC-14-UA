using System.Linq;
using System.Numerics;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Attachable.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Contact perks/loadout window. Tab 1: perk decks, buy-once with skill points.
/// Tab 2: one pick per gear category + the weapon's attachments.
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
        ContactLoadoutCategory.Boots,
        ContactLoadoutCategory.Belt,
        ContactLoadoutCategory.Pouch,
        ContactLoadoutCategory.Pouch2,
    };

    private static readonly Color DescriptionColor = Color.FromHex("#9A9A9A");
    private static readonly Color DeckColor = Color.FromHex("#E0B040");

    private readonly ContactLoadoutSystem _system;
    private readonly IPrototypeManager _prototypes;
    private readonly IComponentFactory _factory;

    private readonly Label _points = new();
    private readonly BoxContainer _perkList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _gearList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _attachmentList = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly Label _shipOnly = new() { Visible = false };
    private readonly Button _apply = new();

    private readonly HashSet<string> _perks = new();
    private readonly Dictionary<ContactLoadoutCategory, string> _items = new();
    private readonly Dictionary<string, string> _attachments = new();
    private List<EntityPrototype>? _attachables;
    private int _maxPoints;

    public ContactLoadoutWindow(ContactLoadoutSystem system, IPrototypeManager prototypes, IComponentFactory factory)
    {
        _system = system;
        _prototypes = prototypes;
        _factory = factory;

        Title = Loc.GetString("mriya-contact-loadout-title");
        MinSize = new Vector2(600, 640);

        var tabs = new TabContainer { VerticalExpand = true };

        var perksTab = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        perksTab.AddChild(_points);
        perksTab.AddChild(new ScrollContainer
        {
            VerticalExpand = true,
            HScrollEnabled = false,
            Children = { _perkList },
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

        tabs.AddChild(perksTab);
        tabs.AddChild(gearTab);
        TabContainer.SetTabTitle(perksTab, Loc.GetString("mriya-contact-loadout-tab-skills"));
        TabContainer.SetTabTitle(gearTab, Loc.GetString("mriya-contact-loadout-tab-gear"));

        _shipOnly.Text = Loc.GetString("mriya-contact-loadout-ship-only");
        _apply.Text = Loc.GetString("mriya-contact-loadout-apply");
        _apply.OnPressed += _ =>
        {
            _system.Apply(new ContactLoadoutApplyEvent
            {
                Perks = _perks.ToList(),
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
    /// Fill from the saved preset; if the points don't cover it any more, the most expensive perks are dropped.
    /// </summary>
    public void Load(ContactLoadoutApplyEvent preset, int points, bool canApply)
    {
        _perks.Clear();
        _items.Clear();
        _attachments.Clear();

        foreach (var id in preset.Perks)
        {
            if (_prototypes.HasIndex<ContactPerkPrototype>(id))
                _perks.Add(id);
        }

        foreach (var (category, id) in preset.Items)
        {
            if (_prototypes.TryIndex<ContactLoadoutItemPrototype>(id, out var item) &&
                item.Category == ContactLoadoutHelpers.ItemCategory(category))
                _items[category] = id;
        }

        foreach (var (slot, id) in preset.Attachments)
        {
            _attachments[slot] = id;
        }

        _maxPoints = points;
        TrimToPoints();

        _apply.Disabled = !canApply;
        _shipOnly.Visible = !canApply;

        RebuildPerks();
        RebuildGear();
        RebuildAttachments();
    }

    public void SetPoints(int points)
    {
        _maxPoints = points;
        TrimToPoints();
        RebuildPerks();
    }

    #region Perks

    private int Spent()
    {
        return ContactLoadoutHelpers.TotalCost(_prototypes, _perks);
    }

    private void TrimToPoints()
    {
        while (Spent() > _maxPoints && _perks.Count > 0)
        {
            var priciest = _perks
                .Select(id => _prototypes.Index<ContactPerkPrototype>(id))
                .OrderByDescending(p => p.Cost)
                .First();

            _perks.Remove(priciest.ID);
        }
    }

    /// <summary>
    /// Decks in order of their first perk; each perk a checkbox with its cost and description.
    /// A perk that doesn't fit the remaining points can't be ticked.
    /// </summary>
    private void RebuildPerks()
    {
        var spent = Spent();
        var left = _maxPoints - spent;
        _points.Text = Loc.GetString("mriya-contact-loadout-points", ("left", left), ("total", _maxPoints));

        _perkList.RemoveAllChildren();

        var decks = _prototypes.EnumeratePrototypes<ContactPerkPrototype>()
            .GroupBy(p => p.Deck)
            .OrderBy(g => g.Min(p => p.Order));

        foreach (var deck in decks)
        {
            _perkList.AddChild(new Label
            {
                Text = Loc.GetString(deck.Key).ToUpperInvariant(),
                FontColorOverride = DeckColor,
                Margin = new Thickness(0, 8, 0, 2),
            });

            foreach (var perk in deck.OrderBy(p => p.Order).ThenBy(p => p.ID))
            {
                var id = perk.ID;
                var owned = _perks.Contains(id);

                var conflicts = !owned && ContactLoadoutHelpers.Conflicts(_prototypes, perk, _perks);
                var check = new CheckBox
                {
                    Text = Loc.GetString(perk.Name),
                    Pressed = owned,
                    Disabled = !owned && (perk.Cost > left || conflicts),
                    ToolTip = conflicts ? Loc.GetString("mriya-contact-loadout-conflict") : null,
                    HorizontalExpand = true,
                };

                check.OnToggled += args =>
                {
                    if (args.Pressed)
                        _perks.Add(id);
                    else
                        _perks.Remove(id);

                    RebuildPerks();
                };

                _perkList.AddChild(new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children =
                    {
                        check,
                        new Label
                        {
                            Text = Loc.GetString("mriya-contact-loadout-cost", ("cost", perk.Cost)),
                            MinWidth = 70,
                            Align = Label.AlignMode.Right,
                        },
                    },
                });

                if (perk.Description is { } desc)
                {
                    // RichTextLabel wraps, Label doesn't
                    var text = new FormattedMessage();
                    text.PushColor(DescriptionColor);
                    text.AddText(Loc.GetString(desc));
                    text.Pop();

                    var description = new RichTextLabel { Margin = new Thickness(24, 0, 0, 4) };
                    description.SetMessage(text);
                    _perkList.AddChild(description);
                }
            }
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
                        options.AddRange(Items(ContactLoadoutHelpers.ItemCategory(cat)).Select(i => i.ID));

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
