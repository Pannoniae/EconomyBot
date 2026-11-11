using System.Text;
using System.Text.RegularExpressions;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using EconomyBot.Logging;

namespace EconomyBot;

/// Button-driven tickets: a panel with an "Open ticket" button, one private channel per ticket, and a plain-text
/// transcript posted to the server's transcript channel when it's closed.
///
/// NOTE: nothing about a ticket is stored on our side. The owner lives in the channel topic and the transcript lives
/// in a Discord channel, so the only thing in guilds.json is the setup and a counter.
public static class Tickets {
    public const string OPEN = "ticket:open";
    public const string CLOSE = "ticket:close";

    private const string OWNER_TAG = "ticket-owner:";

    /// what the opener and the staff role get inside a ticket
    private const Permissions PARTICIPANT = Permissions.AccessChannels | Permissions.SendMessages |
                                            Permissions.ReadMessageHistory | Permissions.AttachFiles |
                                            Permissions.EmbedLinks;

    /// what we need in the ticket category to run the whole thing
    public const Permissions CATEGORY_NEEDS = PARTICIPANT | Permissions.ManageChannels | Permissions.ManageRoles;

    /// what we need in the transcript channel
    public const Permissions TRANSCRIPT_NEEDS = Permissions.AccessChannels | Permissions.SendMessages |
                                                Permissions.EmbedLinks | Permissions.AttachFiles;

    private static readonly Logger logger = Logger.getClassLogger("Tickets");

    // double-click protection: users opening a ticket right now, channels being closed right now
    private static readonly Lock sync = new();
    private static readonly HashSet<(ulong guild, ulong user)> opening = [];
    private static readonly HashSet<ulong> closing = [];

    public static DiscordMessageBuilder panel(string text) => new DiscordMessageBuilder()
        .AddEmbed(new DiscordEmbedBuilder()
            .WithTitle("Contact the moderators")
            .WithDescription(text)
            .WithColor(DiscordColor.Blurple)
            .Build())
        .AddComponents(new DiscordButtonComponent(ButtonStyle.Primary, OPEN, "Open ticket"));

    public static Task onComponent(DiscordClient sender, ComponentInteractionCreateEventArgs e) {
        if (e.Id is not (OPEN or CLOSE) || e.Guild is null) {
            return Task.CompletedTask;
        }

        // closing reads the whole channel, don't sit on the event dispatcher for that
        _ = Task.Run(async () => {
            try {
                await (e.Id == OPEN ? open(sender, e) : close(e));
            }
            catch (Exception ex) when (Program.isExpected(ex)) {
                logger.warn($"Ticket button {e.Id} in {e.Channel.Id}: {Program.describe(ex)}");
            }
            catch (Exception ex) {
                await Program.reportError($"handling ticket button {e.Id} in {e.Channel.Id}", ex);
            }
        });
        return Task.CompletedTask;
    }

    private static ulong? ownerOf(DiscordChannel channel) {
        var topic = channel.Topic;
        if (topic == null || !topic.StartsWith(OWNER_TAG, StringComparison.Ordinal)) {
            return null;
        }

        return ulong.TryParse(topic.AsSpan(OWNER_TAG.Length), out var id) ? id : null;
    }

    private static Task reply(DiscordInteraction interaction, string text) =>
        interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent(text));

    private static async Task open(DiscordClient sender, ComponentInteractionCreateEventArgs e) {
        var guild = e.Guild!;
        var user = e.User;

        // creating a channel can take longer than the 3 seconds an interaction gives us
        await e.Interaction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        if (GuildSettingsStore.read(guild.Id, s => s.tickets) is not { } cfg ||
            guild.GetChannel(cfg.category) is not { Type: ChannelType.Category } category) {
            await reply(e.Interaction, "Ticketing isn't set up on this server (or its category is gone). Poke an admin.");
            return;
        }

        var alreadyOpening = false;
        lock (sync) {
            if (!opening.Add((guild.Id, user.Id))) {
                alreadyOpening = true;
            }
        }

        if (alreadyOpening) {
            await reply(e.Interaction, "Already on it, give me a second.");
            return;
        }

        try {
            if (category.Children.FirstOrDefault(c => ownerOf(c) == user.Id) is { } existing) {
                await reply(e.Interaction, $"You already have an open ticket: {existing.Mention}");
                return;
            }

            var number = GuildSettingsStore.updateAndGet(guild.Id, s => s.nextTicket++);
            // NOTE: the @everyone role has the same ID as the guild
            DiscordOverwriteBuilder[] overwrites = [
                new DiscordOverwriteBuilder(guild.Id, OverwriteType.Role).Deny(Permissions.AccessChannels),
                new DiscordOverwriteBuilder(user.Id, OverwriteType.Member).Allow(PARTICIPANT),
                new DiscordOverwriteBuilder(cfg.staffRole, OverwriteType.Role).Allow(PARTICIPANT),
                new DiscordOverwriteBuilder(sender.CurrentUser.Id, OverwriteType.Member)
                    .Allow(PARTICIPANT | Permissions.ManageChannels)
            ];

            var channel = await guild.CreateTextChannelAsync($"ticket-{number:0000}", category,
                $"{OWNER_TAG}{user.Id}", overwrites);

            await channel.SendMessageAsync(new DiscordMessageBuilder()
                .WithContent($"{user.Mention} opened this ticket, <@&{cfg.staffRole}> will be with you.\n" +
                             "Describe your problem here. Press the button when it's sorted - the channel gets " +
                             "deleted and the moderators keep a transcript.")
                .AddComponents(new DiscordButtonComponent(ButtonStyle.Danger, CLOSE, "Close ticket")));

            await reply(e.Interaction, $"Ticket opened: {channel.Mention}");
        }
        finally {
            lock (sync) {
                opening.Remove((guild.Id, user.Id));
            }
        }
    }

    private static async Task close(ComponentInteractionCreateEventArgs e) {
        var guild = e.Guild!;
        var channel = e.Channel;

        if (GuildSettingsStore.read(guild.Id, s => s.tickets) is not { } cfg ||
            channel.ParentId != cfg.category || ownerOf(channel) is not { } owner) {
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("This isn't a ticket channel.").AsEphemeral());
            return;
        }

        // without message content a transcript would be a list of blank lines, and the channel is the only copy
        if (!Constants.privilegedIntents) {
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent(
                    "I can't save transcripts right now, so I'm not deleting this ticket. A moderator can delete " +
                    "the channel by hand once it's dealt with."));
            return;
        }

        lock (sync) {
            if (!closing.Add(channel.Id)) {
                return;
            }
        }

        try {
            await e.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent(
                    $"Closed by {e.User.Mention}, saving the transcript..."));
            // our own "closing" notice isn't part of the conversation
            var notice = (await e.Interaction.GetOriginalResponseAsync()).Id;

            if (guild.GetChannel(cfg.transcriptChannel) is not { } target) {
                await reply(e.Interaction, "The transcript channel is gone, so I'm not deleting anything. Poke an admin.");
                return;
            }

            var messages = await fetchAll(channel);
            messages.RemoveAll(m => m.Id == notice);
            var saved = await Program.tryDiscord($"posting the transcript of {channel.Name}", async () => {
                await using var file = new MemoryStream(Encoding.UTF8.GetBytes(render(guild, channel, owner, e.User, messages)));
                await target.SendMessageAsync(new DiscordMessageBuilder()
                    .AddEmbed(summary(channel, owner, e.User, messages))
                    .AddFile($"transcript-{channel.Name}.txt", file));
            });

            // the transcript is the whole point: if it didn't land, the channel stays
            if (!saved) {
                await reply(e.Interaction, "Couldn't save the transcript, so the ticket stays open. Poke an admin.");
                return;
            }

            await archiveAttachments(target, channel, messages);
            await channel.DeleteAsync($"ticket closed by {e.User.Username} ({e.User.Id})");
        }
        finally {
            lock (sync) {
                closing.Remove(channel.Id);
            }
        }
    }

    /// Every message in the channel, oldest first.
    private static async Task<List<DiscordMessage>> fetchAll(DiscordChannel channel) {
        var all = new List<DiscordMessage>();
        var page = await channel.GetMessagesAsync(100);
        while (page.Count > 0) {
            all.AddRange(page);
            // pages come newest first, so the oldest one we've seen is the cursor
            page = await channel.GetMessagesBeforeAsync(page.Min(m => m.Id), 100);
        }

        all.Sort((a, b) => a.Id.CompareTo(b.Id));
        return all;
    }

    /// Name an attachment gets when we re-upload it. Prefixed with its ID so ten "image.png"s don't collide, and so
    /// the transcript can point at the exact file.
    private static string savedName(DiscordAttachment a) =>
        $"{a.Id}_{(string.IsNullOrEmpty(a.Filename) ? "attachment" : a.Filename)}";

    private static string render(DiscordGuild guild, DiscordChannel channel, ulong owner, DiscordUser closer,
        List<DiscordMessage> messages) {
        var sb = new StringBuilder();
        sb.AppendLine($"Server:    {guild.Name} ({guild.Id})");
        sb.AppendLine($"Ticket:    {channel.Name} ({channel.Id})");
        sb.AppendLine($"Owner:     {owner}");
        sb.AppendLine($"Opened:    {channel.CreationTimestamp.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Closed:    {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by {closer.Username} ({closer.Id})");
        sb.AppendLine($"Messages:  {messages.Count}");
        sb.AppendLine();
        sb.AppendLine("Participants:");
        foreach (var (author, count) in participants(messages)) {
            sb.AppendLine($"  {count,4} - {author.Username} ({author.Id})");
        }

        sb.AppendLine();
        sb.AppendLine(new string('-', 72));

        foreach (var m in messages) {
            sb.Append($"[{m.Timestamp.UtcDateTime:yyyy-MM-dd HH:mm:ss}] {m.Author?.Username ?? "unknown"}: ");
            sb.AppendLine(string.IsNullOrEmpty(m.Content)
                ? ""
                : readable(guild, m).ReplaceLineEndings("\n    "));
            foreach (var a in m.Attachments) {
                sb.AppendLine($"    [attachment: {savedName(a)}]");
            }

            foreach (var embed in m.Embeds) {
                sb.AppendLine($"    [embed: {embed.Title ?? embed.Description ?? embed.Url?.ToString() ?? "untitled"}]");
            }

            foreach (var sticker in m.Stickers) {
                sb.AppendLine($"    [sticker: {sticker.Name}]");
            }
        }

        return sb.ToString();
    }

    /// Message text with mentions spelled out: a transcript full of <@395573709525745666> is no use to anyone.
    private static string readable(DiscordGuild guild, DiscordMessage m) =>
        Regex.Replace(m.Content, @"<(@!?|@&|#)(\d{15,21})>", match => {
            if (!ulong.TryParse(match.Groups[2].Value, out var id)) {
                return match.Value;
            }

            return match.Groups[1].Value switch {
                "@&" => guild.GetRole(id) is { } role ? $"@{role.Name}" : match.Value,
                "#" => guild.GetChannel(id) is { } channel ? $"#{channel.Name}" : match.Value,
                _ => m.MentionedUsers?.FirstOrDefault(u => u.Id == id) is { } user ? $"@{user.Username}" :
                    guild.Members.TryGetValue(id, out var member) ? $"@{member.Username}" : match.Value
            };
        });

    private static IEnumerable<(DiscordUser author, int count)> participants(List<DiscordMessage> messages) =>
        messages.Where(m => m.Author != null)
            .GroupBy(m => m.Author.Id)
            .Select(g => (g.First().Author, g.Count()))
            .OrderByDescending(p => p.Item2);

    private static DiscordEmbed summary(DiscordChannel channel, ulong owner, DiscordUser closer,
        List<DiscordMessage> messages) {
        var users = string.Join('\n',
            participants(messages).Select(p => $"{p.count} - <@{p.author.Id}> - {p.author.Username}"));
        if (users.Length == 0) {
            users = "nobody said anything";
        }

        return new DiscordEmbedBuilder()
            .WithTitle($"Ticket closed: {channel.Name}")
            .WithColor(DiscordColor.Green)
            .AddField(new DiscordEmbedField("Owner", $"<@{owner}>", true))
            .AddField(new DiscordEmbedField("Closed by", closer.Mention, true))
            .AddField(new DiscordEmbedField("Messages", messages.Count.ToString(), true))
            // discord caps a field at 1024 chars
            .AddField(new DiscordEmbedField("Users in transcript", users.Length <= 1024 ? users : users[..1021] + "..."))
            .WithTimestamp(DateTimeOffset.UtcNow)
            .Build();
    }

    /// Re-upload the ticket's attachments next to the transcript: the CDN links die with the channel.
    /// Done in batches as we go, so a ticket full of screenshots is never all in memory at once.
    private static async Task archiveAttachments(DiscordChannel target, DiscordChannel ticket,
        List<DiscordMessage> messages) {
        var batch = new Dictionary<string, Stream>();
        long size = 0;
        var skipped = new List<string>();

        async Task flush() {
            if (batch.Count == 0) {
                return;
            }

            try {
                var files = batch;
                await Program.tryDiscord($"archiving attachments of {ticket.Name}",
                    () => target.SendMessageAsync(new DiscordMessageBuilder().AddFiles(files)));
            }
            finally {
                foreach (var s in batch.Values) {
                    await s.DisposeAsync();
                }

                batch = new Dictionary<string, Stream>();
                size = 0;
            }
        }

        try {
            foreach (var m in messages) {
                foreach (var a in m.Attachments) {
                    var data = await Program.fetchAttachment(a, m.Id);
                    if (data == null) {
                        skipped.Add(savedName(a));
                        continue;
                    }

                    if (batch.Count >= Program.FILES_PER_MESSAGE || size + data.Length > Program.MAX_ARCHIVE_SIZE) {
                        await flush();
                    }

                    batch[savedName(a)] = data;
                    size += data.Length;
                }
            }
        }
        finally {
            await flush();
        }

        if (skipped.Count > 0) {
            var note = $"Attachments of {ticket.Name} I couldn't save (too big or already gone):\n" +
                       string.Join('\n', skipped);
            await Program.tryDiscord($"listing skipped attachments of {ticket.Name}",
                () => target.SendMessageAsync(note.Length <= 2000 ? note : note[..1997] + "..."));
        }
    }
}
