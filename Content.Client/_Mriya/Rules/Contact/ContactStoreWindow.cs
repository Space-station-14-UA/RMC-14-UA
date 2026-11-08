using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Content.Client._CMU14.Announce;
using Content.Shared._CMU14.Announce;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Є-бали store window
/// Left: operative (balance, gear, ammo, wounds). Middle: folders. Right: folder contents.
/// </summary>
public sealed class ContactStoreWindow : FancyWindow
{
    #region Palette

    private static readonly Color Bg = Color.FromHex("#0D0D0D");
    private static readonly Color Phosphor = Color.FromHex("#0AFE60");
    private static readonly Color Green = Color.FromHex("#087A25");
    private static readonly Color Teal = Color.FromHex("#047D44");
    private static readonly Color Deep = Color.FromHex("#0A4A26");
    private static readonly Color Dim = Color.FromHex("#1E3A28");
    private static readonly Color White = Color.FromHex("#FFFFFF");
    private static readonly Color Black = Color.FromHex("#000000");

    private readonly HashSet<string> _compatible = new();
    private readonly List<FilterFolder> _filterFolders = new();

    private sealed class FilterFolder
    {
        public bool CompatibleOnly = true;
        public RetroButton All = default!;
        public RetroButton Compatible = default!;
        public Label Empty = default!;
        public readonly List<(string Id, Control Row)> Rows = new();
    }

    private const string FontPath = "/EngineFonts/NotoSans/NotoSansMono-Regular.ttf";

    private static StyleBoxFlat Box(Color bg, Color? border = null, float thickness = 0, float margin = 6)
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

    #endregion

    private readonly IEntityManager _ents;
    private readonly IPrototypeManager _protos;
    private readonly Font _font;
    private readonly Font _fontSmall;
    private readonly Font _fontHeading;

    private readonly Label _balanceLabel;
    private readonly Label _folderTitle;
    private readonly BoxContainer _left;
    private readonly BoxContainer _forYou;
    private readonly BoxContainer _folderList;
    private readonly BoxContainer _folderContents;

    private readonly List<(RetroButton Button, Control Contents, string Title)> _folders = new();

    // buy buttons -> price, to grey out what you can't afford
    private readonly Dictionary<RetroButton, int> _priced = new();
    private readonly List<RetroButton> _forYouButtons = new();
    private int _balance;

    public event Action<string, int>? Buy;
    public event Action<string>? ResupplyKit;

    public ContactStoreWindow()
    {
        _ents = IoCManager.Resolve<IEntityManager>();
        _protos = IoCManager.Resolve<IPrototypeManager>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont(FontPath, 12);
        _fontSmall = cache.GetFont(FontPath, 10);
        _fontHeading = cache.GetFont(FontPath, 15);

        Title = Loc.GetString("mriya-contact-store-title");
        MinSize = new Vector2(1180, 660);

        var screen = new PanelContainer
        {
            PanelOverride = Box(Bg, margin: 10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
        };

        // Title bar: solid phosphor, black text
        var titleBar = new PanelContainer { PanelOverride = Box(Phosphor, margin: 8) };
        var titleRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        titleRow.AddChild(Text(Loc.GetString("mriya-contact-store-title").ToUpperInvariant(), Black, _fontHeading, expand: true));
        _balanceLabel = Text(string.Empty, Black, _fontHeading);
        titleRow.AddChild(_balanceLabel);
        titleBar.AddChild(titleRow);
        root.AddChild(titleBar);

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 12,
            VerticalExpand = true,
        };

        // Left: operative
        var leftFrame = new PanelContainer { PanelOverride = Box(Bg, Green, 2, 8), MinWidth = 300 };
        var leftColumn = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        leftColumn.AddChild(Text(Loc.GetString("mriya-contact-store-left-title").ToUpperInvariant(), Phosphor, _fontHeading));
        var leftScroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        _left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
        leftScroll.AddChild(_left);
        leftColumn.AddChild(leftScroll);
        leftFrame.AddChild(leftColumn);
        body.AddChild(leftFrame);

        // Middle: folders
        var folderColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            MinWidth = 190,
        };
        folderColumn.AddChild(Text(Loc.GetString("mriya-contact-store-folders").ToUpperInvariant(), Phosphor, _font));
        _folderList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };
        folderColumn.AddChild(_folderList);
        body.AddChild(folderColumn);

        // connector line between folders and contents
        body.AddChild(new PanelContainer { PanelOverride = Box(White, margin: 0), MinWidth = 2, VerticalExpand = true });

        // Right: contents
        var contentColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        var folderHeader = new PanelContainer { PanelOverride = Box(Deep, White, 2, 10) };
        _folderTitle = Text(string.Empty, White, _fontHeading);
        folderHeader.AddChild(_folderTitle);
        contentColumn.AddChild(folderHeader);

        var contentFrame = new PanelContainer { PanelOverride = Box(Bg, Green, 2, 8), VerticalExpand = true };
        var contentScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        _folderContents = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        contentScroll.AddChild(_folderContents);
        contentFrame.AddChild(contentScroll);
        contentColumn.AddChild(contentFrame);
        body.AddChild(contentColumn);

        root.AddChild(body);

        // Footer: key hints
        root.AddChild(Text(Loc.GetString("mriya-contact-store-footer").ToUpperInvariant(), Teal, _fontSmall));

        screen.AddChild(root);
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
                ScanlineColor = Color.FromHex("#000000"),
                ShowNoise = true,
                NoiseIntensity = 0.08f,
                ShowVignette = true,
                VignetteIntensity = 0.25f,
                ShowChromaticAberration = false,
                GlowColor = Phosphor,
            },
        });
        ContentsContainer.AddChild(crtStack);

        // Folders
        _forYou = VBox();
        AddFolder(Loc.GetString("mriya-contact-store-tab-for-you"), _forYou);

        var entries = _protos.EnumeratePrototypes<ContactStoreEntryPrototype>().ToList();
        foreach (var category in _protos.EnumeratePrototypes<ContactStoreCategoryPrototype>().OrderBy(c => c.Priority))
        {
            var list = VBox();

            FilterFolder? filter = null;
            if (category.CompatibilityFilter)
            {
                var f = new FilterFolder();
                var toggles = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 6,
                    Margin = new Thickness(0, 0, 0, 4),
                };

                f.All = new RetroButton($">{Loc.GetString("mriya-contact-store-filter-all").ToUpperInvariant()}", _font);
                f.Compatible = new RetroButton($">{Loc.GetString("mriya-contact-store-filter-compatible").ToUpperInvariant()}", _font);
                f.All.OnPressed += _ =>
                {
                    f.CompatibleOnly = false;
                    ApplyFilter(f);
                };
                f.Compatible.OnPressed += _ =>
                {
                    f.CompatibleOnly = true;
                    ApplyFilter(f);
                };

                toggles.AddChild(f.All);
                toggles.AddChild(f.Compatible);
                list.AddChild(toggles);

                f.Empty = Text(Loc.GetString("mriya-contact-store-filter-empty"), Teal, _font);
                list.AddChild(f.Empty);

                _filterFolders.Add(f);
                filter = f;
            }

            foreach (var entry in entries.Where(e => e.Category == category.ID).OrderBy(e => e.Price))
            {
                var row = MakeEntryRow(entry, null);
                list.AddChild(row);
                filter?.Rows.Add((entry.ID, row));
            }

            if (filter != null)
                ApplyFilter(filter);

            AddFolder(Loc.GetString(category.Name), list);
        }

        SelectFolder(0);
        SetBalance(0);
    }

    private void ApplyFilter(FilterFolder f)
    {
        f.All.Selected = !f.CompatibleOnly;
        f.Compatible.Selected = f.CompatibleOnly;

        var any = false;
        foreach (var (id, row) in f.Rows)
        {
            var show = !f.CompatibleOnly || _compatible.Contains(id);
            row.Visible = show;
            any |= show;
        }

        f.Empty.Visible = !any;
    }

    #region Building blocks

    private static BoxContainer VBox()
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
    }

    private static Label Text(string text, Color color, Font font, bool expand = false)
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

    /// <summary>
    /// Filled bar with white border: ">LABEL" section header.
    /// </summary>
    private Control SectionBar(string text)
    {
        var bar = new PanelContainer
        {
            PanelOverride = Box(Deep, White, 1, 8),
            Margin = new Thickness(0, 6, 0, 2),
        };
        bar.AddChild(Text($">{text.ToUpperInvariant()}", White, _font));
        return bar;
    }

    private Control LeftHeader(string text)
    {
        return Text($">{text.ToUpperInvariant()}", White, _font);
    }

    private void AddFolder(string title, Control contents)
    {
        var index = _folders.Count;
        var button = new RetroButton($">{title.ToUpperInvariant()}", _font) { MinHeight = 44 };
        button.OnPressed += _ => SelectFolder(index);
        _folderList.AddChild(button);

        contents.Visible = false;
        _folderContents.AddChild(contents);
        _folders.Add((button, contents, title));
    }

    private void SelectFolder(int index)
    {
        for (var i = 0; i < _folders.Count; i++)
        {
            var (button, contents, title) = _folders[i];
            var selected = i == index;
            button.Selected = selected;
            contents.Visible = selected;

            if (selected)
                _folderTitle.Text = title.ToUpperInvariant();
        }
    }

    #endregion

    #region Store rows

    private string EntryName(ContactStoreEntryPrototype entry)
    {
        return entry.Name is { } name ? Loc.GetString(name) : ProtoName(entry.Proto);
    }

    private string ProtoName(EntProtoId proto)
    {
        return _protos.TryIndex(proto, out var p) ? p.Name : proto.Id;
    }

    private Control MakeEntryRow(ContactStoreEntryPrototype entry, List<RetroButton>? track)
    {
        var frame = new PanelContainer { PanelOverride = Box(Bg, Dim, 1, 6) };
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };

        var view = new EntityPrototypeView { SetSize = new Vector2(32, 32) };
        view.SetPrototype(entry.Proto);
        row.AddChild(view);

        row.AddChild(Text(EntryName(entry), Phosphor, _font, expand: true));
        row.AddChild(Text(Loc.GetString("mriya-contact-store-price", ("price", entry.Price)), White, _font));

        foreach (var amount in new[] { 1, 5, 10 })
        {
            if (amount > entry.MaxPerPurchase)
                break;

            var button = new RetroButton($">×{amount}", _font) { MinWidth = 56 };
            button.OnPressed += _ => Buy?.Invoke(entry.ID, amount);
            _priced[button] = entry.Price * amount;
            track?.Add(button);
            row.AddChild(button);
        }

        frame.AddChild(row);
        return frame;
    }

    #endregion

    public void UpdateServer(ContactStoreUpdateMessage msg)
    {
        foreach (var button in _forYouButtons)
        {
            _priced.Remove(button);
        }

        _forYouButtons.Clear();
        _forYou.RemoveAllChildren();

        // Ammo for your guns
        _compatible.Clear();
        _compatible.UnionWith(msg.CompatibleEntries);
        foreach (var f in _filterFolders)
        {
            ApplyFilter(f);
        }

        _forYou.AddChild(SectionBar(Loc.GetString("mriya-contact-store-section-ammo")));
        var compatibleEntries = msg.CompatibleEntries
            .Select(id => _protos.TryIndex<ContactStoreEntryPrototype>(id, out var e) ? e : null)
            .OfType<ContactStoreEntryPrototype>()
            .Where(e => _protos.TryIndex(e.Proto, out var p) &&
            p.Components.ContainsKey("BallisticAmmoProvider") &&
            !p.Components.ContainsKey("Gun"))
            .OrderBy(e => _protos.TryIndex(e.Category, out var c) ? c.Priority : 0)
            .ThenBy(e => e.Price)
            .ToList();

        foreach (var entry in compatibleEntries)
        {
            _forYou.AddChild(MakeEntryRow(entry, _forYouButtons));
        }

        if (compatibleEntries.Count == 0)
            _forYou.AddChild(Text(Loc.GetString("mriya-contact-store-none"), Teal, _font));
        // Quick medicine
        _forYou.AddChild(SectionBar(Loc.GetString("mriya-contact-store-section-quick")));
        foreach (var entry in _protos.EnumeratePrototypes<ContactStoreEntryPrototype>().Where(e => e.Quick).OrderBy(e => e.Price))
        {
            _forYou.AddChild(MakeEntryRow(entry, _forYouButtons));
        }

        // Kit resupply
        _forYou.AddChild(SectionBar(Loc.GetString("mriya-contact-store-section-kits")));
        if (msg.Kits.Count == 0)
            _forYou.AddChild(Text(Loc.GetString("mriya-contact-store-none"), Teal, _font));

        foreach (var kit in msg.Kits)
        {
            if (!_protos.TryIndex<ContactKitPrototype>(kit.Kit, out var kitProto))
                continue;

            var frame = new PanelContainer { PanelOverride = Box(Bg, Dim, 1, 6) };
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            var info = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
            info.AddChild(Text(Loc.GetString(kitProto.Name), Phosphor, _font));

            var missingText = kit.Missing.Count == 0
                ? Loc.GetString("mriya-contact-store-kit-full")
                : string.Join(", ", kit.Missing.Select(m => $"{ProtoName(m.Key)} ×{m.Value}"));
            info.AddChild(Text(missingText, Teal, _fontSmall));
            row.AddChild(info);

            row.AddChild(Text(Loc.GetString("mriya-contact-store-price", ("price", kit.Price)), White, _font));

            var button = new RetroButton($">{Loc.GetString("mriya-contact-store-resupply").ToUpperInvariant()}", _font);
            var kitId = kit.Kit;
            button.OnPressed += _ => ResupplyKit?.Invoke(kitId);
            _priced[button] = kit.Price > 0 ? kit.Price : int.MaxValue;
            _forYouButtons.Add(button);
            row.AddChild(button);

            frame.AddChild(row);
            _forYou.AddChild(frame);
        }

        SetBalance(msg.Balance);
    }

    private void SetBalance(int balance)
    {
        _balance = balance;
        _balanceLabel.Text = Loc.GetString("mriya-contact-store-balance", ("balance", balance)).ToUpperInvariant();

        foreach (var (button, price) in _priced)
        {
            button.Disabled = price > _balance;
        }
    }

    #region Left panel

    public void UpdatePlayer(EntityUid? player)
    {
        _left.RemoveAllChildren();
        if (player is not { } uid)
            return;

        // Wounds
        _left.AddChild(LeftHeader(Loc.GetString("mriya-contact-store-left-wounds")));
        var anyWounds = false;
        if (_ents.TryGetComponent(uid, out DamageableComponent? damageable))
        {
            foreach (var (group, value) in damageable.DamagePerGroup)
            {
                if (value <= 0 || !_protos.TryIndex<DamageGroupPrototype>(group, out var groupProto))
                    continue;

                var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
                row.AddChild(Text(groupProto.LocalizedName.ToUpperInvariant(), Phosphor, _font, expand: true));
                row.AddChild(Text(value.Int().ToString(), White, _font));
                _left.AddChild(row);
                anyWounds = true;
            }
        }

        if (!anyWounds)
            _left.AddChild(Text(Loc.GetString("mriya-contact-store-left-no-wounds"), Teal, _font));

        var inventory = _ents.System<InventorySystem>();
        var hands = _ents.System<SharedHandsSystem>();

        // Hands
        _left.AddChild(LeftHeader(Loc.GetString("mriya-contact-store-left-hands")));
        foreach (var held in hands.EnumerateHeld(uid))
        {
            _left.AddChild(ItemRow(held, null));
        }

        // Ammo: magazines grouped by prototype
        _left.AddChild(LeftHeader(Loc.GetString("mriya-contact-store-left-ammo")));
        var mags = new Dictionary<string, (EntityUid Sample, int Count, int Rounds)>();
        foreach (var item in ContactStoreHelpers.EnumerateCarried(_ents, uid))
        {
            if (!ContactStoreHelpers.IsMagazine(_ents, item) ||
                _ents.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID is not { } id)
            {
                continue;
            }

            var (sample, count, rounds) = mags.GetValueOrDefault(id, (item, 0, 0));
            mags[id] = (sample, count + 1, rounds + ContactStoreHelpers.GetRounds(_ents, item));
        }

        if (mags.Count == 0)
            _left.AddChild(Text(Loc.GetString("mriya-contact-store-none"), Teal, _font));

        foreach (var (_, (sample, count, rounds)) in mags)
        {
            _left.AddChild(ItemRow(sample, Loc.GetString("mriya-contact-store-left-mags", ("count", count), ("rounds", rounds))));
        }

        // Equipment
        _left.AddChild(LeftHeader(Loc.GetString("mriya-contact-store-left-equipment")));
        if (_ents.TryGetComponent(uid, out InventoryComponent? inv))
        {
            foreach (var slot in inv.Slots)
            {
                if (inventory.TryGetSlotEntity(uid, slot.Name, out var item, inv))
                    _left.AddChild(ItemRow(item.Value, null));
            }
        }

    }

    private Control ItemRow(EntityUid item, string? extra)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        var view = new SpriteView { SetSize = new Vector2(32, 32) };
        view.SetEntity(item);
        row.AddChild(view);
        row.AddChild(Text(_ents.GetComponent<MetaDataComponent>(item).EntityName, Phosphor, _font, expand: true));

        if (extra != null)
            row.AddChild(Text(extra, Teal, _fontSmall));

        return row;
    }

    #endregion

    /// <summary>
    /// ">LABEL" button. Normal: green outline. Hover: filled dark green, phosphor border.
    /// Selected/pressed: white fill, black text. Disabled: dim.
    /// </summary>
    private sealed class RetroButton : Button
    {
        private bool _selected;
        private bool _ready;

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