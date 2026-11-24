using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Marines;
using Content.Shared.GameTicking.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Mriya.Rules.Contact;

/// <summary>
/// Contact perks (Creedbreaker-style, bought with skill points) and free loadout picks
/// (weapon + attachments, uniform, armor, helmet).
/// The window opens when a marine spawns, and from the HUD button; applying only works on the ship.
/// The client keeps the preset (CVars); the server checks everything and keeps the milestone unlocks
/// (wave 20 / 40 survived) in a json file, so they can't be edited client side.
/// </summary>
public sealed class ContactLoadoutSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly AttachableHolderSystem _attachable = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly ContactPerkSystem _perks = default!;
    [Dependency] private readonly ContactPerkCombatSystem _perkCombat = default!;

    private static readonly ResPath SaveDir = new("/mriya");
    private static readonly ResPath SavePath = new("/mriya/contact_progress.json");

    private static readonly string[] StoragePriority = { "belt", "pocket1", "pocket2", "suitstorage", "back" };

    /// <summary>
    /// User id -> survived milestone waves. Permanent.
    /// </summary>
    private Dictionary<string, HashSet<int>> _unlocked = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeNetworkEvent<ContactLoadoutRequestEvent>(OnRequest);
        SubscribeNetworkEvent<ContactLoadoutApplyEvent>(OnApply);

        Load();
    }

    private ContactLoadoutComponent? GetConfig()
    {
        var query = EntityQueryEnumerator<ContactLoadoutComponent, ActiveGameRuleComponent>();
        while (query.MoveNext(out var config, out _))
        {
            return config;
        }

        return null;
    }

    #region Opening

    /// <summary>
    /// First attach to a marine during Contact (spawn, late join): offer the window once per body.
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (GetConfig() is not { } config ||
            !IsLivingMarine(ev.Entity) ||
            HasComp<ContactLoadoutStateComponent>(ev.Entity))
        {
            return;
        }

        EnsureComp<ContactLoadoutStateComponent>(ev.Entity);
        SendOpen(ev.Player, ev.Entity, config);
    }

    private void OnRequest(ContactLoadoutRequestEvent ev, EntitySessionEventArgs args)
    {
        if (GetConfig() is not { } config || args.SenderSession.AttachedEntity is not { } player || !IsLivingMarine(player))
            return;

        SendOpen(args.SenderSession, player, config);
    }

    private void SendOpen(ICommonSession session, EntityUid player, ContactLoadoutComponent config)
    {
        RaiseNetworkEvent(new ContactLoadoutOpenEvent(GetPoints(session.UserId, config), IsOnShip(player)), session);
    }

    #endregion

    #region Applying

    private void OnApply(ContactLoadoutApplyEvent ev, EntitySessionEventArgs args)
    {
        if (GetConfig() is not { } config || args.SenderSession.AttachedEntity is not { } player || !IsLivingMarine(player))
            return;

        if (!IsOnShip(player))
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-loadout-ship-only"), player, player, PopupType.SmallCaution);
            return;
        }

        var perks = new List<ProtoId<ContactPerkPrototype>>();
        foreach (var id in ev.Perks.Distinct())
        {
            if (_prototypes.HasIndex<ContactPerkPrototype>(id))
                perks.Add(id);
        }

        if (ContactLoadoutHelpers.TotalCost(_prototypes, ev.Perks) > GetPoints(args.SenderSession.UserId, config))
        {
            _popup.PopupEntity(Loc.GetString("mriya-contact-loadout-not-enough"), player, player, PopupType.SmallCaution);
            return;
        }

        foreach (var id in perks)
        {
            if (ContactLoadoutHelpers.Conflicts(_prototypes, _prototypes.Index(id), ev.Perks))
            {
                _popup.PopupEntity(Loc.GetString("mriya-contact-loadout-conflict"), player, player, PopupType.SmallCaution);
                return;
            }
        }

        _perks.SetPerks(player, perks);
        _perkCombat.Refresh(player);
        ApplyGear(player, ev);

        _popup.PopupEntity(Loc.GetString("mriya-contact-loadout-applied"), player, player);
    }

    private void ApplyGear(EntityUid player, ContactLoadoutApplyEvent ev)
    {
        var state = EnsureComp<ContactLoadoutStateComponent>(player);

        // Mriya. Boundary of Death: the chosen weapon is swapped for Kizuna
        ContactPerkReplaceWeapon? replace = null;
        if (TryComp(player, out ContactPerksComponent? perks))
            replace = _perks.GetEffects<ContactPerkReplaceWeapon>(perks).FirstOrDefault();

        foreach (var (category, id) in ev.Items)
        {
            if (!_prototypes.TryIndex<ContactLoadoutItemPrototype>(id, out var item) || item.Category != category)
                continue;

            if (ContactLoadoutHelpers.ClothingSlot(category) is { } slot)
                ReplaceSlot(player, slot, item.Proto);
            else if (category == ContactLoadoutCategory.Weapon && replace == null)
                GiveWeapon(player, state, item, ev.Attachments);
        }

        if (replace != null)
            GiveReplacement(player, state, replace);
    }

    private void GiveReplacement(EntityUid player, ContactLoadoutStateComponent state, ContactPerkReplaceWeapon replace)
    {
        foreach (var given in state.Given)
        {
            if (!TerminatingOrDeleted(given))
                QueueDel(given);
        }

        state.Given.Clear();

        var coords = Transform(player).Coordinates;
        var weapon = Spawn(replace.Proto, coords);
        state.Given.Add(weapon);

        if (!_inventory.TryEquip(player, weapon, "back", silent: true) &&
            !_inventory.TryEquip(player, weapon, "belt", silent: true) &&
            !_inventory.TryEquip(player, weapon, "suitstorage", silent: true))
        {
            _hands.TryPickupAnyHand(player, weapon);
        }

        foreach (var extra in replace.Extras)
        {
            var item = Spawn(extra, coords);
            state.Given.Add(item);

            if (!TryInsertIntoStorage(player, item))
                _hands.TryPickupAnyHand(player, item);
        }
    }

    /// <summary>
    /// Swaps what's in the slot for the new item. Taking a uniform off drops what depends on it
    /// (pockets, belt, ID...), so everything else worn is put back afterwards.
    /// </summary>
    private void ReplaceSlot(EntityUid player, string slot, EntProtoId proto)
    {
        var worn = new Dictionary<string, EntityUid>();
        var slots = _inventory.GetSlotEnumerator(player);
        while (slots.MoveNext(out var container))
        {
            if (container.ID != slot && container.ContainedEntity is { } item)
                worn[container.ID] = item;
        }

        if (_inventory.TryGetSlotEntity(player, slot, out var old))
        {
            _inventory.TryUnequip(player, slot, silent: true, force: true);
            QueueDel(old.Value);
        }

        var replacement = Spawn(proto, Transform(player).Coordinates);
        if (!_inventory.TryEquip(player, replacement, slot, silent: true, force: true))
            _hands.TryPickupAnyHand(player, replacement);

        foreach (var (otherSlot, item) in worn)
        {
            if (TerminatingOrDeleted(item) ||
                _inventory.TryGetSlotEntity(player, otherSlot, out var current) && current == item)
            {
                continue;
            }

            _inventory.TryEquip(player, item, otherSlot, silent: true, force: true);
        }
    }

    /// <summary>
    /// The previously given weapon and extras are deleted, then the new weapon is made with its attachments.
    /// Attachments: real attachment prototypes, only into unlocked slots whose whitelist they pass.
    /// </summary>
    private void GiveWeapon(EntityUid player, ContactLoadoutStateComponent state, ContactLoadoutItemPrototype weapon, Dictionary<string, string> attachments)
    {
        foreach (var given in state.Given)
        {
            if (!TerminatingOrDeleted(given))
                QueueDel(given);
        }

        state.Given.Clear();

        var coords = Transform(player).Coordinates;
        var gun = Spawn(weapon.Proto, coords);
        state.Given.Add(gun);

        if (TryComp(gun, out AttachableHolderComponent? holder))
        {
            // read-only access to Slots (RMC Access): walk it and look the choice up in our dictionary
            foreach (var (slotId, slot) in holder.Slots)
            {
                if (slot.Locked || !attachments.TryGetValue(slotId, out var attachmentId))
                    continue;

                if (!_prototypes.TryIndex<EntityPrototype>(attachmentId, out var attachmentProto) ||
                    !attachmentProto.TryGetComponent(out AttachableComponent? _, _compFactory))
                {
                    continue;
                }

                var attached = Spawn(attachmentId, coords);
                if (slot.Whitelist != null && !_whitelist.IsWhitelistPass(slot.Whitelist, attached) ||
                    !_attachable.Attach((gun, holder), attached, player, slotId))
                {
                    QueueDel(attached);
                }
            }
        }

        // armor's suit storage, then the back, then hands
        if (!_inventory.TryEquip(player, gun, "suitstorage", silent: true) &&
            !_inventory.TryEquip(player, gun, "back", silent: true))
        {
            _hands.TryPickupAnyHand(player, gun);
        }

        foreach (var extra in weapon.Extras)
        {
            var item = Spawn(extra, coords);
            state.Given.Add(item);

            if (!TryInsertIntoStorage(player, item))
                _hands.TryPickupAnyHand(player, item);
        }
    }

    private bool TryInsertIntoStorage(EntityUid player, EntityUid item)
    {
        if (!TryComp(player, out InventoryComponent? inventory))
            return false;

        var slots = StoragePriority.Concat(inventory.Slots.Select(s => s.Name).Except(StoragePriority));
        foreach (var slot in slots)
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var worn, inventory) ||
                !HasComp<StorageComponent>(worn))
            {
                continue;
            }

            if (_storage.Insert(worn.Value, item, out _, user: player, playSound: false))
                return true;
        }

        return false;
    }

    #endregion

    #region Points

    private int GetPoints(NetUserId user, ContactLoadoutComponent config)
    {
        var points = config.BasePoints;
        if (_unlocked.TryGetValue(user.ToString(), out var waves))
        {
            foreach (var wave in waves)
            {
                points += config.Milestones.GetValueOrDefault(wave);
            }
        }

        return points;
    }

    /// <summary>
    /// Called by ContactRuleSystem when the next wave is due: everyone alive just survived this one.
    /// A milestone wave unlocks its bonus for good, right away.
    /// </summary>
    public void OnWaveSurvived(int wave)
    {
        if (GetConfig() is not { } config || !config.Milestones.TryGetValue(wave, out var bonus))
            return;

        var changed = false;
        var query = EntityQueryEnumerator<MarineComponent, ActorComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var actor, out var mobState))
        {
            if (_mobState.IsDead(uid, mobState))
                continue;

            var session = actor.PlayerSession;
            if (!_unlocked.GetOrNew(session.UserId.ToString()).Add(wave))
                continue;

            changed = true;
            RaiseNetworkEvent(new ContactLoadoutPointsEvent(GetPoints(session.UserId, config)), session);
            _popup.PopupEntity(Loc.GetString("mriya-contact-loadout-milestone", ("wave", wave), ("points", bonus)),
                uid, uid, PopupType.Large);
        }

        if (changed)
            Save();
    }

    private void Load()
    {
        try
        {
            if (!_resources.UserData.Exists(SavePath))
                return;

            using var reader = _resources.UserData.OpenText(SavePath);
            _unlocked = JsonSerializer.Deserialize<Dictionary<string, HashSet<int>>>(reader.ReadToEnd()) ?? new();
        }
        catch (Exception e)
        {
            Log.Error($"Contact: couldn't read {SavePath}: {e}");
        }
    }

    private void Save()
    {
        try
        {
            _resources.UserData.CreateDir(SaveDir);
            using var writer = _resources.UserData.OpenWriteText(SavePath);
            writer.Write(JsonSerializer.Serialize(_unlocked));
        }
        catch (Exception e)
        {
            Log.Error($"Contact: couldn't write {SavePath}: {e}");
        }
    }

    #endregion

    #region Helpers

    private bool IsLivingMarine(EntityUid uid)
    {
        return HasComp<MarineComponent>(uid) && !_mobState.IsDead(uid);
    }

    /// <summary>
    /// On the ship's map (a dropship docked there counts).
    /// </summary>
    private bool IsOnShip(EntityUid uid)
    {
        var map = Transform(uid).MapID;
        var query = EntityQueryEnumerator<AlmayerComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID == map)
                return true;
        }

        return false;
    }

    #endregion
}
