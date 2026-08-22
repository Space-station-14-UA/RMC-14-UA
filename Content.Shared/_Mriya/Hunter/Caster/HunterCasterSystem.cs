using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._Sich.Hunter.Caster;
using Content.Shared.Actions;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
#if CLIENT
using Robust.Client.GameObjects;
#endif
using Robust.Shared.Network;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Sich.Hunter.Caster;

public sealed class HunterCasterSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<HunterCasterComponent, UniqueActionEvent>(OnUniqueAction);
        SubscribeLocalEvent<HunterCasterActionEvent>(OnCasterAction);
        SubscribeLocalEvent<HunterCasterProviderComponent, GotEquippedEvent>(OnArmorEquipped);
        SubscribeLocalEvent<HunterCasterProviderComponent, GotUnequippedEvent>(OnArmorUnequipped);
        SubscribeLocalEvent<HunterCasterProviderComponent, AfterAutoHandleStateEvent>(OnProviderState);
        SubscribeLocalEvent<HunterCasterComponent, EntityTerminatingEvent>(OnCasterDeleted);
    }

    private void OnCasterAction(HunterCasterActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;
        if (!TryComp<InventoryComponent>(performer, out var inventory) ||
            !_inventory.TryGetSlotEntity(performer, "outerClothing", out var armor, inventory) ||
            !TryComp<HunterCasterProviderComponent>(armor.Value, out var comp))
            return;

        // Handle retraction
        if (comp.Active || comp.SpawnedCaster != null)
        {
            if (_net.IsServer)
            {
                if (comp.SpawnedCaster != null)
                    QueueDel(comp.SpawnedCaster.Value);
                
                comp.Active = false;
                comp.SpawnedCaster = null;
                Dirty(armor.Value, comp);
                
                _audio.PlayPvs(comp.SoundOff, performer);
                _popup.PopupEntity(Loc.GetString("hunter-caster-retracted"), performer, performer);
            }
            
            return;
        }
        
        // Handle deployment
        if (_net.IsServer)
        {
            var caster = Spawn("HunterCaster", _transform.GetMapCoordinates(performer));
            if (_hands.TryPickupAnyHand(performer, caster))
            {
                comp.Active = true;
                comp.SpawnedCaster = caster;
                _audio.PlayPvs(comp.SoundOn, performer);
                _popup.PopupEntity(Loc.GetString("hunter-caster-deployed"), performer, performer);
            }
            else
            {
                QueueDel(caster);
                _popup.PopupEntity(Loc.GetString("hunter-caster-hands-busy"), performer, performer);
            }
            
            Dirty(armor.Value, comp);
        }

        args.Handled = true;
    }

    private void OnCasterDeleted(EntityUid uid, HunterCasterComponent comp, ref EntityTerminatingEvent args)
    {
        // Find the provider and reset state if our caster was deleted
        var query = EntityQueryEnumerator<HunterCasterProviderComponent>();
        while (query.MoveNext(out var armorUid, out var provider))
        {
            if (provider.SpawnedCaster != uid)
                continue;

            provider.SpawnedCaster = null;
            provider.Active = false;
            Dirty(armorUid, provider);
            break;
        }
    }

    private void OnProviderState(EntityUid uid, HunterCasterProviderComponent comp, ref AfterAutoHandleStateEvent args)
    {
        UpdateArmorVisuals(uid, comp.Active);
    }

    private void UpdateArmorVisuals(EntityUid armorUid, bool active)
    {
#if CLIENT
        if (!_transform.TryGetParent(armorUid, out var parent) || parent == null || !TryComp<SpriteComponent>(parent.Value, out var sprite))
            return;

        if (sprite.LayerMapTryGet(HunterCasterVisualLayers.Base, out var layer))
        {
            sprite.LayerSetVisible(layer, active);
        }
#endif
    }

    private void OnUniqueAction(EntityUid uid, HunterCasterComponent comp, UniqueActionEvent args)
    {
        if (args.Handled)
            return;

        var nextMode = (int) (comp.CurrentMode + 1) % Enum.GetValues<HunterCasterMode>().Length;
        comp.CurrentMode = (HunterCasterMode) nextMode;
        Dirty(uid, comp);
        
        var modeName = comp.CurrentMode switch
        {
            HunterCasterMode.Disabler => Loc.GetString("hunter-caster-mode-disabler"),
            HunterCasterMode.Bolt     => Loc.GetString("hunter-caster-mode-bolt"),
            _                         => Loc.GetString("hunter-caster-mode-unknown")
        };

        // Update ammo provider with mode-specific cost and projectile
        if (TryComp<HunterCasterAmmoProviderComponent>(uid, out var ammo))
        {
            var (cost, proto) = comp.CurrentMode switch
            {
                HunterCasterMode.Disabler => (30f,   "ProjectileHunterStun"),
                HunterCasterMode.Bolt     => (175f,  "PlasmaRifleBolt"),
                _                         => (30f,   "ProjectileHunterStun")
            };

            ammo.FireCost = cost;
            ammo.Prototype = proto;
            Dirty(uid, ammo);
        }

        if (_net.IsServer)
            _popup.PopupEntity(Loc.GetString("hunter-caster-mode-changed", ("mode", modeName)), args.UserUid, args.UserUid);

        args.Handled = true;
    }

    private void OnArmorEquipped(EntityUid uid, HunterCasterProviderComponent comp, GotEquippedEvent args)
    {
        _actions.AddAction(args.Equipee, ref comp.ActionEntity, comp.ActionId);
    }

    private void OnArmorUnequipped(EntityUid uid, HunterCasterProviderComponent comp, GotUnequippedEvent args)
    {
        _actions.RemoveAction(args.Equipee, comp.ActionEntity);
    }
}
