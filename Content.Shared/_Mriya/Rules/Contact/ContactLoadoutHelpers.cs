using System.Linq;
using Content.Shared.Tag;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Rules both sides use: the client to show the window, the server to validate what it gets.
/// </summary>
public static class ContactLoadoutHelpers
{
    /// <summary>
    /// Sum of perk costs; unknown ids and duplicates count once / not at all.
    /// </summary>
    public static int TotalCost(IPrototypeManager prototypes, IEnumerable<string> perks)
    {
        var total = 0;
        foreach (var id in perks.Distinct())
        {
            if (prototypes.TryIndex<ContactPerkPrototype>(id, out var perk))
                total += perk.Cost;
        }

        return total;
    }

    /// <summary>
    /// Inventory slot a clothing category goes into; null for weapons.
    /// </summary>
    public static string? ClothingSlot(ContactLoadoutCategory category)
    {
        return category switch
        {
            ContactLoadoutCategory.Uniform => "jumpsuit",
            ContactLoadoutCategory.Armor => "outerClothing",
            ContactLoadoutCategory.Helmet => "head",
            ContactLoadoutCategory.Boots => "shoes",
            ContactLoadoutCategory.Belt => "belt",
            ContactLoadoutCategory.Pouch => "pocket1",
            ContactLoadoutCategory.Pouch2 => "pocket2",
            _ => null,
        };
    }

    /// <summary>
    /// Which item category a row picks from (both pocket rows use Pouch items).
    /// </summary>
    public static ContactLoadoutCategory ItemCategory(ContactLoadoutCategory row)
    {
        return row == ContactLoadoutCategory.Pouch2 ? ContactLoadoutCategory.Pouch : row;
    }

    /// <summary>
    /// Whitelist check against a prototype (no entity spawned): tags and components.
    /// </summary>
    public static bool MatchesProto(EntityWhitelist whitelist, EntityPrototype proto, IComponentFactory factory)
    {
        var checks = new List<bool>();

        if (whitelist.Tags is { Count: > 0 } tags)
        {
            var protoTags = proto.TryGetComponent(out TagComponent? tagComp, factory)
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

    /// <summary>
    /// True if the perk excludes an owned perk, or an owned perk excludes it.
    /// </summary>
    public static bool Conflicts(IPrototypeManager prototypes, ContactPerkPrototype perk, IEnumerable<string> owned)
    {
        foreach (var id in owned)
        {
            if (id == perk.ID)
                continue;

            if (perk.Excludes.Contains(id) ||
                prototypes.TryIndex<ContactPerkPrototype>(id, out var other) && other.Excludes.Contains(perk.ID))
            {
                return true;
            }
        }

        return false;
    }
}
