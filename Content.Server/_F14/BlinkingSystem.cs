using Content.Shared._F14;
using Content.Shared.Ghost;
using Robust.Shared.GameObjects;

namespace Content.Server._F14;

public sealed class BlinkingSystem : EntitySystem
{
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlinkingComponent>();
        while (query.MoveNext(out var uid, out var blink))
        {
            // ігнор ангелів та привидів
            if (HasComp<WeepingAngelComponent>(uid) || HasComp<GhostComponent>(uid))
                continue;

            // 1. Відлік заплющених очей
            if (blink.IsBlinking)
            {
                blink.BlinkTimer -= frameTime;
                if (blink.BlinkTimer <= 0f)
                {
                    blink.IsBlinking = false;
                    Dirty(uid, blink);
                }
            }

            // 2. Якщо гравець поза аурою — таймер скидається
            if (!blink.InAngelAura)
            {
                blink.ForcedBlinkTimer = blink.ForcedBlinkInterval;
                continue;
            }

            // 3. Відлік часу біля ангела
            blink.ForcedBlinkTimer -= frameTime;
            if (blink.ForcedBlinkTimer <= 0f)
            {
                blink.ForcedBlinkTimer = blink.ForcedBlinkInterval;
                blink.IsBlinking = true;
                blink.BlinkTimer = blink.BlinkDuration;
                Dirty(uid, blink);
            }
        }
    }

    public void SetInAura(EntityUid uid, BlinkingComponent blink, bool inAura)
    {
        if (blink.InAngelAura == inAura)
            return;

        blink.InAngelAura = inAura;
        Dirty(uid, blink);
    }
}