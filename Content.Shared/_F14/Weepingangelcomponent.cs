using Robust.Shared.GameStates;

namespace Content.Shared._F14;

/// <summary>
/// Компонент, що позначає сутність як "Плачучого Янгола".
/// Дозволяє рухатися та атакувати лише тоді, коли ніхто з живих та гравців(! Янгол не атакує істот без душі) не дивиться на неї.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class WeepingAngelComponent : Component
{
    /// <summary>
    /// Чи знаходиться сутність під поглядом хоча б одного живого спостерігача.
    /// </summary>
    [ViewVariables]
    public bool IsWatched;

    /// <summary>
    /// Максимальна дистанція у тайлах, на якій гравець може помітити та заморозити янгола.
    /// </summary>
    [DataField]
    public float WatchRange = 15f;

    /// <summary>
    /// Поріг скалярного добутку напрямку погляду (Dot Product).
    /// Значення <c>0.6f</c> відповідає конусу огляду близько 50–60 градусів прямо на янгола.
    /// </summary>
    [DataField]
    public float WatchDotThreshold = 0.6f;

    /// <summary>
    /// Базова швидкість ходьби янгола, коли на нього не дивляться.
    /// </summary>
    [DataField]
    public float WalkSpeed = 8f;

    /// <summary>
    /// Швидкість бігу янгола під час руху, коли на нього не дивляться.
    /// </summary>
    [DataField]
    public float SprintSpeed = 12f;

    /// <summary>
    /// Радіус дії аури кліпання навколо янгола.
    /// </summary>
    [DataField]
    public float AuraRange = 17f;

    /// <summary>
    /// Чи може янгол вільно рухатися під поглядом, якщо знаходиться у повній темряві.
    /// </summary>
    [DataField]
    public bool CanMoveInDarkness = true;

    /// <summary>
    /// Радіус перевірки наявності джерел світла коло янгола.
    /// </summary>
    [DataField]
    public float DarknessCheckRadius = 10f;

    /// <summary>
    /// Кулдаун у секундах між повторними атаками в ближньому бою.
    /// </summary>
    [DataField]
    public float AttackCooldown = 1.0f;

    /// <summary>
    ///  таймер кулдауну атаки.
    /// </summary>
    public float AttackTimer = 0f;
}