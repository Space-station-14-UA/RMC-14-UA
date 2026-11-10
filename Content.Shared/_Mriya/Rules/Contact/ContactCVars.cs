using Robust.Shared.Configuration;

namespace Content.Shared._Mriya.Rules.Contact;

/// <summary>
/// Client-side Contact HUD settings, saved between sessions.
/// Position: top-left of the panel in UI pixels, per HUD layout; -1 = default placement.
/// </summary>
[CVarDefs]
public sealed class ContactCVars
{
    public static readonly CVarDef<bool> HudCollapsed =
        CVarDef.Create("mriya.contact_hud.collapsed", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bitmask of hidden lines (ContactHudLines on the client).
    /// </summary>
    public static readonly CVarDef<int> HudHiddenLines =
        CVarDef.Create("mriya.contact_hud.hidden_lines", 0, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> HudDefaultX =
        CVarDef.Create("mriya.contact_hud.default_x", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> HudDefaultY =
        CVarDef.Create("mriya.contact_hud.default_y", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> HudSeparatedX =
        CVarDef.Create("mriya.contact_hud.separated_x", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> HudSeparatedY =
        CVarDef.Create("mriya.contact_hud.separated_y", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}