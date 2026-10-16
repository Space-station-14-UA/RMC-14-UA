using Content.Shared._Sich.Hunter.Pain;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Hypospray.Events;
using Content.Shared.Damage;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Sich.Hunter.Pain;

/// <summary>
/// Система ілюзії болю мисливця:
/// — При отриманні значного пошкодження за один удар відтворює випадковий звук болю.
/// — При введенні інжектора біорідини також відтворює звук болю.
/// </summary>
public sealed class HunterPainSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterPainSoundComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<HunterPainSoundComponent, TargetBeforeHyposprayInjectsEvent>(OnBeforeHyposprayInjects);
    }

    /// <summary>
    /// Відтворює звук болю, якщо мисливець отримав одразу багато пошкодження.
    /// </summary>
    private void OnDamageChanged(Entity<HunterPainSoundComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        var totalDamage = args.DamageDelta.GetTotal();
        if (totalDamage < (double) ent.Comp.DamageThreshold)
            return;

        TryPlayPainSound(ent);
    }

    /// <summary>
    /// Відтворює звук болю при введенні розчину (укол інжектора біорідини).
    /// </summary>
    private void OnBeforeHyposprayInjects(Entity<HunterPainSoundComponent> ent, ref TargetBeforeHyposprayInjectsEvent args)
    {
        if (args.TargetGettingInjected != ent.Owner)
            return;

        if (TryComp<MetaDataComponent>(args.Hypospray, out var meta) && meta.EntityPrototype?.ID != "CMCellularBioinjector")
            return;

        TryPlayPainSound(ent);
    }

    /// <summary>
    /// Відтворює звук болю з урахуванням кулдауну.
    /// </summary>
    private void TryPlayPainSound(Entity<HunterPainSoundComponent> ent)
    {
        if (_timing.CurTime < ent.Comp.LastPainTime + ent.Comp.Cooldown)
            return;

        ent.Comp.LastPainTime = _timing.CurTime;
        Dirty(ent);

        if (_net.IsServer)
            _audio.PlayPvs(ent.Comp.PainSound, ent.Owner);
    }
}
