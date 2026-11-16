using Content.Server.Hands.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._Mriya.NPC.AI;

public static class AIEquipment
{
    public static readonly string[] GunStowSlots = { "suitstorage", "back", "belt", "pocket1", "pocket2" };

    public static readonly string[] MeleeStowSlots = { "pocket2", "pocket1", "belt", "suitstorage", "back" };

    public static readonly string[] GrenadeStowSlots = { "pocket1", "pocket2", "belt", "suitstorage", "back" };

    private static readonly ProtoId<TagPrototype> GrenadeTag = "Grenade";

    public static IEnumerable<EntityUid> EnumerateOwnedItems(EntityUid owner, IEntityManager entMan)
    {
        var hands = entMan.System<HandsSystem>();

        foreach (var held in hands.EnumerateHeld(owner))
        {
            foreach (var nested in EnumerateNested(held, entMan))
                yield return nested;
        }

        if (!entMan.TryGetComponent<InventoryComponent>(owner, out var inventory))
            yield break;

        foreach (var container in inventory.Containers)
        {
            if (container.ContainedEntity is not { } item)
                continue;

            foreach (var nested in EnumerateNested(item, entMan))
                yield return nested;
        }
    }

    private static IEnumerable<EntityUid> EnumerateNested(EntityUid item, IEntityManager entMan)
    {
        if (entMan.HasComponent<VirtualItemComponent>(item))
            yield break;

        yield return item;

        if (!entMan.TryGetComponent<ContainerManagerComponent>(item, out var manager))
            yield break;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                foreach (var nested in EnumerateNested(child, entMan))
                    yield return nested;
            }
        }
    }

    public static bool TryFindGun(EntityUid owner, IEntityManager entMan, out EntityUid gun, bool includeNearby = false)
    {
        foreach (var item in EnumerateOwnedItems(owner, entMan))
        {
            if (!entMan.HasComponent<GunComponent>(item))
                continue;

            gun = item;
            return true;
        }

        if (includeNearby)
        {
            var lookup = entMan.System<EntityLookupSystem>();

            foreach (var item in lookup.GetEntitiesInRange(owner, 2f, LookupFlags.Uncontained))
            {
                if (!entMan.HasComponent<GunComponent>(item))
                    continue;

                gun = item;
                return true;
            }
        }

        gun = EntityUid.Invalid;
        return false;
    }

    public static bool TryFindMelee(EntityUid owner, IEntityManager entMan, out EntityUid melee, bool includeNearby = false)
    {
        foreach (var item in EnumerateOwnedItems(owner, entMan))
        {
            if (entMan.HasComponent<GunComponent>(item) ||
                !entMan.HasComponent<MeleeWeaponComponent>(item))
            {
                continue;
            }

            melee = item;
            return true;
        }

        if (includeNearby)
        {
            var lookup = entMan.System<EntityLookupSystem>();

            foreach (var item in lookup.GetEntitiesInRange(owner, 2f, LookupFlags.Uncontained))
            {
                if (entMan.HasComponent<GunComponent>(item) ||
                    !entMan.HasComponent<MeleeWeaponComponent>(item))
                {
                    continue;
                }

                melee = item;
                return true;
            }
        }

        melee = EntityUid.Invalid;
        return false;
    }

    public static bool TryFindGrenade(EntityUid owner, IEntityManager entMan, out EntityUid grenade)
    {
        var tags = entMan.System<TagSystem>();

        foreach (var item in EnumerateOwnedItems(owner, entMan))
        {
            if (!entMan.HasComponent<OnUseTimerTriggerComponent>(item) ||
                !tags.HasTag(item, GrenadeTag))
            {
                continue;
            }

            grenade = item;
            return true;
        }

        grenade = EntityUid.Invalid;
        return false;
    }

    public static bool TryHoldItem(EntityUid owner, EntityUid item, IEntityManager entMan, bool makeActive = true)
    {
        var hands = entMan.System<HandsSystem>();

        if (!entMan.TryGetComponent<HandsComponent>(owner, out var handComp))
            return false;

        if (hands.IsHolding((owner, handComp), item, out var heldHand))
        {
            if (makeActive)
                hands.TrySetActiveHand((owner, handComp), heldHand);

            return true;
        }

        if (hands.TryGetEmptyHand((owner, handComp), out var emptyHand) &&
            hands.TryPickup(owner, item, emptyHand, checkActionBlocker: false, animate: false, handsComp: handComp))
        {
            if (makeActive)
                hands.TrySetActiveHand((owner, handComp), emptyHand);

            return true;
        }

        var activeHand = hands.GetActiveHand((owner, handComp));

        if (activeHand != null &&
            hands.TryForcePickup((owner, handComp), item, activeHand, checkActionBlocker: false, animate: false, handsComp: handComp))
        {
            if (makeActive)
                hands.TrySetActiveHand((owner, handComp), activeHand);

            return true;
        }

        return false;
    }

    public static bool TryStowItem(EntityUid owner, EntityUid item, IEntityManager entMan, string[] preferredSlots)
    {
        var hands = entMan.System<HandsSystem>();
        var wieldable = entMan.System<SharedWieldableSystem>();
        var inventory = entMan.System<InventorySystem>();

        if (!entMan.TryGetComponent<InventoryComponent>(owner, out var inventoryComp))
            return false;

        if (entMan.TryGetComponent<WieldableComponent>(item, out var wieldComp) && wieldComp.Wielded)
            wieldable.TryUnwield(item, wieldComp, owner, force: true);

        foreach (var slot in preferredSlots)
        {
            if (!inventory.TryGetSlot(owner, slot, out _, inventoryComp))
                continue;

            if (inventory.TryGetSlotEntity(owner, slot, out _, inventoryComp))
                continue;

            if (inventory.TryEquip(owner, item, slot,
                    silent: true,
                    force: true,
                    checkDoafter: false,
                    doRangeCheck: false))
            {
                return true;
            }
        }

        return hands.TryDrop(owner, item, checkActionBlocker: false, doDropInteraction: false);
    }
}
