using DisCatSharp.Entities;
using DisCatSharp.Enums;

namespace EconomyBot;

/// The chat/admin commands themselves, shared by the text (ChatModule, BotModule) and slash (ChatModuleSlash) stubs.
/// Permission checks live here, not in attributes on the stubs, so the two sides can't disagree about them.
public static class ChatCommands {
    public const ulong ZEROX = 1091089609234059316;

    /// <summary>
    /// It got abused, so it's manage messages-only.
    /// </summary>
    /// <param name="amt">How many messages to purge.</param>
    public static async Task purge(Cmd c, int amt) {
        if (!await c.require(Permissions.ManageMessages)) {
            return;
        }

        IReadOnlyList<DiscordMessage> messages;
        // the library is stupid
        if (c.repliedTo is { } from) {
            await Console.Out.WriteLineAsync("Purging from given message!");
            messages = await c.channel.GetMessagesBeforeAsync(from, amt);
        }
        else {
            messages = await c.channel.GetMessagesAsync(amt);
        }

        // purged messages go to this server's own deletion log, if it has one. We log them here, with the contents
        // we just fetched, because the deletion events only know what happened to be in the cache
        if (Program.deleteLogFor(c.channel) is { } log) {
            foreach (var m in messages) {
                Program.purged[m.Id] = 0;
            }

            await Program.logDeletedText(log, c.channel, messages, true);
        }

        await c.channel.DeleteMessagesAsync(messages);
        await c.respond($"Deleted {messages.Count} messages!");
    }

    /// <summary>
    /// ALL users of the server
    /// </summary>
    public static async Task role(Cmd c, DiscordRole role) {
        if (!await c.require(Permissions.Administrator)) {
            return;
        }

        var members = await c.guild.GetAllMembersAsync();
        foreach (var member in members) {
            await member.GrantRoleAsync(role);
        }
        await c.respond($"{members.Count}");
    }

    public static async Task get(Cmd c, DiscordRole role) {
        var totalMembers = (await c.guild.GetAllMembersAsync()).ToList();
        var members = totalMembers.Where(member => member.Roles.Contains(role)).ToList();

        await c.respond($"{members.Count} / {totalMembers.Count}");
    }

    /// <summary>
    /// Sets the channel the "User joined" embeds go to.
    /// </summary>
    public static async Task joinLogSet(Cmd c, DiscordChannel channel) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        if (channel.GuildId != c.guild.Id || channel.Type is not (ChannelType.Text or ChannelType.News)) {
            await c.respond("That has to be a text channel on this server.");
            return;
        }

        // complain now rather than silently dropping every join later
        const Permissions needed = Permissions.AccessChannels | Permissions.SendMessages | Permissions.EmbedLinks;
        if (!channel.PermissionsFor(c.guild.CurrentMember!).HasPermission(needed)) {
            await c.respond($"I can't post embeds in {channel.Mention}, fix my permissions there first.");
            return;
        }

        GuildSettingsStore.update(c.guild.Id, s => s.joinLogChannel = channel.Id);
        await c.respond($"Join/leave log set to {channel.Mention}.");
    }

    public static async Task joinLogOff(Cmd c) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        GuildSettingsStore.update(c.guild.Id, s => s.joinLogChannel = null);
        await c.respond("Join log turned off.");
    }

    public static async Task joinLogShow(Cmd c) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        var current = GuildSettingsStore.read(c.guild.Id, s => s.joinLogChannel);
        var state = current is { } id ? $"Join log currently goes to <#{id}>." : "Join log is off.";
        await c.respond($"{state} Usage: `{c.prefix}joinlog #channel` to set it, `off` to turn it off.");
    }

    /// <summary>
    /// Sets the channel deleted attachments and purged messages get reposted to.
    /// </summary>
    public static async Task deleteLogSet(Cmd c, DiscordChannel channel) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        if (channel.GuildId != c.guild.Id || channel.Type is not (ChannelType.Text or ChannelType.News)) {
            await c.respond("That has to be a text channel on this server.");
            return;
        }

        const Permissions needed = Permissions.AccessChannels | Permissions.SendMessages | Permissions.AttachFiles;
        if (!channel.PermissionsFor(c.guild.CurrentMember!).HasPermission(needed)) {
            await c.respond($"I can't post files in {channel.Mention}, fix my permissions there first.");
            return;
        }

        GuildSettingsStore.update(c.guild.Id, s => s.deleteLogChannel = channel.Id);
        await c.respond($"Deletion log set to {channel.Mention}. Keep that channel moderator-only.");
    }

    public static async Task deleteLogOff(Cmd c) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        GuildSettingsStore.update(c.guild.Id, s => s.deleteLogChannel = null);
        await c.respond("Deletion log turned off.");
    }

    public static async Task deleteLogShow(Cmd c) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        var current = GuildSettingsStore.read(c.guild.Id, s => s.deleteLogChannel);
        var state = current is { } id ? $"Deletion log currently goes to <#{id}>." : "Deletion log is off.";
        await c.respond($"{state} Usage: `{c.prefix}deletelog #channel` to set it, `off` to turn it off.");
    }

    /// <summary>
    /// Sets the role every new member gets.
    /// </summary>
    public static async Task joinRoleSet(Cmd c, DiscordRole role) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        // discord only lets us hand out roles below our own highest one
        var me = c.guild.CurrentMember!;
        if (!me.Permissions.HasPermission(Permissions.ManageRoles) || role.Position >= me.Hierarchy) {
            await c.respond($"I can't give out {role.Name}: I need Manage Roles and a role above it.");
            return;
        }

        GuildSettingsStore.update(c.guild.Id, s => s.joinRole = role.Id);
        await c.respond($"New members now get {role.Name}.");
    }

    public static async Task joinRoleOff(Cmd c) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        GuildSettingsStore.update(c.guild.Id, s => s.joinRole = null);
        await c.respond("Join role turned off.");
    }

    public static async Task joinRoleShow(Cmd c) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        var current = GuildSettingsStore.read(c.guild.Id, s => s.joinRole);
        var state = current is { } id && c.guild.GetRole(id) is { } role
            ? $"New members currently get {role.Name}."
            : "There is no join role.";
        await c.respond($"{state} Usage: `{c.prefix}joinrole @role` to set it, `off` to turn it off.");
    }

    /// <summary>
    /// Sets up ticketing: the category tickets get created in, the role which sees them all, and where the
    /// transcripts go.
    /// </summary>
    public static async Task ticketSetup(Cmd c, DiscordChannel category, DiscordRole staff, DiscordChannel transcripts) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        var guild = c.guild;
        if (category.GuildId != guild.Id || category.Type != ChannelType.Category) {
            await c.respond("The first argument has to be a category on this server (use its ID).");
            return;
        }

        if (transcripts.GuildId != guild.Id || transcripts.Type is not (ChannelType.Text or ChannelType.News)) {
            await c.respond("The transcript channel has to be a text channel on this server.");
            return;
        }

        // complain now rather than when the first ticket falls over
        if (!category.PermissionsFor(guild.CurrentMember!).HasPermission(Tickets.CATEGORY_NEEDS)) {
            await c.respond(
                $"I need these in the {category.Name} category: view, send, read history, attach files, embed links, manage channels, manage permissions.");
            return;
        }

        if (!transcripts.PermissionsFor(guild.CurrentMember!).HasPermission(Tickets.TRANSCRIPT_NEEDS)) {
            await c.respond($"I can't post embeds and files in {transcripts.Mention}, fix my permissions there first.");
            return;
        }

        GuildSettingsStore.update(guild.Id, s => s.tickets = new TicketConfig(category.Id, staff.Id, transcripts.Id));
        await c.respond(
            $"Tickets go into {category.Name}, {staff.Name} can see them, transcripts go to {transcripts.Mention}. Post the button with `{c.prefix}ticketpanel`.");
    }

    /// <summary>
    /// Posts the "Open ticket" button into the current channel.
    /// </summary>
    public static async Task ticketPanel(Cmd c, string? text) {
        if (!await c.require(Permissions.ManageGuild)) {
            return;
        }

        if (GuildSettingsStore.read(c.guild.Id, s => s.tickets) == null) {
            await c.respond($"Run `{c.prefix}ticketsetup` first: category, staff role, transcript channel.");
            return;
        }

        await c.channel.SendMessageAsync(Tickets.panel(string.IsNullOrWhiteSpace(text)
            ? "Got a problem or want to report something? Press the button to open a private channel with the moderators."
            : text));
    }

    public static async Task roll(Cmd c, int sides) {
        if (sides < 1) {
            await c.respond("A die needs at least one side.");
            return;
        }

        var num = new Random().Next(1, sides + 1);
        await c.respond($"You rolled {num}!");
    }

    public static Task webhook(Cmd c, string message) => Program.wiltery.sendWebhookToChannel(c.channel, message);

    public static Task userWebhook(Cmd c, string message) =>
        Program.wiltery.sendWebhookToChannelAsUser(c.channel, message, c.member);

    public static async Task squish(Cmd c, DiscordMember member) {
        var cat = "https://cdn.discordapp.com/attachments/1101712131222683659/1128320701456195594/image.png";
        await c.deleteInvocation();
        await Program.wiltery.sendWebhookToChannelWithCustomUser(c.channel, new DiscordMessageBuilder()
            .WithContent($"{member.DisplayName} was squished by a giant kitten.").AddEmbed(
                new DiscordEmbedBuilder().WithImageUrl(
                    cat)), cat, "Giant cat");
    }

    public static async Task love0x(Cmd c) {
        var zerox = "https://tenor.com/view/girl-anime-kiss-anime-i-love-you-girl-kiss-gif-14375355";
        await c.respond($"{(await c.guild.GetMemberAsync(ZEROX)).Mention} is amazing and I love them so much!");
        await c.respond(zerox);
    }

    public static async Task lovepanno(Cmd c) {
        var panno = "https://tenor.com/view/hug-gif-25588769";
        await c.respond("Pannoniae is a 12/10 human being that deserves love and appreciation <3");
        await c.respond(panno);
    }

    public static Task gc(Cmd c) {
        MemoryUtils.cleanGC();
        return Task.CompletedTask;
    }
}
