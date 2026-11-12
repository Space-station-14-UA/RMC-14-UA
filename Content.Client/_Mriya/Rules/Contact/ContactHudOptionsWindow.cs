using Content.Shared._Mriya.Rules.Contact;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;

namespace Content.Client._Mriya.Rules.Contact;

/// <summary>
/// Which HUD lines are shown, plus resetting the saved position.
/// </summary>
public sealed class ContactHudOptionsWindow : DefaultWindow
{

    private static readonly ContactHudLines[] Lines =
    {
        ContactHudLines.Field,
        ContactHudLines.Wave,
        ContactHudLines.NextWave,
        ContactHudLines.Extraction,
        ContactHudLines.Shuttle,
        ContactHudLines.Aboard,
        ContactHudLines.Gunship,
        ContactHudLines.Balance,
        ContactHudLines.Anchor,
    };

    public ContactHudOptionsWindow(IConfigurationManager cfg)
    {
        Title = Loc.GetString("mriya-contact-hud-options-title");
        MinWidth = 260;

        var box = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        Contents.AddChild(box);

        var hidden = (ContactHudLines) cfg.GetCVar(ContactCVars.HudHiddenLines);
        foreach (var flag in Lines)
        {
            var check = new CheckBox
            {
                Text = Loc.GetString($"mriya-contact-hud-option-{flag.ToString().ToLowerInvariant()}"),
                Pressed = (hidden & flag) == 0,
            };

            check.OnToggled += args =>
            {
                var current = (ContactHudLines) cfg.GetCVar(ContactCVars.HudHiddenLines);
                current = args.Pressed ? current & ~flag : current | flag;
                cfg.SetCVar(ContactCVars.HudHiddenLines, (int) current);
                cfg.SaveToFile();
            };

            box.AddChild(check);
        }

        var reset = new Button { Text = Loc.GetString("mriya-contact-hud-option-reset-position") };
        reset.OnPressed += _ =>
        {
            cfg.SetCVar(ContactCVars.HudDefaultX, -1f);
            cfg.SetCVar(ContactCVars.HudDefaultY, -1f);
            cfg.SetCVar(ContactCVars.HudSeparatedX, -1f);
            cfg.SetCVar(ContactCVars.HudSeparatedY, -1f);
            cfg.SaveToFile();
        };
        box.AddChild(reset);
    }
}

[Flags]
public enum ContactHudLines
{
    None = 0,
    Field = 1 << 0,
    Wave = 1 << 1,
    NextWave = 1 << 2,
    Extraction = 1 << 3,
    Shuttle = 1 << 4,
    Aboard = 1 << 5,
    Gunship = 1 << 6,
    Balance = 1 << 7,
    Anchor = 1 << 8,
}