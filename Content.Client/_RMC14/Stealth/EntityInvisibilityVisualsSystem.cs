using System.Collections.Generic;
using Content.Shared._RMC14.Stealth;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RMC14.Stealth;

public sealed class EntityInvisibilityVisualsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EntityTurnInvisibleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EntityTurnInvisibleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<EntityTurnInvisibleComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        var shader = _prototypes.Index<ShaderPrototype>("RMCInvisible").InstanceUnique();
        _shaders[ent.Owner] = shader;
        sprite.PostShader = shader;
    }

    private void OnShutdown(Entity<EntityTurnInvisibleComponent> ent, ref ComponentShutdown args)
    {
        if (_shaders.Remove(ent.Owner, out var shader))
        {
            if (!TerminatingOrDeleted(ent) && TryComp(ent, out SpriteComponent? sprite))
            {
                if (sprite.PostShader == shader)
                    sprite.PostShader = null;
            }
            shader.Dispose();
        }
    }

    public override void Update(float frameTime)
    {
        var invisible = EntityQueryEnumerator<EntityTurnInvisibleComponent, SpriteComponent>();
        while (invisible.MoveNext(out var uid, out var comp, out var sprite))
        {
            if (!_shaders.TryGetValue(uid, out var shader))
                continue;

            if (sprite.PostShader == null)
            {
                sprite.PostShader = shader;
            }

            if (sprite.PostShader == shader)
            {
                var opacity = TryComp<EntityActiveInvisibleComponent>(uid, out var activeInvisible) ? activeInvisible.Opacity : 1;
                shader.SetParameter("visibility", opacity);
            }
        }
    }
}
