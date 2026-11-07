using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Sprite;

/// <summary>
/// Використовується для відображення старих/наших спрайтів, замість РМСних
/// </summary>
///
[RegisterComponent, NetworkedComponent]
public sealed partial class MriyaOldSpriteComponent : Component
{
    [DataField]
    public string NormalSprite;

    [DataField]
    public string OldSprite;
}
