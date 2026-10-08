using Content.Server._Core.AudioDirector.UI;
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Core.AudioDirector.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class OpenAudioDirectorEui : LocalizedEntityCommands
{
    [Dependency] private EuiManager _euiManager = default!;

    public override string Command => "audiodirector";

    public override void Execute(
        IConsoleShell shell,
        string argStr,
        string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteError(Loc.GetString($"shell-cannot-run-command-from-server"));
            return;
        }

        var ui = new AudioDirectorEui();
        _euiManager.OpenEui(ui, player);
    }
}
