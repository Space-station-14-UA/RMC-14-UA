using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Sich.Hunter.Zoom;

public sealed class HunterZoomSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContentEyeSystem _contentEye = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HunterZoomComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<HunterZoomComponent, HunterZoomActionEvent>(OnZoomAction);
        SubscribeLocalEvent<HunterZoomComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnGetItemActions(Entity<HunterZoomComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.Action, ent.Comp.ActionId);
        Dirty(ent);
    }

    private void OnZoomAction(Entity<HunterZoomComponent> ent, ref HunterZoomActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        ent.Comp.Enabled = !ent.Comp.Enabled;

        var user = args.Performer;

        if (ent.Comp.Enabled)
        {
            _contentEye.SetMaxZoom(user, ent.Comp.Zoom);
            _contentEye.SetZoom(user, ent.Comp.Zoom);

            if (_net.IsServer && ent.Comp.SoundOn != null)
                _audio.PlayPvs(ent.Comp.SoundOn, user);
        }
        else
        {
            _contentEye.ResetZoom(user);

            if (_net.IsServer && ent.Comp.SoundOff != null)
                _audio.PlayPvs(ent.Comp.SoundOff, user);
        }

        Dirty(ent);

        if (ent.Comp.Action != null)
            _actions.SetToggled(ent.Comp.Action.Value, ent.Comp.Enabled);
    }

    private void OnUnequipped(Entity<HunterZoomComponent> ent, ref GotUnequippedEvent args)
    {
        if (!ent.Comp.Enabled)
        {
            ent.Comp.Action = null;
            Dirty(ent);
            return;
        }

        // Reset zoom when mask is removed
        ent.Comp.Enabled = false;
        _contentEye.ResetZoom(args.Equipee);

        if (ent.Comp.Action != null)
            _actions.SetToggled(ent.Comp.Action.Value, false);

        ent.Comp.Action = null;
        Dirty(ent);
    }
}
