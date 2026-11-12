using System.Linq;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Content.Shared._RMC14.Attachable.Components;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Є-бальний store: buying, kit resupply, delivery (equip -> hand -> ground).
/// </summary>
public sealed class ContactStoreSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ContactEconomySystem _economy = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    private static readonly string[] StoragePriority = { "belt", "pocket1", "pocket2", "suitstorage", "back" };

    private const string MagazineSlot = "gun_magazine";

    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Kits each player took from the loadout vendor.
    /// </summary>
    private readonly Dictionary<NetUserId, HashSet<string>> _kits = new();

    private readonly Dictionary<string, ContactStoreEntryPrototype> _entriesByProto = new();
    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        Subs.BuiEvents<ContactStoreComponent>(ContactStoreUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<ContactStoreBuyMessage>(OnBuy);
            subs.Event<ContactStoreResupplyKitMessage>(OnResupply);
        });

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);

        RebuildIndex();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<ContactStoreEntryPrototype>())
            RebuildIndex();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _kits.Clear();
    }

    private void RebuildIndex()
    {
        _entriesByProto.Clear();
        foreach (var entry in _prototypes.EnumeratePrototypes<ContactStoreEntryPrototype>())
        {
            _entriesByProto.TryAdd(entry.Proto.Id, entry);
        }
    }

    #region Kits

    /// <summary>
    /// Call when a player takes something from the loadout vendor.
    /// Records the kit whose VendorEntry matches.
    /// </summary>
    public void RecordVended(EntityUid player, EntProtoId vended)
    {
        if (!_economy.TryGetUser(player, out var user))
            return;

        foreach (var kit in _prototypes.EnumeratePrototypes<ContactKitPrototype>())
        {
            if (kit.VendorEntry == vended)
                _kits.GetOrNew(user).Add(kit.ID);
        }
    }

    private ContactStoreKitData? GetKitData(string kitId, Dictionary<string, int> carried)
    {
        if (!_prototypes.TryIndex<ContactKitPrototype>(kitId, out var kit))
            return null;

        var price = 0;
        var missing = new Dictionary<string, int>();
        foreach (var item in kit.Items)
        {
            if (!_entriesByProto.TryGetValue(item.Proto.Id, out var entry))
                continue;

            var count = item.Count - carried.GetValueOrDefault(item.Proto.Id);
            if (count <= 0)
                continue;

            missing[item.Proto.Id] = count;
            price += count * entry.Price;
        }

        return new ContactStoreKitData(kitId, price, missing);
    }

    #endregion

    private void OnOpened(Entity<ContactStoreComponent> ent, ref BoundUIOpenedEvent args)
    {
        SendUpdate(ent, args.Actor);
    }

    private void OnBuy(Entity<ContactStoreComponent> ent, ref ContactStoreBuyMessage args)
    {
        var user = args.Actor;
        if (!_prototypes.TryIndex<ContactStoreEntryPrototype>(args.Entry, out var entry))
            return;

        var amount = Math.Clamp(args.Amount, 1, entry.MaxPerPurchase);
        if (!_economy.TrySpend(user, entry.Price * amount))
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-store-not-enough"), ent, user);
            return;
        }

        for (var i = 0; i < amount; i++)
        {
            Deliver(user, entry.Proto);
        }

        SendUpdate(ent, user);
    }

    private void OnResupply(Entity<ContactStoreComponent> ent, ref ContactStoreResupplyKitMessage args)
    {
        var user = args.Actor;
        if (!_economy.TryGetUser(user, out var userId) ||
            !_kits.TryGetValue(userId, out var kits) ||
            !kits.Contains(args.Kit))
        {
            return;
        }

        var carried = ContactStoreHelpers.CountCarried(EntityManager, user);
        if (GetKitData(args.Kit, carried) is not { Price: > 0 } data)
            return;

        if (!_economy.TrySpend(user, data.Price))
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-store-not-enough"), ent, user);
            return;
        }

        foreach (var (proto, count) in data.Missing)
        {
            for (var i = 0; i < count; i++)
            {
                Deliver(user, proto);
            }
        }

        SendUpdate(ent, user);
    }

    private void SendUpdate(Entity<ContactStoreComponent> ent, EntityUid actor)
    {
        var carried = ContactStoreHelpers.CountCarried(EntityManager, actor);

        var compatible = new HashSet<string>();
        foreach (var item in ContactStoreHelpers.EnumerateCarried(EntityManager, actor))
        {
            if (!HasComp<GunComponent>(item))
                continue;

            var whitelists = new List<EntityWhitelist>();

            // magazines
            if (TryComp(item, out ItemSlotsComponent? slots) &&
                _itemSlots.TryGetSlot(item, MagazineSlot, out var magSlot, slots) &&
                magSlot.Whitelist != null)
            {
                whitelists.Add(magSlot.Whitelist);
            }

            // internal ammo (shotguns, U1 launcher, ...)
            if (TryComp(item, out BallisticAmmoProviderComponent? ballistic) && ballistic.Whitelist != null)
                whitelists.Add(ballistic.Whitelist);

            // attachments; locked slots can't be changed, so skip them
            if (TryComp(item, out AttachableHolderComponent? holder))
            {
                foreach (var slot in holder.Slots.Values)
                {
                    if (!slot.Locked && slot.Whitelist != null)
                        whitelists.Add(slot.Whitelist);
                }
            }

            foreach (var entry in _entriesByProto.Values)
            {
                if (whitelists.Any(w => MatchesProto(w, entry.Proto)))
                    compatible.Add(entry.ID);
            }
        }

        var kits = new List<ContactStoreKitData>();
        if (_economy.TryGetUser(actor, out var user) && _kits.TryGetValue(user, out var taken))
        {
            foreach (var kitId in taken)
            {
                if (GetKitData(kitId, carried) is { } data)
                    kits.Add(data);
            }
        }

        var msg = new ContactStoreUpdateMessage(_economy.GetBalance(actor), compatible.ToList(), kits);
        _ui.ServerSendUiMessage(ent.Owner, ContactStoreUiKey.Key, msg, actor);
    }

    /// <summary>
    /// Equip if a free slot fits, otherwise hand, otherwise the ground.
    /// </summary>
    private void Deliver(EntityUid user, EntProtoId proto)
    {
        var item = Spawn(proto, Transform(user).Coordinates);

        if (TryComp(user, out InventoryComponent? inventory))
        {
            foreach (var slot in inventory.Slots)
            {
                if ((slot.SlotFlags & SlotFlags.POCKET) != 0)
                    continue;

                if (_inventory.TryEquip(user, item, slot.Name, silent: true, inventory: inventory))
                    return;
            }

            if (TryInsertIntoStorage(user, item, inventory))
                return;
        }

        _hands.TryPickupAnyHand(user, item);
    }

    private bool TryInsertIntoStorage(EntityUid user, EntityUid item, InventoryComponent inventory)
    {
        var slots = StoragePriority.Concat(inventory.Slots.Select(s => s.Name).Except(StoragePriority));
        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(user, slot, out var worn, inventory) ||
                !HasComp<StorageComponent>(worn))
            {
                continue;
            }

            if (_storage.Insert(worn.Value, item, out _, user: user, playSound: false))
                return true;
        }

        return false;
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        if (time < _nextUpdate)
            return;

        _nextUpdate = time + UpdateEvery;

        var query = EntityQueryEnumerator<ContactStoreComponent>();
        while (query.MoveNext(out var uid, out var store))
        {
            foreach (var actor in _ui.GetActors(uid, ContactStoreUiKey.Key).ToList())
            {
                SendUpdate((uid, store), actor);
            }
        }
    }

   private bool MatchesProto(EntityWhitelist whitelist, EntProtoId protoId)
   {
       if (!_prototypes.TryIndex(protoId, out var proto))
           return false;

       var checks = new List<bool>();

       if (whitelist.Tags is { Count: > 0 } tags)
       {
           var protoTags = proto.TryGetComponent(out TagComponent? tagComp, _compFactory)
               ? tagComp.Tags
               : new HashSet<ProtoId<TagPrototype>>();

           checks.Add(whitelist.RequireAll ? tags.All(protoTags.Contains) : tags.Any(protoTags.Contains));
       }

       if (whitelist.Components is { Length: > 0 } comps)
       {
           checks.Add(whitelist.RequireAll
               ? comps.All(proto.Components.ContainsKey)
               : comps.Any(proto.Components.ContainsKey));
       }

       return whitelist.RequireAll
           ? checks.Count > 0 && checks.All(c => c)
           : checks.Any(c => c);
   }

}