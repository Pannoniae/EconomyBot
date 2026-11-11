using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Attributes;
using DisCatSharp.Entities;

namespace EconomyBot;

/// Text versions of the chat/admin commands. The commands themselves live in <see cref="ChatCommands"/>; anything
/// added here needs its twin in <see cref="ChatModuleSlash"/>.
public class ChatModule : BaseCommandModule {
    [Command]
    public Task purge(CommandContext ctx, int amt) => Run.prefix(ctx, c => ChatCommands.purge(c, amt));

    [Command]
    public Task role(CommandContext ctx, DiscordRole role) => Run.prefix(ctx, c => ChatCommands.role(c, role));

    [Command]
    public Task get(CommandContext ctx, DiscordRole role) => Run.prefix(ctx, c => ChatCommands.get(c, role));

    [Command("joinlog"), Priority(1)]
    public Task joinLog(CommandContext ctx, DiscordChannel channel) =>
        Run.prefix(ctx, c => ChatCommands.joinLogSet(c, channel));

    /// <summary>
    /// ".joinlog off" turns it off, ".joinlog" on its own says where it currently goes.
    /// </summary>
    [Command("joinlog")]
    public Task joinLog(CommandContext ctx, [RemainingText] string? arg = null) => Run.prefix(ctx,
        c => string.Equals(arg?.Trim(), "off", StringComparison.OrdinalIgnoreCase)
            ? ChatCommands.joinLogOff(c)
            : ChatCommands.joinLogShow(c));

    [Command("deletelog"), Priority(1)]
    public Task deleteLog(CommandContext ctx, DiscordChannel channel) =>
        Run.prefix(ctx, c => ChatCommands.deleteLogSet(c, channel));

    [Command("deletelog")]
    public Task deleteLog(CommandContext ctx, [RemainingText] string? arg = null) => Run.prefix(ctx,
        c => string.Equals(arg?.Trim(), "off", StringComparison.OrdinalIgnoreCase)
            ? ChatCommands.deleteLogOff(c)
            : ChatCommands.deleteLogShow(c));

    [Command("joinrole"), Priority(1)]
    public Task joinRole(CommandContext ctx, DiscordRole role) =>
        Run.prefix(ctx, c => ChatCommands.joinRoleSet(c, role));

    [Command("joinrole")]
    public Task joinRole(CommandContext ctx, [RemainingText] string? arg = null) => Run.prefix(ctx,
        c => string.Equals(arg?.Trim(), "off", StringComparison.OrdinalIgnoreCase)
            ? ChatCommands.joinRoleOff(c)
            : ChatCommands.joinRoleShow(c));

    /// <summary>
    /// The category has to be given by ID, there's no way to mention one.
    /// </summary>
    [Command("ticketsetup")]
    public Task ticketSetup(CommandContext ctx, DiscordChannel category, DiscordRole staff,
        DiscordChannel transcripts) => Run.prefix(ctx, c => ChatCommands.ticketSetup(c, category, staff, transcripts));

    [Command("ticketpanel")]
    public Task ticketPanel(CommandContext ctx, [RemainingText] string? text = null) =>
        Run.prefix(ctx, c => ChatCommands.ticketPanel(c, text));

    [Command]
    public Task roll(CommandContext ctx) => Run.prefix(ctx, c => ChatCommands.roll(c, 6));

    [Command]
    public Task roll(CommandContext ctx, int sides) => Run.prefix(ctx, c => ChatCommands.roll(c, sides));

    [Command("webhook")]
    public Task sendWebhook(CommandContext ctx, [RemainingText] string message) =>
        Run.prefix(ctx, c => ChatCommands.webhook(c, message));

    [Command("user")]
    public Task userWebhook(CommandContext ctx, [RemainingText] string message) =>
        Run.prefix(ctx, c => ChatCommands.userWebhook(c, message));

    [Command("squish")]
    public Task squish(CommandContext ctx, DiscordMember member) =>
        Run.prefix(ctx, c => ChatCommands.squish(c, member));

    [Command("0x")]
    public Task love0x(CommandContext ctx) => Run.prefix(ctx, ChatCommands.love0x);

    [Command("panno")]
    public Task lovepanno(CommandContext ctx) => Run.prefix(ctx, ChatCommands.lovepanno);
}
