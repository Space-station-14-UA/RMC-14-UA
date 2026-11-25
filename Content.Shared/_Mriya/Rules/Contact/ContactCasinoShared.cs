using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared._RMC14.PlayingCards;
using Robust.Shared.Audio;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Gives an entity's UI the Contact games: the store terminal (games tab), a slot machine, a blackjack table.
/// Games are played over whatever UI key the entity's UI uses (ContactStoreUiKey or ContactCasinoUiKey).
/// There is one blackjack table per round: every entity with Blackjack joins the same game.
/// </summary>
[RegisterComponent]
public sealed partial class ContactCasinoComponent : Component
{
    [DataField]
    public bool Slots = true;

    [DataField]
    public bool Blackjack = true;

    /// <summary>
    /// Slot reels / limits for this entity. Blackjack always uses the default rules (one shared table).
    /// </summary>
    [DataField]
    public ProtoId<ContactCasinoPrototype> Rules = ContactCasinoPrototype.Default;
}

[Serializable, NetSerializable]
public enum ContactCasinoUiKey : byte
{
    Key,
}

/// <summary>
/// Bets, reels and payouts. YAML type: contactCasino.
/// </summary>
[Prototype]
public sealed partial class ContactCasinoPrototype : IPrototype
{
    public static readonly ProtoId<ContactCasinoPrototype> Default = "MRContactCasinoDefault";

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<int> SlotBets = new() { 5, 10, 25, 50 };

    /// <summary>
    /// Reel symbols; each reel rolls one independently by weight.
    /// </summary>
    [DataField(required: true)]
    public List<ContactSlotSymbol> Symbols = new();

    [DataField]
    public List<int> BlackjackBets = new() { 5, 10, 25, 50, 100 };

    [DataField]
    public int BlackjackSeats = 5;

    [DataField]
    public int BlackjackDecks = 6;

    /// <summary>
    /// Betting closes this long after the first bet.
    /// </summary>
    [DataField]
    public TimeSpan BettingTime = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A player who doesn't act in time stands.
    /// </summary>
    [DataField]
    public TimeSpan TurnTime = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Results stay on the table this long before the next round.
    /// </summary>
    [DataField]
    public TimeSpan ResultsTime = TimeSpan.FromSeconds(6);

    /// <summary>
    /// When the last reel stops on the client; the win sound plays then.
    /// </summary>
    [DataField]
    public TimeSpan SpinTime = TimeSpan.FromSeconds(1.9);

    [DataField]
    public SoundSpecifier? LeverSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    /// <summary>
    /// Client: every reel step (each symbol rolling by).
    /// </summary>
    [DataField]
    public SoundSpecifier? TickSound = new SoundPathSpecifier("/Audio/Items/pen_click.ogg", AudioParams.Default.WithVolume(-6f));

    /// <summary>
    /// Client: each reel stopping.
    /// </summary>
    [DataField]
    public SoundSpecifier? StopSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    [DataField]
    public SoundSpecifier? WinSound = new SoundPathSpecifier("/Audio/Effects/Arcade/win.ogg");

    [DataField]
    public SoundSpecifier? JackpotSound = new SoundPathSpecifier("/Audio/Machines/vending_jingle.ogg");

    /// <summary>
    /// A win of bet × this or more plays JackpotSound instead of WinSound.
    /// </summary>
    [DataField]
    public int JackpotMultiplier = 20;
}

[DataDefinition]
public sealed partial class ContactSlotSymbol
{
    /// <summary>
    /// Short text on the reel (the store is a text terminal).
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public Color Color = Color.FromHex("#0AFE60");

    [DataField]
    public int Weight = 1;

    /// <summary>
    /// Three of this symbol: bet × this.
    /// </summary>
    [DataField]
    public int Three;

    /// <summary>
    /// Exactly two of this symbol: bet × this (0 = nothing).
    /// </summary>
    [DataField]
    public int Two;

    /// <summary>
    /// Shown on the reel instead of the text (Name is still the tooltip / payout table text).
    /// </summary>
    [DataField]
    public EntProtoId? Entity;
}

#region Messages

/// <summary>
/// Balance for whoever has a casino UI open: on open and every second (locks bets you can't pay).
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactCasinoBalanceMessage(int balance) : BoundUserInterfaceMessage
{
    public readonly int Balance = balance;
}

[Serializable, NetSerializable]
public sealed class ContactSlotSpinMessage(int bet) : BoundUserInterfaceMessage
{
    public readonly int Bet = bet;
}

[Serializable, NetSerializable]
public sealed class ContactSlotResultMessage(int[] reels, int bet, int payout, int balance) : BoundUserInterfaceMessage
{
    /// <summary>
    /// Symbol index per reel.
    /// </summary>
    public readonly int[] Reels = reels;

    public readonly int Bet = bet;
    public readonly int Payout = payout;
    public readonly int Balance = balance;
}

[Serializable, NetSerializable]
public enum ContactBlackjackAction : byte
{
    Sit,
    Leave,
    Bet,
    Hit,
    Stand,
    Double,
}

[Serializable, NetSerializable]
public sealed class ContactBlackjackActionMessage(ContactBlackjackAction action, int amount = 0) : BoundUserInterfaceMessage
{
    public readonly ContactBlackjackAction Action = action;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public enum ContactBlackjackPhase : byte
{
    /// <summary>Nobody has bet yet.</summary>
    Idle,
    Betting,
    Playing,
    Results,
}

[Serializable, NetSerializable]
public enum ContactBlackjackOutcome : byte
{
    None,
    Blackjack,
    Win,
    Push,
    Lose,
    Bust,
}

[Serializable, NetSerializable]
public sealed class ContactBlackjackSeatData
{
    public string Name = string.Empty;
    public bool Mine;
    public bool Turn;
    public int Bet;
    public bool Doubled;
    public List<byte> Cards = new();
    public ContactBlackjackOutcome Outcome;
    public int Payout;
}

/// <summary>
/// The table as one player sees it (Mine marks their seat). Sent to every viewer on change and every second.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactBlackjackStateMessage : BoundUserInterfaceMessage
{
    public ContactBlackjackPhase Phase;
    public float SecondsLeft;
    public int Balance;
    public bool Seated;
    public bool TableFull;
    public List<ContactBlackjackSeatData> Seats = new();

    /// <summary>
    /// <see cref="ContactCards.Hidden"/> = face down.
    /// </summary>
    public List<byte> Dealer = new();
}

#endregion

/// <summary>
/// Card = byte 0..51: rank = card % 13 (0 = ace, 10..12 = J Q K), suit = card / 13.
/// </summary>
public static class ContactCards
{
    public const byte Hidden = 255;

    private static readonly string[] Ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
    private static readonly string[] Suits = { "♠", "♥", "♦", "♣" };

    public static string Name(byte card)
    {
        return card == Hidden ? "??" : Ranks[card % 13] + Suits[card / 13 % 4];
    }

    /// <summary>
    /// RMC's playing card sprite: _RMC14/Objects/Fun/playing_cards.rsi, "spades_ace" ... "back_deck".
    /// Suit order here matches RMC's CardSuit (spades, hearts, diamonds, clubs).
    /// </summary>
    public static string StateName(byte card)
    {
        return card == Hidden
            ? "back_deck"
            : SharedPlayingCardSystem.GetCardStateName((CardSuit) (card / 13 % 4), (CardRank) (card % 13 + 1));
    }

    public static bool IsRed(byte card)
    {
        return card != Hidden && card / 13 % 4 is 1 or 2;
    }

    /// <summary>
    /// Best total (aces 11 while that doesn't bust). Hidden cards don't count.
    /// </summary>
    public static int Total(IEnumerable<byte> cards, out bool soft)
    {
        var total = 0;
        var aces = 0;
        foreach (var card in cards)
        {
            if (card == Hidden)
                continue;

            var rank = card % 13;
            if (rank == 0)
            {
                aces++;
                total += 11;
            }
            else
            {
                total += Math.Min(rank + 1, 10);
            }
        }

        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }

        soft = aces > 0;
        return total;
    }

    public static int Total(IEnumerable<byte> cards)
    {
        return Total(cards, out _);
    }

    public static bool IsBlackjack(List<byte> cards)
    {
        return cards.Count == 2 && Total(cards) == 21;
    }
}
