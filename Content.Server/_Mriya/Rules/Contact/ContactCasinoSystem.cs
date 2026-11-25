using System.Linq;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact games, paid with Є-бали. Everything (reels, shoe, dealer) is decided here; clients only send buttons.
/// Slots: solo, per entity rules. Blackjack: one table for the round, shared by every casino entity
/// (store terminals, tablets, tables). Seats are by player, so death / respawn keeps the seat.
/// Whoever isn't looking at a casino UI when their turn comes stands automatically.
/// </summary>
public sealed class ContactCasinoSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ContactEconomySystem _economy = default!;

    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Open casino UIs: actor -> (entity, key). Table updates go to all of them.
    /// </summary>
    private readonly Dictionary<EntityUid, (EntityUid Source, Enum Key)> _viewers = new();

    private readonly List<Seat> _seats = new();
    private readonly List<byte> _dealer = new();
    private readonly List<byte> _shoe = new();
    private ContactBlackjackPhase _phase = ContactBlackjackPhase.Idle;
    private TimeSpan _phaseEnds;
    private int _turn = -1;
    private bool _dealerRevealed;
    private TimeSpan _nextRefresh;

    private sealed class Seat
    {
        public NetUserId User;
        public string Name = string.Empty;
        public int Bet;
        public bool Doubled;
        public bool Done;
        public readonly List<byte> Cards = new();
        public ContactBlackjackOutcome Outcome;
        public int Payout;
    }

    public override void Initialize()
    {
        // one subscription per message: the key (store or casino UI) is whatever the entity uses
        SubscribeLocalEvent<ContactCasinoComponent, BoundUIOpenedEvent>(OnOpened);
        SubscribeLocalEvent<ContactCasinoComponent, BoundUIClosedEvent>(OnClosed);
        SubscribeLocalEvent<ContactCasinoComponent, ContactSlotSpinMessage>(OnSpin);
        SubscribeLocalEvent<ContactCasinoComponent, ContactBlackjackActionMessage>(OnBlackjack);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private ContactCasinoPrototype TableRules => _prototypes.Index(ContactCasinoPrototype.Default);

    private static bool IsCasinoKey(Enum key)
    {
        return key is ContactCasinoUiKey or ContactStoreUiKey;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _viewers.Clear();
        _seats.Clear();
        _dealer.Clear();
        _shoe.Clear();
        _phase = ContactBlackjackPhase.Idle;
        _turn = -1;
    }

    private void OnOpened(Entity<ContactCasinoComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!IsCasinoKey(args.UiKey) || !ent.Comp.Blackjack)
            return;

        _viewers[args.Actor] = (ent, args.UiKey);
        SendTable(args.Actor);
    }

    private void OnClosed(Entity<ContactCasinoComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_viewers.TryGetValue(args.Actor, out var viewer) && viewer.Source == ent.Owner && Equals(viewer.Key, args.UiKey))
            _viewers.Remove(args.Actor);
    }

    #region Slots

    private void OnSpin(Entity<ContactCasinoComponent> ent, ref ContactSlotSpinMessage args)
    {
        if (!ent.Comp.Slots ||
            !_prototypes.TryIndex(ent.Comp.Rules, out var rules) ||
            rules.Symbols.Count == 0 ||
            !rules.SlotBets.Contains(args.Bet))
        {
            return;
        }

        var user = args.Actor;
        if (!_economy.TrySpend(user, args.Bet))
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-store-not-enough"), ent, user);
            return;
        }

        var reels = new int[3];
        for (var i = 0; i < reels.Length; i++)
        {
            reels[i] = RollSymbol(rules);
        }

        var payout = args.Bet * SlotMultiplier(rules, reels);
        if (payout > 0 && _economy.TryGetUser(user, out var userId))
            _economy.AddWinnings(userId, payout);

        _ui.ServerSendUiMessage(ent.Owner, args.UiKey,
            new ContactSlotResultMessage(reels, args.Bet, payout, _economy.GetBalance(user)), user);
    }

    private int RollSymbol(ContactCasinoPrototype rules)
    {
        var total = rules.Symbols.Sum(s => Math.Max(s.Weight, 0));
        var roll = _random.Next(Math.Max(total, 1));
        for (var i = 0; i < rules.Symbols.Count; i++)
        {
            roll -= Math.Max(rules.Symbols[i].Weight, 0);
            if (roll < 0)
                return i;
        }

        return rules.Symbols.Count - 1;
    }

    private static int SlotMultiplier(ContactCasinoPrototype rules, int[] reels)
    {
        if (reels[0] == reels[1] && reels[1] == reels[2])
            return rules.Symbols[reels[0]].Three;

        var best = 0;
        foreach (var symbol in reels.Distinct())
        {
            if (reels.Count(r => r == symbol) == 2)
                best = Math.Max(best, rules.Symbols[symbol].Two);
        }

        return best;
    }

    #endregion

    #region Blackjack actions

    private void OnBlackjack(Entity<ContactCasinoComponent> ent, ref ContactBlackjackActionMessage args)
    {
        if (!ent.Comp.Blackjack || !_economy.TryGetUser(args.Actor, out var user))
            return;

        _viewers[args.Actor] = (ent, args.UiKey);
        var rules = TableRules;
        var seat = _seats.FirstOrDefault(s => s.User == user);

        switch (args.Action)
        {
            case ContactBlackjackAction.Sit:
                if (seat == null && _seats.Count < rules.BlackjackSeats)
                    _seats.Add(new Seat { User = user, Name = Name(args.Actor) });
                break;

            case ContactBlackjackAction.Leave:
                // mid-hand the seat order is the turn order: wait for the results
                if (seat == null || _phase == ContactBlackjackPhase.Playing)
                    break;

                // bet placed but cards not dealt yet: give it back
                if (seat.Bet > 0 && _phase == ContactBlackjackPhase.Betting)
                    _economy.AddWinnings(user, seat.Bet);

                _seats.Remove(seat);
                if (_phase == ContactBlackjackPhase.Betting && _seats.All(s => s.Bet == 0))
                    _phase = ContactBlackjackPhase.Idle;
                break;

            case ContactBlackjackAction.Bet:
                if (seat == null ||
                    seat.Bet > 0 ||
                    _phase is not (ContactBlackjackPhase.Idle or ContactBlackjackPhase.Betting) ||
                    !rules.BlackjackBets.Contains(args.Amount))
                {
                    break;
                }

                if (!_economy.TrySpend(args.Actor, args.Amount))
                {
                    _popup.PopupEntity(Loc.GetString("mriya-contact-store-not-enough"), ent, args.Actor);
                    break;
                }

                seat.Bet = args.Amount;
                if (_phase == ContactBlackjackPhase.Idle)
                {
                    _phase = ContactBlackjackPhase.Betting;
                    _phaseEnds = _timing.CurTime + rules.BettingTime;
                }

                break;

            case ContactBlackjackAction.Hit:
                if (IsTurn(seat))
                    Hit(seat!);
                break;

            case ContactBlackjackAction.Stand:
                if (IsTurn(seat))
                    EndTurn(seat!);
                break;

            case ContactBlackjackAction.Double:
                if (!IsTurn(seat) || seat!.Cards.Count != 2 || seat.Doubled)
                    break;

                if (!_economy.TrySpend(args.Actor, seat.Bet))
                {
                    _popup.PopupEntity(Loc.GetString("mriya-contact-store-not-enough"), ent, args.Actor);
                    break;
                }

                seat.Bet *= 2;
                seat.Doubled = true;
                seat.Cards.Add(Draw());
                EndTurn(seat);
                break;
        }

        SendTableToAll();
    }

    private bool IsTurn(Seat? seat)
    {
        return seat != null && _phase == ContactBlackjackPhase.Playing && _turn >= 0 && _turn < _seats.Count && _seats[_turn] == seat;
    }

    private void Hit(Seat seat)
    {
        seat.Cards.Add(Draw());
        if (ContactCards.Total(seat.Cards) >= 21)
            EndTurn(seat);
        else
            _phaseEnds = _timing.CurTime + TableRules.TurnTime;
    }

    private void EndTurn(Seat seat)
    {
        seat.Done = true;
        NextTurn();
    }

    #endregion

    #region Round flow

    private void Deal()
    {
        var rules = TableRules;
        if (_shoe.Count < 52)
            Shuffle(rules);

        _dealer.Clear();
        _dealerRevealed = false;
        foreach (var seat in _seats)
        {
            seat.Cards.Clear();
            seat.Doubled = false;
            seat.Outcome = ContactBlackjackOutcome.None;
            seat.Payout = 0;
            seat.Done = seat.Bet == 0;
        }

        for (var round = 0; round < 2; round++)
        {
            foreach (var seat in _seats)
            {
                if (seat.Bet > 0)
                    seat.Cards.Add(Draw());
            }

            _dealer.Add(Draw());
        }

        // naturals don't play; a dealer natural ends the round right away
        foreach (var seat in _seats)
        {
            if (seat.Bet > 0 && ContactCards.IsBlackjack(seat.Cards))
                seat.Done = true;
        }

        _phase = ContactBlackjackPhase.Playing;
        _turn = -1;

        if (ContactCards.IsBlackjack(_dealer))
        {
            FinishRound();
            return;
        }

        NextTurn();
    }

    private void NextTurn()
    {
        for (var i = _turn + 1; i < _seats.Count; i++)
        {
            // sat down mid-round / no bet: sits this one out
            if (_seats[i].Done || _seats[i].Bet == 0)
                continue;

            _turn = i;
            _phaseEnds = _timing.CurTime + TableRules.TurnTime;
            return;
        }

        FinishRound();
    }

    /// <summary>
    /// Dealer reveals and draws to 17 (stands on soft 17), then everyone is paid.
    /// </summary>
    private void FinishRound()
    {
        _turn = -1;
        _dealerRevealed = true;

        var anyAlive = _seats.Any(s => s.Bet > 0 && ContactCards.Total(s.Cards) <= 21 && !ContactCards.IsBlackjack(s.Cards));
        if (anyAlive && !ContactCards.IsBlackjack(_dealer))
        {
            while (ContactCards.Total(_dealer) < 17)
            {
                _dealer.Add(Draw());
            }
        }

        var dealerTotal = ContactCards.Total(_dealer);
        var dealerBlackjack = ContactCards.IsBlackjack(_dealer);

        foreach (var seat in _seats)
        {
            if (seat.Bet == 0)
                continue;

            var total = ContactCards.Total(seat.Cards);
            var blackjack = ContactCards.IsBlackjack(seat.Cards);

            (seat.Outcome, seat.Payout) = true switch
            {
                _ when total > 21 => (ContactBlackjackOutcome.Bust, 0),
                _ when blackjack && dealerBlackjack => (ContactBlackjackOutcome.Push, seat.Bet),
                _ when blackjack => (ContactBlackjackOutcome.Blackjack, seat.Bet + seat.Bet * 3 / 2),
                _ when dealerBlackjack => (ContactBlackjackOutcome.Lose, 0),
                _ when dealerTotal > 21 || total > dealerTotal => (ContactBlackjackOutcome.Win, seat.Bet * 2),
                _ when total == dealerTotal => (ContactBlackjackOutcome.Push, seat.Bet),
                _ => (ContactBlackjackOutcome.Lose, 0),
            };

            if (seat.Payout > 0)
                _economy.AddWinnings(seat.User, seat.Payout);
        }

        _phase = ContactBlackjackPhase.Results;
        _phaseEnds = _timing.CurTime + TableRules.ResultsTime;
    }

    private void ClearRound()
    {
        _dealer.Clear();
        _dealerRevealed = false;
        _turn = -1;
        _phase = ContactBlackjackPhase.Idle;

        // players who walked away lose their seat
        _seats.RemoveAll(s => !IsViewing(s.User));
        foreach (var seat in _seats)
        {
            seat.Cards.Clear();
            seat.Bet = 0;
            seat.Doubled = false;
            seat.Done = false;
            seat.Outcome = ContactBlackjackOutcome.None;
            seat.Payout = 0;
        }
    }

    private void Shuffle(ContactCasinoPrototype rules)
    {
        _shoe.Clear();
        for (var deck = 0; deck < Math.Max(rules.BlackjackDecks, 1); deck++)
        {
            for (byte card = 0; card < 52; card++)
            {
                _shoe.Add(card);
            }
        }

        _random.Shuffle(_shoe);
    }

    private byte Draw()
    {
        if (_shoe.Count == 0)
            Shuffle(TableRules);

        var card = _shoe[^1];
        _shoe.RemoveAt(_shoe.Count - 1);
        return card;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var changed = false;

        switch (_phase)
        {
            case ContactBlackjackPhase.Betting when now >= _phaseEnds:
                Deal();
                changed = true;
                break;

            case ContactBlackjackPhase.Playing when _turn >= 0 && _turn < _seats.Count:
                // timed out, or walked away from the table
                var seat = _seats[_turn];
                if (now >= _phaseEnds || !IsViewing(seat.User))
                {
                    EndTurn(seat);
                    changed = true;
                }

                break;

            case ContactBlackjackPhase.Results when now >= _phaseEnds:
                ClearRound();
                changed = true;
                break;
        }

        if (changed || now >= _nextRefresh && _phase != ContactBlackjackPhase.Idle)
        {
            _nextRefresh = now + RefreshEvery;
            SendTableToAll();
        }
    }

    #endregion

    #region State

    private bool IsViewing(NetUserId user)
    {
        foreach (var actor in _viewers.Keys)
        {
            if (_economy.TryGetUser(actor, out var viewer) && viewer == user)
                return true;
        }

        return false;
    }

    private void SendTableToAll()
    {
        foreach (var actor in _viewers.Keys.ToList())
        {
            SendTable(actor);
        }
    }

    private void SendTable(EntityUid actor)
    {
        if (!_viewers.TryGetValue(actor, out var viewer))
            return;

        if (TerminatingOrDeleted(viewer.Source) || !_ui.IsUiOpen(viewer.Source, viewer.Key, actor))
        {
            _viewers.Remove(actor);
            return;
        }

        _economy.TryGetUser(actor, out var user);
        var msg = new ContactBlackjackStateMessage
        {
            Phase = _phase,
            SecondsLeft = _phase == ContactBlackjackPhase.Idle
                ? 0
                : MathF.Max(0, (float) (_phaseEnds - _timing.CurTime).TotalSeconds),
            Balance = _economy.GetBalance(actor),
            TableFull = _seats.Count >= TableRules.BlackjackSeats,
        };

        for (var i = 0; i < _seats.Count; i++)
        {
            var seat = _seats[i];
            var mine = seat.User == user;
            msg.Seated |= mine;
            msg.Seats.Add(new ContactBlackjackSeatData
            {
                Name = seat.Name,
                Mine = mine,
                Turn = i == _turn,
                Bet = seat.Bet,
                Doubled = seat.Doubled,
                Cards = seat.Cards.ToList(),
                Outcome = seat.Outcome,
                Payout = seat.Payout,
            });
        }

        for (var i = 0; i < _dealer.Count; i++)
        {
            // hole card stays face down until the dealer plays
            msg.Dealer.Add(i == 1 && !_dealerRevealed ? ContactCards.Hidden : _dealer[i]);
        }

        _ui.ServerSendUiMessage(viewer.Source, viewer.Key, msg, actor);
    }

    #endregion
}
