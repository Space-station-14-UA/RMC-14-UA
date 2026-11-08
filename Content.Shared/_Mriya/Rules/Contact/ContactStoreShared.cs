using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Є-бальний store terminal.
/// </summary>
[RegisterComponent]
public sealed partial class ContactStoreComponent : Component;

[Serializable, NetSerializable]
public enum ContactStoreUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ContactStoreBuyMessage(string entry, int amount) : BoundUserInterfaceMessage
{
    public readonly string Entry = entry;
    public readonly int Amount = amount;
}

[Serializable, NetSerializable]
public sealed class ContactStoreResupplyKitMessage(string kit) : BoundUserInterfaceMessage
{
    public readonly string Kit = kit;
}

[Serializable, NetSerializable]
public sealed class ContactStoreKitData(string kit, int price, Dictionary<string, int> missing)
{
    public readonly string Kit = kit;
    public readonly int Price = price;

    /// <summary>
    /// Entity proto id -> missing count. Only items that have a store entry.
    /// </summary>
    public readonly Dictionary<string, int> Missing = missing;
}

/// <summary>
/// Per-player data, sent to one actor (balance differs per player, so this isn't BUI state).
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactStoreUpdateMessage(int balance, List<string> compatibleEntries, List<ContactStoreKitData> kits)
    : BoundUserInterfaceMessage
{
    public readonly int Balance = balance;

    /// <summary>
    /// Store entry ids compatible with the player's guns: magazines, rounds, attachments.
    /// </summary>
    public readonly List<string> CompatibleEntries = compatibleEntries;

    public readonly List<ContactStoreKitData> Kits = kits;
}

public static class ContactStoreHelpers
{
    /// <summary>
    /// Everything inside the entity's containers, recursively (inventory, hands, backpack, belt, pouches, gun mags).
    /// </summary>
    public static IEnumerable<EntityUid> EnumerateCarried(IEntityManager ents, EntityUid uid)
    {
        if (!ents.TryGetComponent(uid, out ContainerManagerComponent? manager))
            yield break;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var child in container.ContainedEntities)
            {
                yield return child;

                foreach (var inner in EnumerateCarried(ents, child))
                {
                    yield return inner;
                }
            }
        }
    }

    public static bool IsMagazine(IEntityManager ents, EntityUid uid)
    {
        return ents.HasComponent<BallisticAmmoProviderComponent>(uid) && !ents.HasComponent<GunComponent>(uid);
    }

    public static int GetRounds(IEntityManager ents, EntityUid uid)
    {
        return ents.TryGetComponent(uid, out BallisticAmmoProviderComponent? ammo) ? ammo.Count : 0;
    }

    /// <summary>
    /// Counts carried items by prototype id. Empty magazines don't count.
    /// </summary>
    public static Dictionary<string, int> CountCarried(IEntityManager ents, EntityUid uid)
    {
        var counts = new Dictionary<string, int>();
        foreach (var item in EnumerateCarried(ents, uid))
        {
            if (ents.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID is not { } id)
                continue;

            if (IsMagazine(ents, item) && GetRounds(ents, item) == 0)
                continue;

            counts[id] = counts.GetValueOrDefault(id) + 1;
        }

        return counts;
    }
}