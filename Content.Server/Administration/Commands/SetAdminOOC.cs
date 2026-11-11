// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared.Administration;
using Robust.Server.Player; // Wormix
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.NameColor)]
    internal sealed class SetAdminOOC : LocalizedCommands
    {
        [Dependency] private readonly IServerDbManager _dbManager = default!;
        [Dependency] private readonly IServerPreferencesManager _preferenceManager = default!;

        // Wormix start edit
        [Dependency] private readonly IPlayerLocator _locator = default!;
        [Dependency] private readonly IPlayerManager _players = default!;


        public override string Command => "setadminooc";

        public override async void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (args.Length != 2)
            {
                shell.WriteError(Loc.GetString("shell-wrong-arguments-number-need-specific",
                    ("properAmount", 2),
                    ("currentAmount", args.Length)));
                return;
            }


            var player = args[0].Trim();
            var user = await _locator.LookupIdByNameAsync(player);

            if (user != null)
            {
                var colorArg = string.Join(" ", args[1]).Trim();
                if (string.IsNullOrEmpty(colorArg))
                    return;

                var color = Color.TryFromHex(colorArg);
                if (!color.HasValue)
                {
                    shell.WriteError(Loc.GetString("shell-invalid-color-hex"));
                    return;
                }


                // Save the DB
                await _dbManager.SaveAdminOOCColorAsync(user.UserId, color.Value);
                // Update the cached preference
                var prefs = _preferenceManager.GetPreferences(user.UserId);
                prefs.AdminOOCColor = color.Value;

                return;
            }

            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));

        }


        public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        {
            if (args.Length == 1)
            {
                return CompletionResult.FromHintOptions(
                    _players.Sessions.Select(s => s.Name),
                    "Can't find");
            }

            return CompletionResult.Empty;
        }
        // Wormix edit end
    }
}
