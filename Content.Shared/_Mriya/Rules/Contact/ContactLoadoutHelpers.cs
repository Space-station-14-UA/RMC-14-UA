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
    public static int ClampLevel(ContactSkillPrototype skill, int level)
    {
        return Math.Clamp(level, skill.BaseLevel, Math.Max(skill.BaseLevel, skill.MaxLevel));
    }

    /// <summary>
    /// Cost of going from level to level + 1.
    /// </summary>
    public static int NextCost(ContactSkillPrototype skill, int level)
    {
        if (skill.Costs.Count == 0)
            return 0;

        return skill.Costs[Math.Clamp(level, 0, skill.Costs.Count - 1)];
    }

    /// <summary>
    /// Points spent on this skill at this level (BaseLevel is free).
    /// </summary>
    public static int SkillCost(ContactSkillPrototype skill, int level)
    {
        var cost = 0;
        for (var l = skill.BaseLevel; l < ClampLevel(skill, level); l++)
        {
            cost += NextCost(skill, l);
        }

        return cost;
    }

    public static int TotalCost(IPrototypeManager prototypes, Dictionary<string, int> levels)
    {
        var total = 0;
        foreach (var skill in prototypes.EnumeratePrototypes<ContactSkillPrototype>())
        {
            total += SkillCost(skill, levels.GetValueOrDefault(skill.ID, skill.BaseLevel));
        }

        return total;
    }

    /// <summary>
    /// Inventory slot a clothing category goes into; null for weapons and attachments.
    /// </summary>
    public static string? ClothingSlot(ContactLoadoutCategory category)
    {
        return category switch
        {
            ContactLoadoutCategory.Uniform => "jumpsuit",
            ContactLoadoutCategory.Armor => "outerClothing",
            ContactLoadoutCategory.Helmet => "head",
            _ => null,
        };
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
}
