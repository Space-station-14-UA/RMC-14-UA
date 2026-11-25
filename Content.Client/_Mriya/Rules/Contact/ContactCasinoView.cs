using System.Linq;
using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using static Content.Client._Mriya.Rules.Contact.ContactRetro;
using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Games screen: slots (solo) and the shared blackjack table. Used inside the store window (games view)
/// and in ContactCasinoWindow for slot machines / tables. Raises events; the owning BUI sends the messages.
/// </summary>
public sealed class ContactCasinoView : BoxContainer
{
    private static readonly TimeSpan SpinTime = TimeSpan.FromSeconds(1.2);
    private static readonly TimeSpan SpinTimeout = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan ReelStep = TimeSpan.FromSeconds(0.07);

    private readonly IGameTiming _timing;
    private readonly IRobustRandom _random;
    private static readonly ResPath CardRsi = new("_RMC14/Objects/Fun/playing_cards.rsi");
    private static readonly Vector2 CardSize = new(48, 48);

    private readonly SpriteSystem _sprite;

    private readonly Font _font;
    private readonly Font _fontSmall;
    private readonly Font _fontBig;

    public event Action<int>? Spin;
    public event Action<ContactBlackjackAction, int>? BlackjackAction;

    private readonly List<(RetroButton Button, Control Panel)> _tabs = new();

    // slots
    private readonly ContactCasinoPrototype? _slotRules;
    private readonly Label[] _reels = new Label[3];
    private readonly EntityPrototypeView[] _reelViews = new EntityPrototypeView[3];
    private readonly Label _slotResult;
    private readonly Label _slotBalance;
    private readonly List<RetroButton> _spinButtons = new();
    private bool _spinning;
    private TimeSpan _spinStarted;
    private TimeSpan _nextReelStep;
    private ContactSlotResultMessage? _pendingResult;

    // blackjack
    private readonly Label _bjStatus;
    private readonly Label _bjBalance;
    private readonly BoxContainer _bjDealer;
    private readonly BoxContainer _bjSeats;
    private readonly RetroButton _sit;
    private readonly RetroButton _leave;
    private readonly RetroButton _hit;
    private readonly RetroButton _stand;
    private readonly RetroButton _double;
    private readonly List<(RetroButton Button, int Amount)> _betButtons = new();
    private ContactBlackjackStateMessage? _bjState;
    private TimeSpan _bjDeadline;

    public ContactCasinoView(ContactCasinoComponent casino)
    {
        _timing = IoCManager.Resolve<IGameTiming>();
        _random = IoCManager.Resolve<IRobustRandom>();
        _sprite = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
        var protos = IoCManager.Resolve<IPrototypeManager>();
        var cache = IoCManager.Resolve<IResourceCache>();
        _font = cache.GetFont(FontPath, 12);
        _fontSmall = cache.GetFont(FontPath, 10);
        _fontBig = cache.GetFont(FontPath, 22);

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 8;
        HorizontalExpand = true;
        VerticalExpand = true;

        var tabRow = HBox();
        AddChild(tabRow);
        var panels = new Control { HorizontalExpand = true, VerticalExpand = true };
        AddChild(panels);

        // ---------- slots ----------
        _slotResult = Text(string.Empty, Phosphor, _font);
        _slotBalance = Text(string.Empty, White, _font);
        if (casino.Slots && protos.TryIndex(casino.Rules, out var rules) && rules.Symbols.Count > 0)
        {
            _slotRules = rules;
            var slots = Frame(VBox(10));
            var inner = (BoxContainer) slots.GetChild(0);

            inner.AddChild(_slotBalance);

            var reelRow = HBox(12);
            reelRow.HorizontalAlignment = HAlignment.Center;
            for (var i = 0; i < _reels.Length; i++)
            {
                // PanelContainer stacks its children: text and sprite overlap, one is hidden
                var reel = new PanelContainer { PanelOverride = Box(Black, Phosphor, 2, 12), MinSize = new(150, 90) };
                _reels[i] = Text(string.Empty, Phosphor, _fontBig);
                _reels[i].HorizontalAlignment = HAlignment.Center;
                _reels[i].VerticalAlignment = VAlignment.Center;
                _reelViews[i] = new EntityPrototypeView
                {
                    SetSize = new Vector2(72, 72),
                    Scale = new Vector2(2, 2),
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                };
                reel.AddChild(_reels[i]);
                reel.AddChild(_reelViews[i]);
                reelRow.AddChild(reel);
                SetReel(i, rules.Symbols[i % rules.Symbols.Count]);
            }

            inner.AddChild(reelRow);
            _slotResult.HorizontalAlignment = HAlignment.Center;
            inner.AddChild(_slotResult);

            var bets = HBox();
            bets.HorizontalAlignment = HAlignment.Center;
            foreach (var bet in rules.SlotBets)
            {
                var amount = bet;
                var button = new RetroButton(Loc.GetString("mriya-contact-casino-spin", ("bet", bet)).ToUpperInvariant(), _font)
                {
                    MinWidth = 110,
                };
                button.OnPressed += _ => StartSpin(amount);
                _spinButtons.Add(button);
                bets.AddChild(button);
            }

            inner.AddChild(bets);

            // payout table
            inner.AddChild(Text($">{Loc.GetString("mriya-contact-casino-payouts").ToUpperInvariant()}", White, _font));
            foreach (var symbol in rules.Symbols.Where(s => s.Three > 0 || s.Two > 0).OrderByDescending(s => s.Three))
            {
                var row = HBox(12);
                row.AddChild(SymbolRun(symbol, 3));
                row.AddChild(new Control { HorizontalExpand = true });
                row.AddChild(Text($"×{symbol.Three}", White, _font));
                if (symbol.Two > 0)
                {
                    row.AddChild(SymbolRun(symbol, 2));
                    row.AddChild(Text($"×{symbol.Two}", White, _font));
                }

                inner.AddChild(row);
            }

            AddTab(tabRow, panels, Loc.GetString("mriya-contact-casino-tab-slots"), slots);
        }

        // ---------- blackjack ----------
        _bjStatus = Text(string.Empty, Phosphor, _font, expand: true);
        _bjBalance = Text(string.Empty, White, _font);
        _bjDealer = HBox();
        _bjSeats = VBox(6);
        _sit = new RetroButton(Loc.GetString("mriya-contact-casino-sit").ToUpperInvariant(), _font);
        _leave = new RetroButton(Loc.GetString("mriya-contact-casino-leave").ToUpperInvariant(), _font);
        _hit = new RetroButton(Loc.GetString("mriya-contact-casino-hit").ToUpperInvariant(), _font) { MinWidth = 100 };
        _stand = new RetroButton(Loc.GetString("mriya-contact-casino-stand").ToUpperInvariant(), _font) { MinWidth = 100 };
        _double = new RetroButton(Loc.GetString("mriya-contact-casino-double").ToUpperInvariant(), _font) { MinWidth = 100 };
        _sit.OnPressed += _ => BlackjackAction?.Invoke(ContactBlackjackAction.Sit, 0);
        _leave.OnPressed += _ => BlackjackAction?.Invoke(ContactBlackjackAction.Leave, 0);
        _hit.OnPressed += _ => BlackjackAction?.Invoke(ContactBlackjackAction.Hit, 0);
        _stand.OnPressed += _ => BlackjackAction?.Invoke(ContactBlackjackAction.Stand, 0);
        _double.OnPressed += _ => BlackjackAction?.Invoke(ContactBlackjackAction.Double, 0);

        if (casino.Blackjack)
        {
            var table = Frame(VBox(8));
            var inner = (BoxContainer) table.GetChild(0);

            var top = HBox();
            top.AddChild(_bjStatus);
            top.AddChild(_bjBalance);
            inner.AddChild(top);

            var dealerRow = new PanelContainer { PanelOverride = Box(Deep, White, 1, 8) };
            var dealerBox = HBox(12);
            dealerBox.AddChild(Text(Loc.GetString("mriya-contact-casino-dealer").ToUpperInvariant(), White, _font));
            dealerBox.AddChild(_bjDealer);
            dealerRow.AddChild(dealerBox);
            inner.AddChild(dealerRow);

            var seatScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
            seatScroll.AddChild(_bjSeats);
            inner.AddChild(seatScroll);

            var seatButtons = HBox();
            seatButtons.AddChild(_sit);
            seatButtons.AddChild(_leave);
            seatButtons.AddChild(new Control { HorizontalExpand = true });
            seatButtons.AddChild(Text(Loc.GetString("mriya-contact-casino-bet").ToUpperInvariant(), White, _font));
            foreach (var bet in protos.Index(ContactCasinoPrototype.Default).BlackjackBets)
            {
                var amount = bet;
                var button = new RetroButton($">{bet}", _font) { MinWidth = 56 };
                button.OnPressed += _ => BlackjackAction?.Invoke(ContactBlackjackAction.Bet, amount);
                _betButtons.Add((button, amount));
                seatButtons.AddChild(button);
            }

            inner.AddChild(seatButtons);

            var play = HBox();
            play.HorizontalAlignment = HAlignment.Center;
            play.AddChild(_hit);
            play.AddChild(_stand);
            play.AddChild(_double);
            inner.AddChild(play);

            AddTab(tabRow, panels, Loc.GetString("mriya-contact-casino-tab-blackjack"), table);
            UpdateBlackjack();
        }

        if (_tabs.Count == 0)
            AddChild(Text(Loc.GetString("mriya-contact-casino-none"), Teal, _font));
        else
            SelectTab(0);
    }

    #region Layout

    private PanelContainer Frame(BoxContainer inner)
    {
        var frame = new PanelContainer
        {
            PanelOverride = Box(Bg, Green, 2, 10),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        frame.AddChild(inner);
        return frame;
    }

    private void AddTab(BoxContainer tabRow, Control panels, string title, Control panel)
    {
        var index = _tabs.Count;
        var button = new RetroButton($">{title.ToUpperInvariant()}", _font) { MinWidth = 140 };
        button.OnPressed += _ => SelectTab(index);
        tabRow.AddChild(button);
        panels.AddChild(panel);
        _tabs.Add((button, panel));
    }

    private void SelectTab(int index)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Button.Selected = i == index;
            _tabs[i].Panel.Visible = i == index;
        }
    }

    #endregion

    #region Slots

    private void StartSpin(int bet)
    {
        if (_spinning)
            return;

        _spinning = true;
        _spinStarted = _timing.RealTime;
        _pendingResult = null;
        _slotResult.Text = string.Empty;
        foreach (var button in _spinButtons)
        {
            button.Disabled = true;
        }

        Spin?.Invoke(bet);
    }

    public void SlotResult(ContactSlotResultMessage msg)
    {
        _pendingResult = msg;
        SetBalance(msg.Balance);
    }

    private void FinishSpin()
    {
        _spinning = false;
        foreach (var button in _spinButtons)
        {
            button.Disabled = false;
        }

        if (_pendingResult is not { } result || _slotRules == null)
            return;

        for (var i = 0; i < _reels.Length && i < result.Reels.Length; i++)
        {
            SetReel(i, _slotRules.Symbols[Math.Clamp(result.Reels[i], 0, _slotRules.Symbols.Count - 1)]);
        }

        _slotResult.Text = result.Payout > 0
            ? Loc.GetString("mriya-contact-casino-slot-win", ("amount", result.Payout)).ToUpperInvariant()
            : Loc.GetString("mriya-contact-casino-slot-lose").ToUpperInvariant();
        _slotResult.FontColorOverride = result.Payout > 0 ? Gold : Teal;
        _pendingResult = null;
    }

    /// <summary>
    /// Entity sprite if the symbol has one, otherwise its text.
    /// </summary>
    private void SetReel(int index, ContactSlotSymbol symbol)
    {
        var hasEntity = symbol.Entity != null;
        _reelViews[index].Visible = hasEntity;
        _reels[index].Visible = !hasEntity;

        if (hasEntity)
        {
            _reelViews[index].SetPrototype(symbol.Entity);
            _reelViews[index].ToolTip = Loc.GetString(symbol.Name);
        }
        else
        {
            _reels[index].Text = Loc.GetString(symbol.Name);
            _reels[index].FontColorOverride = symbol.Color;
        }
    }

    /// <summary>
    /// "XENO XENO XENO" in the payout table, as sprites when the symbol has an entity.
    /// </summary>
    private Control SymbolRun(ContactSlotSymbol symbol, int count)
    {
        var box = HBox(2);
        for (var i = 0; i < count; i++)
        {
            if (symbol.Entity is { } entity)
            {
                var view = new EntityPrototypeView { SetSize = new Vector2(32, 32), ToolTip = Loc.GetString(symbol.Name) };
                view.SetPrototype(entity);
                box.AddChild(view);
            }
            else
            {
                box.AddChild(Text(Loc.GetString(symbol.Name), symbol.Color, _font));
            }
        }

        return box;
    }

    #endregion

    #region Blackjack

    public void BlackjackState(ContactBlackjackStateMessage msg)
    {
        _bjState = msg;
        _bjDeadline = _timing.RealTime + TimeSpan.FromSeconds(msg.SecondsLeft);
        SetBalance(msg.Balance);
        UpdateBlackjack();
    }

    public void SetBalance(int balance)
    {
        var text = Loc.GetString("mriya-contact-store-balance", ("balance", balance)).ToUpperInvariant();
        _slotBalance.Text = text;
        _bjBalance.Text = text;
    }

    private void UpdateBlackjack()
    {
        var state = _bjState;
        _bjDealer.RemoveAllChildren();
        _bjSeats.RemoveAllChildren();

        var phase = state?.Phase ?? ContactBlackjackPhase.Idle;
        UpdateStatus();

        if (state != null)
        {
            AddCards(_bjDealer, state.Dealer);
            if (state.Dealer.Count > 0)
                _bjDealer.AddChild(Text($"= {ContactCards.Total(state.Dealer)}", White, _font));
        }

        var mine = state?.Seats.FirstOrDefault(s => s.Mine);
        if (state == null || state.Seats.Count == 0)
            _bjSeats.AddChild(Text(Loc.GetString("mriya-contact-casino-empty-table"), Teal, _font));

        foreach (var seat in state?.Seats ?? new())
        {
            var frame = new PanelContainer { PanelOverride = Box(Bg, seat.Turn ? Phosphor : seat.Mine ? White : Dim, seat.Turn ? 2 : 1, 6) };
            var row = HBox(10);

            var name = seat.Mine ? Loc.GetString("mriya-contact-casino-you", ("name", seat.Name)) : seat.Name;
            row.AddChild(Text($"{(seat.Turn ? ">" : " ")}{name}", seat.Mine ? White : Phosphor, _font));
            row.AddChild(Text(seat.Bet > 0 ? $"[{seat.Bet}{(seat.Doubled ? " ×2" : string.Empty)}]" : "[-]", Gold, _font));

            var cards = HBox(4);
            cards.HorizontalExpand = true;
            AddCards(cards, seat.Cards);
            if (seat.Cards.Count > 0)
                cards.AddChild(Text($"= {ContactCards.Total(seat.Cards)}", White, _font));
            row.AddChild(cards);

            if (seat.Outcome != ContactBlackjackOutcome.None)
            {
                var outcome = Loc.GetString($"mriya-contact-casino-outcome-{seat.Outcome.ToString().ToLowerInvariant()}");
                if (seat.Payout > 0)
                    outcome += $" +{seat.Payout}";

                row.AddChild(Text(outcome.ToUpperInvariant(), seat.Payout > seat.Bet ? Gold : seat.Payout > 0 ? White : Red, _font));
            }

            frame.AddChild(row);
            _bjSeats.AddChild(frame);
        }

        var seated = state?.Seated ?? false;
        var canBet = seated && mine is { Bet: 0 } && phase is ContactBlackjackPhase.Idle or ContactBlackjackPhase.Betting;
        var myTurn = mine is { Turn: true } && phase == ContactBlackjackPhase.Playing;

        _sit.Disabled = seated || (state?.TableFull ?? false);
        _leave.Disabled = !seated || phase == ContactBlackjackPhase.Playing;
        foreach (var (button, amount) in _betButtons)
        {
            button.Disabled = !canBet || amount > (state?.Balance ?? 0);
        }

        _hit.Disabled = !myTurn;
        _stand.Disabled = !myTurn;
        _double.Disabled = !myTurn || mine!.Cards.Count != 2 || mine.Doubled || mine.Bet > state!.Balance;
    }

    private void UpdateStatus()
    {
        var phase = _bjState?.Phase ?? ContactBlackjackPhase.Idle;
        var seconds = (int) Math.Ceiling(Math.Max(0, (_bjDeadline - _timing.RealTime).TotalSeconds));
        _bjStatus.Text = Loc.GetString($"mriya-contact-casino-phase-{phase.ToString().ToLowerInvariant()}", ("seconds", seconds))
            .ToUpperInvariant();
    }

    /// <summary>
    /// RMC playing card sprites (same as the physical deck); the name is the tooltip.
    /// </summary>
    private void AddCards(BoxContainer box, List<byte> cards)
    {
        foreach (var card in cards)
        {
            box.AddChild(new TextureRect
            {
                Texture = _sprite.Frame0(new SpriteSpecifier.Rsi(CardRsi, ContactCards.StateName(card))),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                SetSize = CardSize,
                ToolTip = ContactCards.Name(card),
                MouseFilter = MouseFilterMode.Pass,
                VerticalAlignment = VAlignment.Center,
            });
        }
    }

    #endregion

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var now = _timing.RealTime;

        if (_bjState is { Phase: not ContactBlackjackPhase.Idle })
            UpdateStatus();

        if (!_spinning || _slotRules == null)
            return;

        var elapsed = now - _spinStarted;

        // result in and the reels have spun long enough, or the server never answered (not enough points)
        if (_pendingResult != null && elapsed >= SpinTime || elapsed >= SpinTimeout)
        {
            FinishSpin();
            return;
        }

        if (now < _nextReelStep)
            return;

        _nextReelStep = now + ReelStep;
        for (var i = 0; i < _reels.Length; i++)
        {
            SetReel(i, _random.Pick(_slotRules.Symbols));
        }
    }
}
