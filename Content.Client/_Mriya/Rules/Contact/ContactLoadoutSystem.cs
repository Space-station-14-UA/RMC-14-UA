using Content.Shared._Mriya.Rules.Contact;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Client side of the Contact skills/loadout window: opens it when the server says so,
/// keeps the preset in CVars between rounds, and sends Apply.
/// </summary>
public sealed class ContactLoadoutSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;

    private static readonly ContactLoadoutCategory[] Categories =
    {
        ContactLoadoutCategory.Weapon,
        ContactLoadoutCategory.Uniform,
        ContactLoadoutCategory.Armor,
        ContactLoadoutCategory.Helmet,
    };

    private ContactLoadoutWindow? _window;

    public override void Initialize()
    {
        SubscribeNetworkEvent<ContactLoadoutOpenEvent>(OnOpen);
        SubscribeNetworkEvent<ContactLoadoutPointsEvent>(OnPoints);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
        _window = null;
    }

    /// <summary>
    /// HUD button: ask the server, it answers with an Open (current points, on the ship or not).
    /// </summary>
    public void RequestOpen()
    {
        RaiseNetworkEvent(new ContactLoadoutRequestEvent());
    }

    private void OnOpen(ContactLoadoutOpenEvent ev)
    {
        _window ??= new ContactLoadoutWindow(this, _prototypes, _factory);
        _window.Load(LoadPreset(), ev.Points, ev.CanApply);
        _window.OpenCentered();
    }

    private void OnPoints(ContactLoadoutPointsEvent ev)
    {
        _window?.SetPoints(ev.Points);
    }

    public void Apply(ContactLoadoutApplyEvent preset)
    {
        SavePreset(preset);
        RaiseNetworkEvent(preset);
    }

    #region Preset

    private ContactLoadoutApplyEvent LoadPreset()
    {
        var preset = new ContactLoadoutApplyEvent();

        foreach (var (key, value) in Parse(_cfg.GetCVar(ContactCVars.LoadoutSkills)))
        {
            if (int.TryParse(value, out var level))
                preset.Skills[key] = level;
        }

        // explicit list: the client sandbox rejects generic Enum helpers
        foreach (var (key, value) in Parse(_cfg.GetCVar(ContactCVars.LoadoutItems)))
        {
            foreach (var category in Categories)
            {
                if (category.ToString() == key)
                    preset.Items[category] = value;
            }
        }

        foreach (var (key, value) in Parse(_cfg.GetCVar(ContactCVars.LoadoutAttachments)))
        {
            preset.Attachments[key] = value;
        }

        return preset;
    }

    private void SavePreset(ContactLoadoutApplyEvent preset)
    {
        _cfg.SetCVar(ContactCVars.LoadoutSkills, Join(preset.Skills, k => k, v => v.ToString()));
        _cfg.SetCVar(ContactCVars.LoadoutItems, Join(preset.Items, k => k.ToString(), v => v));
        _cfg.SetCVar(ContactCVars.LoadoutAttachments, Join(preset.Attachments, k => k, v => v));
        _cfg.SaveToFile();
    }

    /// <summary>
    /// "a=1;b=2" -> pairs. Ids never contain ; or =.
    /// </summary>
    private static IEnumerable<(string Key, string Value)> Parse(string text)
    {
        foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0 && eq < pair.Length - 1)
                yield return (pair[..eq], pair[(eq + 1)..]);
        }
    }

    private static string Join<TKey, TValue>(Dictionary<TKey, TValue> dict, Func<TKey, string> key, Func<TValue, string> value)
        where TKey : notnull
    {
        var parts = new List<string>();
        foreach (var (k, v) in dict)
        {
            parts.Add($"{key(k)}={value(v)}");
        }

        return string.Join(';', parts);
    }

    #endregion
}
