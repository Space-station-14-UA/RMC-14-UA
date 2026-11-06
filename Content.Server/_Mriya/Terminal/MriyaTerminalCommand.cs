using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Mriya.Terminal;

[AdminCommand(AdminFlags.Admin)]
public sealed class MriyaTerminalCommand : IConsoleCommand
{
    public string Command => "terminalmsg";
    public string Description => "Відкриває термінал комунікації або відправляє повідомлення до нього";
    public string Help => "terminalmsg [повідомлення]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            if (shell.Player is not { } player)
            {
                shell.WriteError("Цю команду можна виконати лише з клієнта");
                return;
            }

            var euiManager = IoCManager.Resolve<EuiManager>();
            euiManager.OpenEui(new MriyaTerminalAdminEui(), player);
            return;
        }

        var message = string.Join(" ", args);
        var system = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<MriyaTerminalSystem>();

        system.SendCorporateMessage(message);
        shell.WriteLine($"Корпоративне повідомлення відправлено: {message}");
    }
}
