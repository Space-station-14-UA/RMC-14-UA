using Content.Server.Mriya.Sponsors.UI;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Player;
using Robust.Shared.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Content.Server.Mriya.Sponsors;

public sealed class SponsorGhostSystem : EntitySystem
{
    [Dependency] private readonly ISponsorManager _sponsorManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<GhostComponent, SpawnGhostForPlayerEvent>(OnSpawnGhostForPlayerEventHandler);
        SubscribeLocalEvent<SaveGhostColorEvent>(OnGhostColorSaved);
    }

    private void OnGhostColorSaved(SaveGhostColorEvent ev)
    {
        var session = ev.Session;
        if (session.AttachedEntity is not { } entity)
            return;
        if (TryComp<GhostComponent>(entity, out var comp))
            SetGhostOOCColor(entity, comp);
    }

    private void OnSpawnGhostForPlayerEventHandler(Entity<GhostComponent> ent, ref SpawnGhostForPlayerEvent args)
    {
        SetGhostOOCColor(ent.Owner, ent.Comp);
    }

    private void OnMindAdded(Entity<GhostComponent> ent, ref MindAddedMessage args)
    {
        SetGhostOOCColor(ent.Owner, ent.Comp);
    }

    private void SetGhostOOCColor(EntityUid uid, GhostComponent component)
    {
        var color = GetOOCColorForGhost(uid);
        if (string.IsNullOrEmpty(color))
            color = "#FFFFFFFF";

        var c = Color.FromHex(color);
        var msg = new SetGhostColorMsg()
        {
            Color = c
        };
        RaiseLocalEvent(uid, msg);
    }

    private string? GetOOCColorForGhost(EntityUid uid)
    {
        if (!TryGetPlayerSessionFromEntity(uid, out var session))
        {
            return null;
        }

        return _sponsorManager.GetGhostColor(session.UserId);
    }

    private bool TryGetPlayerSessionFromEntity(EntityUid uid, [NotNullWhen(true)] out ICommonSession? session)
    {
        session = null;
        if (!TryComp<ActorComponent>(uid, out var actor))
            return false;

        session = actor.PlayerSession;
        return true;
    }
}

public struct SpawnGhostForPlayerEvent
{
    public readonly EntityUid? Entity;
    public SpawnGhostForPlayerEvent(EntityUid? entity = null)
    {
        Entity = entity;
    }
}
