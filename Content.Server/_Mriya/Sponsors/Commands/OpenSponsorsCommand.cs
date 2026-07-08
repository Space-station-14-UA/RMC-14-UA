using Content.Server.Administration;
using Content.Server.EUI;
using Content.Server.Mriya.Sponsors.UI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Mriya.Sponsors.Commands
{
    [AdminCommand(AdminFlags.Permissions)]
    public sealed partial class OpenSponsorsCommand : LocalizedEntityCommands
    {
        [Dependency] private EuiManager _euiManager = default!;

        public override string Command => "sponsors";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine(Loc.GetString($"shell-cannot-run-command-from-server"));
                return;
            }

            var ui = new AdminSponsorsEui();
            _euiManager.OpenEui(ui, player);
        }
    }

    [AnyCommand]
    public sealed partial class OpenSponsorsWindowCommand : LocalizedEntityCommands
    {
        [Dependency] private EuiManager _euiManager = default!;

        public override string Command => "sponsorwindow";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine(Loc.GetString($"shell-cannot-run-command-from-server"));
                return;
            }

            var ui = new SponsorListEui();
            _euiManager.OpenEui(ui, player);
        }
    }

    [AnyCommand]
    public sealed partial class OpenPersonalSponsorWindowCommand : LocalizedEntityCommands
    {
        [Dependency] private EuiManager _euiManager = default!;

        public override string Command => "sponsorsettings"; // Або можеш змінити на "sponsorpersonal"

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine(Loc.GetString("shell-cannot-run-command-from-server"));
                return;
            }

            // Відкриваємо наше нове вікно персональних налаштувань
            var ui = new PersonalSponsorEui();
            _euiManager.OpenEui(ui, player);
        }
    }
}
