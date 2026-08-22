using Content.Shared._F14;
using Content.Shared.Ghost;
using Robust.Shared.GameObjects;

namespace Content.Server._F14;

/// <summary>
/// Серверна частина кліпання, яка керує відліком часу закриття очей та таймерами примусового(єдниого виду) кліпання в аурі.
/// </summary>
public sealed class BlinkingSystem : EntitySystem
{
    /// <summary>
    /// Оновлює таймери кліпання та викликає кліпання для гравців в аурі янгола.
    /// </summary>
    /// <param name="frameTime">Час, що минув з останнього кадру.</param>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BlinkingComponent>();
        while (query.MoveNext(out var uid, out var blink))
        {
            if (HasComp<WeepingAngelComponent>(uid) || HasComp<GhostComponent>(uid))
                continue;

            if (blink.IsBlinking)
            {
                blink.BlinkTimer -= frameTime;
                if (blink.BlinkTimer <= 0f)
                {
                    blink.IsBlinking = false;
                    Dirty(uid, blink);
                }
            }

            if (!blink.InAngelAura)
            {
                blink.ForcedBlinkTimer = blink.ForcedBlinkInterval;
                continue;
            }

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

    /// <summary>
    /// Дає мітку аури янгола
    /// </summary>
    /// <param name="uid">EntityUid сутності.</param>
    /// <param name="blink">Компонент кліпання.</param>
    /// <param name="inAura">Чи знаходиться в аурі.</param>
    public void SetInAura(EntityUid uid, BlinkingComponent blink, bool inAura)
    {
        if (blink.InAngelAura == inAura)
            return;

        blink.InAngelAura = inAura;
        Dirty(uid, blink);
    }
}