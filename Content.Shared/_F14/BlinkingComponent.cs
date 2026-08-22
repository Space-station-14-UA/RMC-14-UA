using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._F14;

/// <summary>
/// Компонент кліпання очей. Додається живим істотам для механіки кліпання та аури темряви.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlinkingComponent : Component
{
    /// <summary>
    /// Чи заплющені зараз очі гравця.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public bool IsBlinking = false;

    /// <summary>
    /// Тривалість одного кліпання у секундах.
    /// </summary>
    [DataField]
    public float BlinkDuration = 0.3f;

    /// <summary>
    /// Поточний відлік часу залишкового заплющення очей.
    /// </summary>
    [ViewVariables]
    public float BlinkTimer;

    /// <summary>
    /// Чи перебуває сутність в аурі примусового кліпання Плачучого Янгола.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool InAngelAura;

    /// <summary>
    /// Інтервал між примусовими кліпаннями під дією аури.
    /// </summary>
    [DataField]
    public float ForcedBlinkInterval = 3f;

    /// <summary>
    /// Таймер до наступного примусового заплющення очей.
    /// </summary>
    [ViewVariables]
    public float ForcedBlinkTimer;
}

/// <summary>
/// Мережева подія зміни стану кліпання гравця.
/// </summary>
[Serializable, NetSerializable]
public sealed class BlinkChangedEvent : EntityEventArgs
{
    /// <summary>
    /// Новий стан заплющення очей (true — заплющені, false — відкриті).
    /// </summary>
    public bool IsBlinking { get; }

    /// <summary>
    /// Робить новий екземпляр мержевої події зміни заплющення очей.
    /// </summary>
    /// <param name="isBlinking">Стан заплющення очей.</param>
    public BlinkChangedEvent(bool isBlinking) => IsBlinking = isBlinking;
}