using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;
using DisCatSharp.Enums;

namespace EconomyBot;

/// Slash versions of the chat/admin commands. The commands themselves live in <see cref="ChatCommands"/>; anything
/// added here needs its twin in <see cref="ChatModule"/> (or BotModule).
///
/// The permission on an attribute here only decides who gets to see the command in the picker; the check which
/// actually counts is the one in ChatCommands.
public class ChatModuleSlash : ApplicationCommandsModule {
    // NOTE: ephemeral matters for purge, a visible "thinking..." message would be one of the messages it deletes
    [SlashCommand("purge", "Deletes the last messages in this channel.", (long)Permissions.ManageMessages)]
    public Task purge(InteractionContext ctx,
        [Option("amount", "How many messages to delete."), MinimumValue(1), MaximumValue(100)]
        long amount) => Run.slash(ctx, c => ChatCommands.purge(c, (int)amount), true);

    [SlashCommand("role", "Gives a role to every member of the server.", (long)Permissions.Administrator)]
    public Task role(InteractionContext ctx, [Option("role", "The role to hand out.")] DiscordRole role) =>
        Run.slash(ctx, c => ChatCommands.role(c, role), true);

    [SlashCommand("get", "Counts how many members have a role.")]
    public Task get(InteractionContext ctx, [Option("role", "The role to count.")] DiscordRole role) =>
        Run.slash(ctx, c => ChatCommands.get(c, role));

    [SlashCommand("joinlog", "Shows or sets where \"User joined\" messages go.", (long)Permissions.ManageGuild)]
    public Task joinLog(InteractionContext ctx,
        [Option("channel", "Channel to post joins in."), ChannelTypes(ChannelType.Text, ChannelType.News)]
        DiscordChannel? channel = null,
        [Option("off", "Turn the join log off.")]
        bool off = false) => Run.slash(ctx,
        c => off ? ChatCommands.joinLogOff(c) :
            channel is null ? ChatCommands.joinLogShow(c) : ChatCommands.joinLogSet(c, channel), true);

    [SlashCommand("deletelog", "Shows or sets where deleted attachments and purged messages get logged.",
        (long)Permissions.ManageGuild)]
    public Task deleteLog(InteractionContext ctx,
        [Option("channel", "Channel to log deletions in."), ChannelTypes(ChannelType.Text, ChannelType.News)]
        DiscordChannel? channel = null,
        [Option("off", "Turn the deletion log off.")]
        bool off = false) => Run.slash(ctx,
        c => off ? ChatCommands.deleteLogOff(c) :
            channel is null ? ChatCommands.deleteLogShow(c) : ChatCommands.deleteLogSet(c, channel), true);

    [SlashCommand("joinrole", "Shows or sets the role new members get.", (long)Permissions.ManageGuild)]
    public Task joinRole(InteractionContext ctx,
        [Option("role", "Role to give to new members.")]
        DiscordRole? role = null,
        [Option("off", "Stop giving new members a role.")]
        bool off = false) => Run.slash(ctx,
        c => off ? ChatCommands.joinRoleOff(c) :
            role is null ? ChatCommands.joinRoleShow(c) : ChatCommands.joinRoleSet(c, role), true);

    [SlashCommand("ticketsetup", "Sets up ticketing for this server.", (long)Permissions.ManageGuild)]
    public Task ticketSetup(InteractionContext ctx,
        [Option("category", "Category the ticket channels get created in."), ChannelTypes(ChannelType.Category)]
        DiscordChannel category,
        [Option("staff", "Role which can see every ticket.")]
        DiscordRole staff,
        [Option("transcripts", "Channel the transcripts of closed tickets go to."),
         ChannelTypes(ChannelType.Text, ChannelType.News)]
        DiscordChannel transcripts) =>
        Run.slash(ctx, c => ChatCommands.ticketSetup(c, category, staff, transcripts), true);

    [SlashCommand("ticketpanel", "Posts the \"Open ticket\" button in this channel.", (long)Permissions.ManageGuild)]
    public Task ticketPanel(InteractionContext ctx,
        [Option("text", "What the panel should say. Leave empty for the default.")]
        string text = "") => Run.slash(ctx, c => ChatCommands.ticketPanel(c, text), true);

    [SlashCommand("roll", "Rolls a die.")]
    public Task roll(InteractionContext ctx,
        [Option("sides", "How many sides the die has."), MinimumValue(1)]
        long sides = 6) => Run.slash(ctx, c => ChatCommands.roll(c, (int)Math.Min(sides, int.MaxValue - 1)));
}
