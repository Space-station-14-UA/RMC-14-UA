using Content.Server._RMC14.SupplyDrop;
using Content.Shared._Mriya.ATM;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Players;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Mriya.ATM;

public sealed class ATMSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SupplyDropSystem _supplyDrop = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly string[] LinkSyllables =
    {
        "MU", "TH", "UR", "KA", "RI", "ZO", "VE", "NA", "LO", "SY", "XE", "NO", "TE", "RA", "MI", "QU",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ATMComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ATMComponent, BeforeActivatableUIOpenEvent>(OnBeforeOpen);
        SubscribeLocalEvent<ATMComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);

        Subs.BuiEvents<ATMComponent>(ATMUIKey.Key, subs =>
        {
            subs.Event<ATMBuyMsg>(OnBuy);
            subs.Event<ATMWithdrawMsg>(OnWithdraw);
        });
    }

    private void OnMapInit(Entity<ATMComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.TerminalNumber = _random.Next(1, 100);
        var a = _random.Pick(LinkSyllables);
        var b = _random.Pick(LinkSyllables);
        var c = _random.Pick(LinkSyllables);
        ent.Comp.LinkCode = $"{a}-{b}-{c}{_random.Next(10, 100)}";
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        EnsureComp<BankAccountComponent>(ev.Entity);
    }

    private void OnBeforeOpen(Entity<ATMComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        var account = EnsureComp<BankAccountComponent>(args.User);
        SendState(ent, account);
    }

    private void OnInteractUsing(Entity<ATMComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp(args.User, out BankAccountComponent? account))
            return;

        if (!TryComp(args.Used, out StackComponent? stack) || stack.StackTypeId != "Dollar")
            return;

        var count = _stack.GetCount(args.Used, stack);
        account.Balance += count;
        Dirty(args.User, account);
        QueueDel(args.Used);
        args.Handled = true;
        _popup.PopupEntity($"Внесено {count}$.", args.User, args.User);
        SendState(ent, account);
    }

    private void OnBuy(Entity<ATMComponent> ent, ref ATMBuyMsg args)
    {
        if (!_proto.TryIndex(ent.Comp.Shop, out var shop))
            return;

        if (args.Category < 0 || args.Category >= shop.Categories.Count)
            return;

        var entries = shop.Categories[args.Category].Entries;
        if (args.Entry < 0 || args.Entry >= entries.Count)
            return;

        var account = EnsureComp<BankAccountComponent>(args.Actor);
        var order = entries[args.Entry];

        if (account.Balance < order.Cost)
        {
            _popup.PopupEntity("Недостатньо коштів.", args.Actor, args.Actor);
            return;
        }

        account.Balance -= order.Cost;
        Dirty(args.Actor, account);
        _audio.PlayPvs(ent.Comp.PurchaseSound, ent.Owner);

        var crate = Spawn(order.Case, MapCoordinates.Nullspace);

        var coords = _transform.GetMapCoordinates(args.Actor);
        const float skyFall = 5f;
        const float drop = 5f;
        _supplyDrop.LaunchSupplyDrop(crate, coords, skyFall, drop, TimeSpan.FromSeconds(skyFall + drop));
        _popup.PopupEntity("Замовлення прийнято. Дивіться догори.", args.Actor, args.Actor);

        SendState(ent, account);
    }

    private void OnWithdraw(Entity<ATMComponent> ent, ref ATMWithdrawMsg args)
    {
        var account = EnsureComp<BankAccountComponent>(args.Actor);

        var amount = Math.Min(args.Amount, account.Balance);
        if (amount <= 0)
        {
            _popup.PopupEntity("Недостатньо коштів.", args.Actor, args.Actor);
            return;
        }

        account.Balance -= amount;
        Dirty(args.Actor, account);

        var cash = Spawn("RMCSpaceCash1", _transform.GetMapCoordinates(args.Actor));
        if (TryComp(cash, out StackComponent? stack))
            _stack.SetCount(cash, amount, stack);

        _hands.PickupOrDrop(args.Actor, cash);
        _popup.PopupEntity($"Знято {amount}$.", args.Actor, args.Actor);

        SendState(ent, account);
    }

    private void SendState(Entity<ATMComponent> ent, BankAccountComponent account)
    {
        _ui.SetUiState(ent.Owner, ATMUIKey.Key, BuildState(ent, account));
    }

    private ATMBuiState BuildState(Entity<ATMComponent> ent, BankAccountComponent account)
    {
        var categories = new List<ATMBuiCategory>();

        if (_proto.TryIndex(ent.Comp.Shop, out var shop))
        {
            foreach (var category in shop.Categories)
            {
                var entries = new List<ATMBuiEntry>();
                foreach (var entry in category.Entries)
                    entries.Add(new ATMBuiEntry(entry.Name, entry.Cost, entry.Preview ?? entry.Case));

                categories.Add(new ATMBuiCategory(category.Name, entries));
            }
        }

        var mapId = _transform.GetMapId(ent.Owner);
        var mapName = "НЕВІДОМО";
        if (mapId != MapId.Nullspace)
        {
            var mapUid = _map.GetMapEntityId(mapId);
            var name = mapUid.IsValid() ? Name(mapUid) : string.Empty;
            if (!string.IsNullOrEmpty(name))
                mapName = name.ToUpperInvariant();
        }

        return new ATMBuiState(account.Balance, Name(account.Owner), mapName, ent.Comp.TerminalNumber, ent.Comp.LinkCode, categories);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<BankAccountComponent>();
        while (query.MoveNext(out var uid, out var account))
        {
            if (account.NextGain == default)
                account.NextGain = time + account.GainEvery;

            if (time < account.NextGain)
                continue;

            account.NextGain = time + account.GainEvery;
            account.Balance += account.GainAmount;
            Dirty(uid, account);
        }
    }
}
