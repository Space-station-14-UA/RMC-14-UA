using Content.Server.Administration;
using Content.Server.EUI;
using Content.Server.Mriya.Sponsors.UI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Mriya.Sponsors.Commands
{
    /// <summary>
    /// Administrative command for opening the sponsor and rank configuration window. Restricted to administrators with the <see cref="AdminFlags.Permissions"/> flag. Used for managing sponsors and their server access rights.
    /// </summary>
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

    /// <summary>
    /// Player command that opens the sponsor list window. Available to all players for viewing the list of sponsors.
    /// </summary>
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

    /// <summary>
    /// Command to open the personal sponsor settings window. Available to all players for viewing and modifying their own sponsor settings.
    /// </summary>
    [AnyCommand]
    public sealed partial class OpenPersonalSponsorWindowCommand : LocalizedEntityCommands
    {
        [Dependency] private EuiManager _euiManager = default!;

        public override string Command => "sponsorsettings";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player == null)
            {
                shell.WriteLine(Loc.GetString("shell-cannot-run-command-from-server"));
                return;
            }

            var ui = new PersonalSponsorEui();
            _euiManager.OpenEui(ui, player);
        }
    }
}
