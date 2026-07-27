using System;
using System.Numerics;
using System.Collections.Generic;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Ghost;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee;
using Content.Server.NPC.Systems;
using Content.Server.NPC.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Audio.Systems;
using Robust.Server.GameObjects;
using Content.Shared._F14;

namespace Content.Server._F14;

/// <summary>
/// Серверна система, що відповідає за штучний інтелект Плачучого Янгола:
/// обчислення поглядів гравців, перевірку освітлення, заморозку фізики, наведення ШІ та завдавання шкоди.
/// </summary>
public sealed class WeepingAngelSystem : EntitySystem
{
    /// <summary>
    /// Інтервал перевірки наявності світла(може бути змінений для оптимізації/покращення геймплею).
    /// </summary>
    private const float DarknessCheckInterval = 0.25f;

    /// <summary>
    /// Максимальна відстань атаки.
    /// </summary>
    private const float MeleeAttackRangeSquared = 2.25f;

    /// <summary>
    /// базовий домаг, на випадок якщо буде нечайно прибраний з YML.
    /// </summary>
    private const int DefaultBluntDamage = 175;

    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSys = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BlinkingSystem _blinking = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly HashSet<EntityUid> _frozenAngels = new();
    private float _darknessTimer = 0f;
    private readonly Dictionary<EntityUid, bool> _darknessCache = new();

    /// <summary>
    /// Ініціалізує системи та підписується на події спроби атаки.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeepingAngelComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    /// <summary>
    /// Скасовує спробу атаки, якщо на янгола дивляться.
    /// </summary>
    private void OnAttackAttempt(EntityUid uid, WeepingAngelComponent component, AttackAttemptEvent args)
    {
        if (component.IsWatched)
            args.Cancel();
    }

    /// <summary>
    /// Оновлює стани усіх янголів у грі - аури, погляди і тд.
    /// <param name="frameTime">Час, що минув з останнього кадру.</param>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _darknessTimer += frameTime;
        bool checkDarkness = _darknessTimer >= DarknessCheckInterval;
        if (checkDarkness)
            _darknessTimer = 0f;

        var resetQuery = EntityQueryEnumerator<BlinkingComponent>();
        while (resetQuery.MoveNext(out var resetUid, out var resetBlink))
        {
            _blinking.SetInAura(resetUid, resetBlink, false);
        }

        var angelQuery = EntityQueryEnumerator<WeepingAngelComponent, TransformComponent, PhysicsComponent, MovementSpeedModifierComponent>();
        while (angelQuery.MoveNext(out var angelUid, out var angelComp, out var angelXform, out var phys, out var move))
        {
            var angelPos = _transform.GetWorldPosition(angelXform);

            if (angelComp.AttackTimer > 0f)
                angelComp.AttackTimer -= frameTime;

            ApplyAuraOptimized(angelUid, angelComp, angelXform, angelPos);

            var isWatched = IsBeingWatched(angelUid, angelComp, angelXform, angelPos);
            angelComp.IsWatched = isWatched;

            if (checkDarkness)
                _darknessCache[angelUid] = IsInDarknessOptimized(angelXform, angelComp.DarknessCheckRadius, angelPos);

            var inDarkness = _darknessCache.GetValueOrDefault(angelUid, false);
            var canMoveFreely = angelComp.CanMoveInDarkness && inDarkness;
            var shouldFreeze = isWatched && !canMoveFreely;

            if (shouldFreeze)
            {
                if (!_frozenAngels.Contains(angelUid))
                {
                    _frozenAngels.Add(angelUid);
                    _physics.SetLinearVelocity(angelUid, Vector2.Zero, body: phys);
                    _movementSys.ChangeBaseSpeed(angelUid, 0f, 0f, 0f, move);
                    RemComp<NPCSteeringComponent>(angelUid);
                }
            }
            else
            {
                if (_frozenAngels.Contains(angelUid))
                {
                    _frozenAngels.Remove(angelUid);
                }

                _movementSys.ChangeBaseSpeed(angelUid, angelComp.WalkSpeed, angelComp.SprintSpeed, 20f, move);
                EnsureComp<NPCSteeringComponent>(angelUid);

                var target = GetNearestTarget(angelUid, angelXform, angelPos, angelComp.WatchRange);
                if (target != null)
                {
                    var (targetUid, targetXform, targetPos) = target.Value;

                    _steering.Register(angelUid, targetXform.Coordinates);

                    var distSqr = (targetPos - angelPos).LengthSquared();
                    if (distSqr <= MeleeAttackRangeSquared)
                    {
                        if (angelComp.AttackTimer <= 0f)
                        {
                            angelComp.AttackTimer = angelComp.AttackCooldown;

                            if (TryComp<MeleeWeaponComponent>(angelUid, out var weapon))
                            {
                                _damageable.TryChangeDamage(targetUid, weapon.Damage, true, origin: angelUid);
                                if (weapon.HitSound != null)
                                    _audio.PlayPvs(weapon.HitSound, angelUid);
                            }
                            else
                            {
                                var dmg = new DamageSpecifier();
                                dmg.DamageDict.Add("Blunt", DefaultBluntDamage);
                                _damageable.TryChangeDamage(targetUid, dmg, true, origin: angelUid);
                            }
                        }
                    }
                }
                else
                {
                    RemComp<NPCSteeringComponent>(angelUid);
                }
            }
        }
    }

    /// <summary>
    /// Мітить усіх живих гравців і активовує кліпання
    /// </summary>
    private void ApplyAuraOptimized(EntityUid angelUid, WeepingAngelComponent angelComp, TransformComponent angelXform, Vector2 angelPos)
    {
        var sqrRange = angelComp.AuraRange * angelComp.AuraRange;
        var blinkQuery = EntityQueryEnumerator<BlinkingComponent, TransformComponent>();

        while (blinkQuery.MoveNext(out var uid, out var blink, out var xform))
        {
            if (HasComp<WeepingAngelComponent>(uid) || HasComp<GhostComponent>(uid))
                continue;

            if (xform.MapID != angelXform.MapID) continue;

            var distSqr = (_transform.GetWorldPosition(xform) - angelPos).LengthSquared();
            if (distSqr <= sqrRange)
            {
                _blinking.SetInAura(uid, blink, true);
            }
        }
    }

    /// <summary>
    /// Перевіряє, чи знаходиться янгол у темряві (відсутність джерел світла).
    /// </summary>
    private bool IsInDarknessOptimized(TransformComponent xform, float searchRadius, Vector2 pos)
    {
        var lightsQuery = EntityQueryEnumerator<PointLightComponent, TransformComponent>();

        while (lightsQuery.MoveNext(out var lightUid, out var light, out var lightXform))
        {
            if (!light.Enabled || lightXform.MapID != xform.MapID) continue;

            var lightPos = _transform.GetWorldPosition(lightXform);
            var distSqr = (lightPos - pos).LengthSquared();

            if (distSqr <= light.Radius * light.Radius)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Знаходить найближчого живого гравця.
    /// </summary>
    private (EntityUid Uid, TransformComponent Xform, Vector2 Pos)? GetNearestTarget(EntityUid angelUid, TransformComponent angelXform, Vector2 angelPos, float maxRange)
    {
        EntityUid? nearest = null;
        TransformComponent? nearestXform = null;
        Vector2 nearestPos = Vector2.Zero;
        float minSqrDist = maxRange * maxRange;

        var playerQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (playerQuery.MoveNext(out var pUid, out _, out var pXform))
        {
            if (HasComp<GhostComponent>(pUid) || _mobState.IsDead(pUid) || HasComp<WeepingAngelComponent>(pUid))
                continue;

            if (angelXform.MapID != pXform.MapID)
                continue;

            var pPos = _transform.GetWorldPosition(pXform);
            var distSqr = (pPos - angelPos).LengthSquared();

            if (distSqr < minSqrDist)
            {
                minSqrDist = distSqr;
                nearest = pUid;
                nearestXform = pXform;
                nearestPos = pPos;
            }
        }

        if (nearest != null && nearestXform != null)
            return (nearest.Value, nearestXform, nearestPos);

        return null;
    }

    /// <summary>
    /// Перевіряє, чи дивиться хоч один гравець на янгола.
    /// </summary>
    private bool IsBeingWatched(EntityUid angelUid, WeepingAngelComponent angelComp, TransformComponent angelXform, Vector2 angelPos)
    {
        var playerQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        var sqrWatchRange = angelComp.WatchRange * angelComp.WatchRange;

        while (playerQuery.MoveNext(out var pUid, out _, out var pXform))
        {
            if (HasComp<GhostComponent>(pUid) || _mobState.IsDead(pUid) || HasComp<WeepingAngelComponent>(pUid))
                continue;

            if (TryComp<BlinkingComponent>(pUid, out var blink) && blink.IsBlinking)
                continue;

            if (angelXform.MapID != pXform.MapID)
                continue;

            var pPos = _transform.GetWorldPosition(pXform);
            var vecToAngel = angelPos - pPos;
            var distSqr = vecToAngel.LengthSquared();

            if (distSqr > sqrWatchRange || distSqr < 0.0001f)
                continue;

            var distance = MathF.Sqrt(distSqr);
            var dirToAngel = vecToAngel / distance;
            var pFacing = _transform.GetWorldRotation(pXform).ToWorldVec();

            if (Vector2.Dot(pFacing, dirToAngel) > angelComp.WatchDotThreshold)
                return true;
        }

        return false;
    }
}