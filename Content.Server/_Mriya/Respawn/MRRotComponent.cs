using Content.Shared.Whitelist;

namespace Content.Server._Mriya.Respawn;

/// <summary>
/// Game rule: sets RMC's unrevivable delay (RMCRevivableComponent) for matching mobs.
/// RMC then handles the timer, HUD icon stages and the "unrevivable" examine/defib text.
/// </summary>
[RegisterComponent, Access(typeof(MRRotRuleSystem))]
public sealed partial class MRRotRuleComponent : Component
{
    [DataField]
    public TimeSpan RotTime = TimeSpan.FromMinutes(5);

    [DataField]
    public EntityWhitelist Whitelist = new()
    {
        Components = new[] { "Marine" },
    };

    [DataField]
    public TimeSpan CheckEvery = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextCheck;
}