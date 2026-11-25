using Content.Shared._Mriya.Rules.Contact;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Mobs.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._Mriya.Rules.Contact;

public sealed class ContactPerkHighlightSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        _overlays.AddOverlay(new ContactPerkHighlightOverlay());
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<ContactPerkHighlightOverlay>();
    }
}

/// <summary>
/// Headhunter: boxes around living xenos in the perk's range band, drawn above FOV (through walls).
/// </summary>
public sealed class ContactPerkHighlightOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly ContactPerkSystem _perks;
    private readonly SharedTransformSystem _transform;
    private readonly EntityLookupSystem _lookup;
    private readonly MobStateSystem _mobState;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ContactPerkHighlightOverlay()
    {
        IoCManager.InjectDependencies(this);
        _perks = _entities.System<ContactPerkSystem>();
        _transform = _entities.System<SharedTransformSystem>();
        _lookup = _entities.System<EntityLookupSystem>();
        _mobState = _entities.System<MobStateSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } player ||
            !_entities.TryGetComponent(player, out ContactPerksComponent? perks) ||
            !_entities.TryGetComponent(player, out TransformComponent? xform) ||
            xform.MapID != args.MapId)
        {
            return;
        }

        var origin = _transform.GetWorldPosition(xform);
        var handle = args.WorldHandle;
        foreach (var highlight in _perks.GetEffects<ContactPerkHighlight>(perks))
        {
            foreach (var xeno in _lookup.GetEntitiesInRange<XenoComponent>(xform.Coordinates, highlight.MaxRange))
            {
                if (_mobState.IsDead(xeno))
                    continue;

                var pos = _transform.GetWorldPosition(xeno);
                if ((pos - origin).Length() < highlight.MinRange)
                    continue;

                var box = _lookup.GetWorldAABB(xeno).Enlarged(0.15f);
                handle.DrawRect(box, highlight.Color.WithAlpha(0.15f));
                handle.DrawRect(box, highlight.Color, filled: false);
            }
        }
    }
}