using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared._Wormix.CCVar;
using Robust.Shared.Network;
using Content.Server.Database;
using Robust.Shared.Localization;

namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    [Dependency] private readonly DiscordWebhook _discord = default!;
    private WebhookData? _webhook;

    private void InitializeDiscord()
    {
        var value = _cfg.GetCVar(CCVar.BanDiscordWebhook);
        if (!string.IsNullOrEmpty(value))
        {
            _discord.TryGetWebhook(value, val => _webhook = val);
        }
    }

    /// <summary>
    /// Send to webhook a server ban message.
    /// </summary>
    private async void SendServerBanWebhook(BanDef ban, string player, string admin)
    {
        try
        {
            if (_webhook is null)
                return;

            var hook = _webhook.Value.ToIdentifier();
            var id = ban.Id?.ToString() ?? "?";
            var rId = string.Join(", ", ban.RoundIds) ?? "?";

            var footer = Loc.GetString("ban-manager-notify-discord-footer", ("rounds", rId), ("id", id));
            var title = Loc.GetString("ban-manager-notify-discord-title");
            var color = 0xFF0000; // red

            var message = "...";

            if (ban.ExpirationTime is null)
                message = Loc.GetString("ban-manager-notify-discord-perma",
                    ("admin", admin),
                    ("player", player),
                    ("reason", ban.Reason));
            else
                message = Loc.GetString("ban-manager-notify-discord",
                    ("admin", admin),
                    ("player", player),
                    ("expiration", ban.ExpirationTime.Value.DateTime),
                    ("reason", ban.Reason));

            var embed = new WebhookEmbed
            {
                Title = "Была выдана блокировка",
                Description = message,
                Color = color,
                Footer = new WebhookEmbedFooter
                {
                    Text = footer
                },
                Timestamp = DateTime.UtcNow
            };

            var payload = new WebhookPayload
            {
                Embeds = new List<WebhookEmbed> { embed }
            };

            await _discord.CreateMessage(hook, payload);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to send ban information to webhook!\n{e}");
        }
    }

    /// <summary>
    /// Send to webhook a role ban message.
    /// </summary>
    private async void SendRoleBanWebhook(BanDef ban, string player, string admin)
    {
        try
        {
            if (_webhook is null)
                return;

            var hook = _webhook.Value.ToIdentifier();
            var id = ban.Id?.ToString() ?? "?";
            var rId = string.Join(", ", ban.RoundIds) ?? "?";

            var footer = Loc.GetString("ban-manager-notify-discord-footer", ("rounds", rId), ("id", id));
            var color = 0xFFA500; // oran

            var message = "...";

            if (ban.ExpirationTime is null)
                message = Loc.GetString("ban-manager-notify-discord-role-perma",
                    ("admin", admin),
                    ("roles", string.Join(", ", ban.Roles ?? [])),
                    ("player", player),
                    ("reason", ban.Reason));
            else
                message = Loc.GetString("ban-manager-notify-discord-role",
                    ("admin", admin),
                    ("player", player),
                    ("roles", string.Join(", ", ban.Roles ?? [])),
                    ("expiration", ban.ExpirationTime.Value.DateTime),
                    ("reason", ban.Reason));

            var embed = new WebhookEmbed
            {
                Title = "Была выдана блокировка роли",
                Description = message,
                Color = color,
                Footer = new WebhookEmbedFooter
                {
                    Text = footer
                },
                Timestamp = DateTime.UtcNow
            };

            var payload = new WebhookPayload
            {
                Embeds = new List<WebhookEmbed> { embed }
            };

            await _discord.CreateMessage(hook, payload);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to send roleban information to webhook!\n{e}");
        }
    }
}
