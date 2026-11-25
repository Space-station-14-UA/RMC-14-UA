using System.Linq;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using static Content.Client._Mriya.Rules.Contact.ContactRetro;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Three reels × three rows (the middle row is the payline), a lever and a spin button.
/// Pick a bet, pull the lever. Reels stop left to right; the server already decided the result.
/// </summary>
public sealed class ContactSlotMachineControl : BoxContainer
{
    private const int Reels = 3;
    private const int Rows = 3;
    private const int Payline = 1;

    private static readonly TimeSpan ReelStep = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan ReelStagger = TimeSpan.FromSeconds(0.35);
    private static readonly TimeSpan SpinTimeout = TimeSpan.FromSeconds(5);

    private readonly IGameTiming _timing;
    private readonly IRobustRandom _random;
    private readonly ContactCasinoPrototype _rules;

    private readonly Cell[,] _cells = new Cell[Reels, Rows];
    private readonly int[,] _shown = new int[Reels, Rows];
    private readonly bool[] _stopped = new bool[Reels];
    private readonly List<(RetroButton Button, int Bet)> _betButtons = new();
    private readonly RetroButton _spinButton;
    private readonly ContactSlotLever _lever;
    private readonly Label _balanceLabel;
    private readonly Label _result;

    private int _bet;
    private int _balance;
    private bool _spinning;
    private TimeSpan _spinStarted;
    private TimeSpan _nextStep;
    private ContactSlotResultMessage? _pending;

    private readonly SharedAudioSystem _audio;

    public event Action<int>? Spin;

    public ContactSlotMachineControl(ContactCasinoPrototype rules, Font font, Font fontBig)
    {
        _timing = IoCManager.Resolve<IGameTiming>();
        _random = IoCManager.Resolve<IRobustRandom>();
        _audio = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SharedAudioSystem>();
        _rules = rules;
        _bet = rules.SlotBets.FirstOrDefault();

        Orientation = LayoutOrientation.Vertical;
        SeparationOverride = 10;

        _balanceLabel = Text(string.Empty, White, font);
        AddChild(_balanceLabel);

        // reels + lever
        var machine = HBox(14);
        machine.HorizontalAlignment = HAlignment.Center;

        var reelFrame = new PanelContainer { PanelOverride = Box(Black, Phosphor, 2, 10) };
        var reelRow = HBox(8);
        reelRow.AddChild(PaylineArrow(">", font));
        for (var reel = 0; reel < Reels; reel++)
        {
            var column = VBox(4);
            for (var row = 0; row < Rows; row++)
            {
                var cell = new Cell(fontBig, row == Payline);
                _cells[reel, row] = cell;
                column.AddChild(cell);
                SetCell(reel, row, _random.Next(rules.Symbols.Count));
            }

            reelRow.AddChild(column);
        }

        reelRow.AddChild(PaylineArrow("<", font));
        reelFrame.AddChild(reelRow);
        machine.AddChild(reelFrame);

        _lever = new ContactSlotLever { VerticalAlignment = VAlignment.Center };
        _lever.Pulled += () => StartSpin();
        machine.AddChild(_lever);
        AddChild(machine);

        _result = Text(string.Empty, Phosphor, font);
        _result.HorizontalAlignment = HAlignment.Center;
        AddChild(_result);

        // bet picker + spin
        var controls = HBox();
        controls.HorizontalAlignment = HAlignment.Center;
        controls.AddChild(Text(Loc.GetString("mriya-contact-casino-bet").ToUpperInvariant(), White, font));
        foreach (var bet in rules.SlotBets)
        {
            var amount = bet;
            var button = new RetroButton($">{bet}", font) { MinWidth = 56 };
            button.OnPressed += _ =>
            {
                _bet = amount;
                UpdateButtons();
            };
            _betButtons.Add((button, bet));
            controls.AddChild(button);
        }

        _spinButton = new RetroButton(string.Empty, font) { MinWidth = 120, Margin = new Thickness(12, 0, 0, 0) };
        _spinButton.OnPressed += _ => _lever.Pull();
        controls.AddChild(_spinButton);
        AddChild(controls);

        // payout table
        AddChild(Text($">{Loc.GetString("mriya-contact-casino-payouts").ToUpperInvariant()}", White, font));
        foreach (var symbol in rules.Symbols.Where(s => s.Three > 0 || s.Two > 0).OrderByDescending(s => s.Three))
        {
            var row = HBox(12);
            row.AddChild(SymbolRun(symbol, 3, font));
            row.AddChild(new Control { HorizontalExpand = true });
            row.AddChild(Text($"×{symbol.Three}", White, font));
            if (symbol.Two > 0)
            {
                row.AddChild(SymbolRun(symbol, 2, font));
                row.AddChild(Text($"×{symbol.Two}", White, font));
            }

            AddChild(row);
        }

        SetBalance(0);
    }

    private static Label PaylineArrow(string arrow, Font font)
    {
        var label = Text(arrow, Gold, font);
        label.VerticalAlignment = VAlignment.Center;
        return label;
    }

    private Control SymbolRun(ContactSlotSymbol symbol, int count, Font font)
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
                box.AddChild(Text(Loc.GetString(symbol.Name), symbol.Color, font));
            }
        }

        return box;
    }

    #region State from the BUI

    public void SetBalance(int balance)
    {
        _balance = balance;

        // mid-spin the balance already includes the win: don't spoil it, FinishSpin shows it
        if (_spinning)
            return;

        _balanceLabel.Text = Loc.GetString("mriya-contact-store-balance", ("balance", balance)).ToUpperInvariant();
        UpdateButtons();
    }

    public void Result(ContactSlotResultMessage msg)
    {
        _pending = msg;
    }

    #endregion

    #region Spin

    private bool CanSpin => !_spinning && _bet > 0 && _balance >= _bet;

    private void UpdateButtons()
    {
        foreach (var (button, bet) in _betButtons)
        {
            button.Selected = bet == _bet;
            button.Disabled = _spinning || bet > _balance;
        }

        _spinButton.Text = Loc.GetString("mriya-contact-casino-spin", ("bet", _bet)).ToUpperInvariant();
        _spinButton.Disabled = !CanSpin;
        _lever.Disabled = !CanSpin;
    }

    private void StartSpin()
    {
        if (!CanSpin)
            return;

        _spinning = true;
        _spinStarted = _timing.RealTime;
        _pending = null;
        for (var i = 0; i < Reels; i++)
        {
            _stopped[i] = false;
        }

        _result.Text = string.Empty;
        UpdateButtons();

        Spin?.Invoke(_bet);
    }

    private void StopReel(int reel, int symbol)
    {
        _stopped[reel] = true;
        SetCell(reel, 0, _random.Next(_rules.Symbols.Count));
        SetCell(reel, Payline, symbol);
        SetCell(reel, 2, _random.Next(_rules.Symbols.Count));
        _audio.PlayGlobal(_rules.StopSound, Filter.Local(), false);
    }

    private void FinishSpin()
    {
        _spinning = false;

        if (_pending is { } result)
        {
            _result.Text = result.Payout > 0
                ? Loc.GetString("mriya-contact-casino-slot-win", ("amount", result.Payout)).ToUpperInvariant()
                : Loc.GetString("mriya-contact-casino-slot-lose").ToUpperInvariant();
            _result.FontColorOverride = result.Payout > 0 ? Gold : Teal;
            SetBalance(result.Balance);

            if (result.Payout > 0)
            {
                var jackpot = result.Payout >= result.Bet * _rules.JackpotMultiplier;
                _audio.PlayGlobal(jackpot ? _rules.JackpotSound : _rules.WinSound, Filter.Local(), false);
            }

            for (var reel = 0; reel < Reels; reel++)
            {
                _cells[reel, Payline].Highlight(result.Payout > 0);
            }
        }

        _pending = null;
        UpdateButtons();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (!_spinning)
            return;

        var elapsed = _timing.RealTime - _spinStarted;

        // the server never answered (rejected): stop where we are
        if (_pending == null && elapsed >= SpinTimeout)
        {
            FinishSpin();
            return;
        }

        // left to right, the last reel at SpinTime
        if (_pending is { } result)
        {
            for (var reel = 0; reel < Reels; reel++)
            {
                if (_stopped[reel] || elapsed < _rules.SpinTime - ReelStagger * (Reels - 1 - reel))
                    continue;

                StopReel(reel, Math.Clamp(result.Reels[reel], 0, _rules.Symbols.Count - 1));
            }

            if (_stopped.All(s => s))
            {
                FinishSpin();
                return;
            }
        }

        if (_timing.RealTime < _nextStep)
            return;

        _nextStep = _timing.RealTime + ReelStep;

        // rolling reels: everything moves down one row, a new symbol comes in on top
        for (var reel = 0; reel < Reels; reel++)
        {
            if (_stopped[reel])
                continue;

            for (var row = Rows - 1; row > 0; row--)
            {
                SetCell(reel, row, _shown[reel, row - 1]);
            }

            SetCell(reel, 0, _random.Next(_rules.Symbols.Count));
            _cells[reel, Payline].Highlight(false);
        }

        _audio.PlayGlobal(_rules.TickSound, Filter.Local(), false);
    }

    private void SetCell(int reel, int row, int symbol)
    {
        _shown[reel, row] = symbol;
        _cells[reel, row].Set(_rules.Symbols[symbol]);
    }

    #endregion

    /// <summary>
    /// One reel window: symbol text or entity sprite. Rows off the payline are dimmed.
    /// </summary>
    private sealed class Cell : PanelContainer
    {
        private readonly Label _label;
        private readonly EntityPrototypeView _view;
        private readonly bool _payline;

        public Cell(Font font, bool payline)
        {
            _payline = payline;
            MinSize = new Vector2(120, payline ? 72 : 56);
            Modulate = payline ? Color.White : Color.White.WithAlpha(0.35f);

            _label = Text(string.Empty, Phosphor, font);
            _label.HorizontalAlignment = HAlignment.Center;
            _label.VerticalAlignment = VAlignment.Center;

            _view = new EntityPrototypeView
            {
                SetSize = new Vector2(48, 48),
                Scale = new Vector2(1.5f, 1.5f),
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };

            AddChild(_label);
            AddChild(_view);
            Highlight(false);
        }

        public void Set(ContactSlotSymbol symbol)
        {
            var hasEntity = symbol.Entity != null;
            _view.Visible = hasEntity;
            _label.Visible = !hasEntity;
            ToolTip = Loc.GetString(symbol.Name);

            if (hasEntity)
            {
                _view.SetPrototype(symbol.Entity);
            }
            else
            {
                _label.Text = ToolTip;
                _label.FontColorOverride = symbol.Color;
            }
        }

        public void Highlight(bool win)
        {
            PanelOverride = Box(Bg, win ? Gold : _payline ? Phosphor : Dim, _payline ? 2 : 1, 4);
        }
    }
}

/// <summary>
/// Click it: the arm goes down, the spin starts at the bottom, the arm springs back.
/// </summary>
public sealed class ContactSlotLever : Control
{
    private const float DownSeconds = 0.18f;
    private const float UpSeconds = 0.4f;

    private float _progress;
    private int _direction;

    public bool Disabled;

    public event Action? Pulled;

    public ContactSlotLever()
    {
        MinSize = new Vector2(48, 190);
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.Hand;
    }

    public void Pull()
    {
        if (!Disabled && _direction == 0)
            _direction = 1;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        Pull();
        args.Handle();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_direction == 0)
            return;

        if (_direction > 0)
        {
            _progress += args.DeltaSeconds / DownSeconds;
            if (_progress >= 1f)
            {
                _progress = 1f;
                _direction = -1;
                Pulled?.Invoke();
            }
        }
        else
        {
            _progress -= args.DeltaSeconds / UpSeconds;
            if (_progress <= 0f)
            {
                _progress = 0f;
                _direction = 0;
            }
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var scale = UIScale;
        var x = size.X / 2f;
        var pivotY = size.Y * 0.55f;
        var knobY = MathHelper.Lerp(size.Y * 0.12f, size.Y * 0.9f, _progress);
        var halfStick = 3f * scale;
        var knobRadius = 11f * scale;

        // housing on the machine's side
        var housingHalf = new Vector2(size.X * 0.45f, 14f * scale);
        handle.DrawRect(new UIBox2(x - housingHalf.X, pivotY - housingHalf.Y, x + housingHalf.X, pivotY + housingHalf.Y), Deep);
        handle.DrawRect(new UIBox2(x - housingHalf.X, pivotY - housingHalf.Y, x + housingHalf.X, pivotY + housingHalf.Y), Phosphor, false);

        // arm and knob
        var armColor = Disabled ? Dim : Green;
        handle.DrawRect(new UIBox2(x - halfStick, MathF.Min(pivotY, knobY), x + halfStick, MathF.Max(pivotY, knobY)), armColor);
        handle.DrawCircle(new Vector2(x, knobY), knobRadius, Disabled ? Dim : Red);
        handle.DrawCircle(new Vector2(x - knobRadius * 0.3f, knobY - knobRadius * 0.3f), knobRadius * 0.3f, White.WithAlpha(Disabled ? 0.1f : 0.5f));
    }
}