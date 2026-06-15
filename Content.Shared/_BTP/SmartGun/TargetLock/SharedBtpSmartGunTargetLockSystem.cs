using Content.Shared._RMC14.Weapons.Ranged.Homing;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Examine;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared._BTP.SmartGun.TargetLock;

public abstract class SharedBtpSmartGunTargetLockSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BtpSmartGunTargetLockComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<BtpSmartGunTargetLockComponent, BtpSmartGunTargetLockActionEvent>(OnToggleAction);
        SubscribeLocalEvent<BtpSmartGunTargetLockComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<BtpSmartGunTargetLockComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BtpSmartGunTargetLockProjectileComponent, PreventCollideEvent>(OnProjectilePreventCollide);
    }

    public override void Update(float frameTime)
    {
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<BtpSmartGunTargetLockComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Target is not { } target)
                continue;

            if (TerminatingOrDeleted(target) ||
                comp.User is not { } user ||
                TerminatingOrDeleted(user) ||
                !_transform.InRange(Transform(uid).Coordinates, Transform(target).Coordinates, comp.Range + 1f))
            {
                ClearTarget((uid, comp));
            }
        }
    }

    protected void TargetLockRequested(NetEntity netGun, NetEntity netUser, NetEntity? netTarget)
    {
        if (!_net.IsServer)
            return;

        var gun = GetEntity(netGun);
        var user = GetEntity(netUser);

        if (!TryComp(gun, out BtpSmartGunTargetLockComponent? comp))
            return;

        if (!comp.Activated)
            return;

        if (netTarget == null)
        {
            ClearTarget((gun, comp), user);
            return;
        }

        var target = GetEntity(netTarget.Value);
        if (!CanLock((gun, comp), user, target))
            return;

        SetTarget((gun, comp), user, target);
    }

    private void OnGetActions(Entity<BtpSmartGunTargetLockComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnToggleAction(Entity<BtpSmartGunTargetLockComponent> ent, ref BtpSmartGunTargetLockActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.Activated = !ent.Comp.Activated;
        if (!ent.Comp.Activated)
            ClearTarget(ent, args.Performer);

        Dirty(ent);
        _actions.SetToggled(ent.Comp.Action, ent.Comp.Activated);
        args.Handled = true;
    }

    private void OnAmmoShot(Entity<BtpSmartGunTargetLockComponent> ent, ref AmmoShotEvent args)
    {
        if (!ent.Comp.Activated ||
            ent.Comp.Target is not { } target ||
            TerminatingOrDeleted(target))
        {
            return;
        }

        foreach (var projectile in args.FiredProjectiles)
        {
            var homing = EnsureComp<HomingProjectileComponent>(projectile);
            homing.Target = target;
            homing.ProjectileSpeed = ent.Comp.ProjectileSpeed;
            Dirty(projectile, homing);

            var lockProjectile = EnsureComp<BtpSmartGunTargetLockProjectileComponent>(projectile);
            lockProjectile.Target = target;
            Dirty(projectile, lockProjectile);
        }
    }

    private void OnShutdown(Entity<BtpSmartGunTargetLockComponent> ent, ref ComponentShutdown args)
    {
        ClearTarget(ent);
    }

    private void OnProjectilePreventCollide(Entity<BtpSmartGunTargetLockProjectileComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.OtherEntity == ent.Comp.Target)
            return;

        if (HasComp<MobStateComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private bool CanLock(Entity<BtpSmartGunTargetLockComponent> ent, EntityUid user, EntityUid target)
    {
        if (target == user || target == ent.Owner)
            return false;

        if (!HasComp<MobStateComponent>(target))
        {
            _popup.PopupClient("Target cannot be locked.", user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_combat.IsInCombatMode(user))
        {
            _popup.PopupClient("Combat mode is required.", user, user, PopupType.SmallCaution);
            return false;
        }

        if (TryComp(ent, out WieldableComponent? wieldable) && !wieldable.Wielded)
        {
            _popup.PopupClient("The smartgun must be wielded.", user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_transform.InRange(Transform(ent).Coordinates, Transform(target).Coordinates, ent.Comp.Range))
        {
            _popup.PopupClient("Target is outside lock range.", user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_examine.InRangeUnOccluded(user, target, ent.Comp.Range))
        {
            _popup.PopupClient("Lock line is obstructed.", user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private void SetTarget(Entity<BtpSmartGunTargetLockComponent> ent, EntityUid user, EntityUid target)
    {
        if (ent.Comp.Target == target)
            return;

        ClearTarget(ent, user, false);

        ent.Comp.Target = target;
        ent.Comp.User = user;
        Dirty(ent);

        var locked = EnsureComp<BtpSmartGunTargetLockedComponent>(target);
        if (!locked.LockedBy.Contains(ent))
            locked.LockedBy.Add(ent);
        Dirty(target, locked);

        _actions.SetToggled(ent.Comp.Action, true);
        _audio.PlayGlobal(ent.Comp.LockSound, user);
    }

    protected void ClearTarget(Entity<BtpSmartGunTargetLockComponent> ent, EntityUid? user = null, bool playSound = true)
    {
        if (ent.Comp.Target is { } target && TryComp(target, out BtpSmartGunTargetLockedComponent? locked))
        {
            locked.LockedBy.Remove(ent);
            if (locked.LockedBy.Count == 0)
                RemCompDeferred<BtpSmartGunTargetLockedComponent>(target);
            else
                Dirty(target, locked);
        }

        var hadTarget = ent.Comp.Target != null;
        ent.Comp.Target = null;
        ent.Comp.User = null;
        Dirty(ent);

        if (hadTarget && playSound && user != null)
            _audio.PlayPredicted(ent.Comp.UnlockSound, ent, user);
    }
}
