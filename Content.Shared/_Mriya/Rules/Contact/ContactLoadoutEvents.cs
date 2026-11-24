using Robust.Shared.Serialization;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Server -> client: open the perks/loadout window (on spawn, or as the answer to a request).
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactLoadoutOpenEvent : EntityEventArgs
{
    public readonly int Points;

    /// <summary>
    /// On the ship right now: Apply works.
    /// </summary>
    public readonly bool CanApply;

    public ContactLoadoutOpenEvent(int points, bool canApply)
    {
        Points = points;
        CanApply = canApply;
    }
}

/// <summary>
/// Server -> client: skill points changed (wave milestone reached).
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactLoadoutPointsEvent : EntityEventArgs
{
    public readonly int Points;

    public ContactLoadoutPointsEvent(int points)
    {
        Points = points;
    }
}

/// <summary>
/// Client -> server: the HUD button was pressed, send me an Open.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactLoadoutRequestEvent : EntityEventArgs;

/// <summary>
/// Client -> server: apply this preset. The server re-checks points, categories and attachment slots.
/// </summary>
[Serializable, NetSerializable]
public sealed class ContactLoadoutApplyEvent : EntityEventArgs
{
    /// <summary>
    /// ContactPerkPrototype ids.
    /// </summary>
    public List<string> Perks = new();

    /// <summary>
    /// Category -> ContactLoadoutItemPrototype id. Missing = keep what the job gave.
    /// </summary>
    public Dictionary<ContactLoadoutCategory, string> Items = new();

    /// <summary>
    /// Weapon attachment slot id -> attachment entity prototype id.
    /// </summary>
    public Dictionary<string, string> Attachments = new();
}
