using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._Mriya.CCVar;

[CVarDefs]
public sealed partial class MriyaCVars : CVars
{
    /// <summary>
    /// Використовується для зміни інтенсивності тряски від вибухів у клієнта
    /// </summary>
    public static readonly CVarDef<float> MriyaExplosionShakeIntensity =
        CVarDef.Create("mriya.explosion_shake_intensity", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Використовується для відображення старих/наших спрайтів, замість РМСних у клієнта
    /// </summary>
    public static readonly CVarDef<bool> MRUseOldSprites =
        CVarDef.Create("mriya.use_old_sprites", true, CVar.REPLICATED | CVar.CLIENT | CVar.ARCHIVE);
}
