using Robust.Shared.GameStates;

namespace Content.Shared._F14;

[RegisterComponent, NetworkedComponent]
public sealed partial class WeepingAngelComponent : Component
{
    [ViewVariables]
    public bool IsWatched;

    [DataField]
    public float WatchRange = 15f;

    [DataField]
    public float WatchDotThreshold = 0.6f;

    [DataField]
    public float WalkSpeed = 8f;

    [DataField]
    public float SprintSpeed = 12f;

    [DataField]
    public float AuraRange = 17f;

    [DataField]
    public bool CanMoveInDarkness = true;

    [DataField]
    public float DarknessCheckRadius = 10f;

    [DataField]
    public float AttackCooldown = 1.0f;

    public float AttackTimer = 0f;
}