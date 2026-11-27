using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Projectiles;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(RMCProjectileSystem))]
public sealed partial class ModifyTargetOnHitComponent : Component
{
    [DataField]
    public ComponentRegistry? Add;

    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist;

    // Mriya start. Збереження наявних компонентів цілі замість їх заміни (кислота СМУ).
    [DataField, AutoNetworkedField]
    public bool RemoveExisting = true;
    // Mriya end
}
