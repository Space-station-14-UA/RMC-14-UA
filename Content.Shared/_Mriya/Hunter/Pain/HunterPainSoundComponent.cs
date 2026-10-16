using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Sich.Hunter.Pain;

/// <summary>
/// Відтворює звук болю мисливця при отриманні великого пошкодження або уколі інжектора біорідини.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HunterPainSoundComponent : Component
{
    /// <summary>
    /// Колекція звуків болю. За замовчуванням — HunterPain.
    /// </summary>
    [DataField, AutoNetworkedField]
    public SoundSpecifier PainSound = new SoundCollectionSpecifier("HunterPain");

    /// <summary>
    /// Мінімальна кількість пошкодження за один удар, щоб спрацював звук болю.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DamageThreshold = 20f;

    /// <summary>
    /// Мінімальний час між відтвореннями звуку болю.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Час останнього відтворення звуку болю (для кулдауну).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan LastPainTime = TimeSpan.Zero;
}
