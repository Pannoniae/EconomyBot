using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Converters;
using DisCatSharp.CommandsNext.Exceptions;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;
using DisCatSharp.Exceptions;
using DisCatSharp.Interactivity;
using DisCatSharp.Interactivity.Enums;
using DisCatSharp.Interactivity.Extensions;
using DisCatSharp.Lavalink;
using DisCatSharp.Net;
using EconomyBot.Logging;
using Spectre.Console;

namespace EconomyBot;

public class Program {
    private static readonly Logger logger = Logger.getClassLogger("Main");

    public static LavalinkSession LavalinkNode = null!;
    public static MusicService musicService = null!;
    public static ImagesModule imagesModule = null!;
    public static WilteryHandler wiltery = null!;

    public static DiscordEmoji cube = null!;

    public static bool lavalinkInit = false;
    public static bool hasSetup = false;

    public static DiscordClient client = null!;
    
    private static readonly Dictionary<ulong, PlaybackState> savedPlaybackStates = new();
    private static DateTime lastReconnectionAttempt = DateTime.MinValue;
    private static DateTime lastSaveAttempt = DateTime.MinValue;
    private static DateTime lastRestoreAttempt = DateTime.MinValue;
    private static readonly Lock reconnectionLock = new();
    private static readonly Lock saveStateLock = new();
    private static readonly Lock restoreStateLock = new();
    private static bool needsLavalinkReconnection = false;

    public const ulong LOG = 838920584879800343;
    public static ulong HALLOFFAME = 1078991955633127474;
    public const ulong MY_GUILD = 838843082110664756;
    public const ulong BUG_REPORT_CATEGORY = 1472268871791808790;


    // shut up compiler
    private Program() {
        _ = Main(null!);
    }


    public static async Task Main(string[] args) {
        // logging

        // set global exception handler
        AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) => {
            logger.error(eventArgs.ExceptionObject as Exception ?? throw new InvalidOperationException());
        };
        AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) => {
            logger.info("Exiting...");
        };

        Logger.setLogLevel(LogLevel.INFO);

        Constants.init();
        GuildSettingsStore.load();

        var discord = new DiscordClient(new DiscordConfiguration {
            TokenType = TokenType.Bot,
            Token = Constants.token,
            // the two privileged ones we need: members (join role, GetAllMembersAsync) and message content
            // (prefix commands, deletion log, spam filters). No presences.
            Intents = Constants.privilegedIntents
                ? DiscordIntents.AllUnprivileged | DiscordIntents.GuildMembers | DiscordIntents.MessageContent
                : DiscordIntents.AllUnprivileged,
            ReconnectIndefinitely = true
        });
        client = discord;
        if (!Constants.privilegedIntents) {
            logger.warn("Running WITHOUT privileged intents: slash commands and ticket buttons only. No text " +
                        "commands, join log, join role, deletion log, spam filter or ticket transcripts.");
        }

        // error handling

        var endpoint = new ConnectionEndpoint {
            Hostname = "127.0.0.1", // From your server configuration.
            Port = 2333 // From your server configuration
        };

        var lavalinkConfig = new LavalinkConfiguration {
            Password = "youshallnotpass", // From your server configuration.
            RestEndpoint = endpoint,
            SocketEndpoint = endpoint
        };

        var lavalink = discord.UseLavalink();
        var commands = discord.UseCommandsNext(new CommandsNextConfiguration {
            // a test bot (BOT_PROFILE) gets its own prefixes, so it can sit in the same server as the real one
            // without both answering every ".something"
            StringPrefixes = Constants.profile == null ? [".", "!!"] : ["t.", "t!!"],
        });
        var slashCommands = discord.UseApplicationCommands();
        try {
            // every text command has a slash twin and the other way around, see Cmd.cs
            slashCommands.RegisterGlobalCommands<ChatModuleSlash>();
            slashCommands.RegisterGlobalCommands<MusicModuleSlash>();
            slashCommands.RegisterGlobalCommands<ImagesModuleSlash>();
            commands.CommandErrored += errorHandler;
            commands.RegisterCommands<ChatModule>();
            commands.RegisterCommands<MusicModule>();
            commands.RegisterCommands<ImagesModule>();
            commands.RegisterCommands<BotModule>();
        }
        catch (Exception e) {
            if (e is BadRequestException ex) {
                logger.error(ex.JsonMessage ?? "No message? wtf");
            }

            logger.error(e.Message);
        }

        discord.UseInteractivity(new InteractivityConfiguration {
            Timeout = TimeSpan.FromSeconds(180),
            PollBehaviour = PollBehaviour.KeepEmojis,
            AckPaginationButtons = true
        });
        // NOTE: without the privileged intents these would either never fire or fire with empty messages, and
        // an empty message is worse than no message (a deletion log full of blanks, a spam filter matching nothing)
        if (Constants.privilegedIntents) {
            discord.MessageCreated += messageHandler;
            discord.MessagesBulkDeleted += messageDeleteHandler;
            discord.MessageDeleted += messageDeleteHandler;
            discord.GuildMemberAdded += roleHandler;
            discord.GuildMemberAdded += joinLogHandler;
            discord.GuildMemberRemoved += leaveLogHandler;
        }

        discord.Ready += async (sender, _) => {
            await setup(sender, lavalink, lavalinkConfig);
            if (needsLavalinkReconnection) {
                needsLavalinkReconnection = false;
                await lavalinkReconnect();
            }
        };
        discord.Resumed += async (_, _) => {
            if (needsLavalinkReconnection) {
                needsLavalinkReconnection = false;
                await lavalinkReconnect();
            }
        };
        //discord.GuildDownloadCompleted += (sender, _) => setupB(sender, lavalink, lavalinkConfig);
        discord.ComponentInteractionCreated += Tickets.onComponent;

        discord.GetCommandsNext().UnregisterConverter<TimeSpan>();
        discord.GetCommandsNext().RegisterConverter(new CustomTimeSpanConverter());
        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        discord.ClientErrored += async (sender, e) => {
            logger.error($"An error occurred while connecting to Discord with type {e.EventName}");
            logger.error(e.Exception);
        };
        discord.SocketErrored += async (sender, e) => {
            logger.error(e.Exception);
            await saveAllPlaybackStates();
        };
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        await discord.ConnectAsync();
        MemoryUtils.cleanGC();
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        while (await timer.WaitForNextTickAsync()) {
            try {
                MemoryUtils.cleanGC();
            }
            catch (Exception e) {
                AnsiConsole.WriteException(e);
            }
        }

        // hold console window
        await Task.Delay(-1);
    }

    internal static bool isExpected(Exception e) => e switch {
        NotFoundException => true, // the thing is gone (10008 unknown message, 10007 unknown member, ...)
        UnauthorizedException => true, // not allowed to touch it
        ServerErrorException => true, // discord being discord
        RateLimitException => true,
        RequestSizeException => true, // attachment too fat to re-upload
        RestQueueFullException or RestQueueTimeoutException or RestCircuitBrokenException => true, // we're flooding
        HttpRequestException or TaskCanceledException => true, // network/CDN
        _ => false
    };

    /// One-liner for the log. The full stack trace of "message was already deleted" helps nobody.
    internal static string describe(Exception e) => e switch {
        NotFoundException x => $"404 {x.JsonMessage ?? x.Message}",
        UnauthorizedException x => $"403 {x.JsonMessage ?? x.Message}",
        BadRequestException x => $"400 {x.JsonMessage ?? x.Message}",
        ServerErrorException x => $"5xx {x.JsonMessage ?? x.Message}",
        _ => $"{e.GetType().Name}: {e.Message}"
    };

    /// Fire a Discord call which is allowed to fail because the target went away. Returns whether it landed.
    /// Use this for anything reacting to a message/member we don't control - by the time we get there, a spambot
    /// has usually been banned and its messages purged.
    internal static async Task<bool> tryDiscord(string what, Func<Task> action) {
        try {
            await action();
            return true;
        }
        catch (Exception e) when (isExpected(e)) {
            logger.warn($"{what}: {describe(e)}");
            return false;
        }
        catch (Exception e) {
            await reportError(what, e);
            return false;
        }
    }

    /// The log channel, or null if we can't reach it (mid-reconnect, kicked, whatever).
    private static async Task<DiscordChannel?> getLogChannel() {
        try {
            return (await client.GetGuildAsync(MY_GUILD)).GetChannel(LOG);
        }
        catch (Exception e) {
            logger.warn($"Log channel unreachable: {describe(e)}");
            return null;
        }
    }

    internal static async Task reportError(string what, Exception e) {
        logger.error($"{what}:");
        logger.error(e);
        try {
            var log = await getLogChannel();
            if (log == null) {
                return;
            }

            // discord caps messages at 2000 chars
            var report = $"Penis happened! ({what})\n```{e}```";
            await log.SendMessageAsync(report.Length <= 2000 ? report : report[..1997] + "```");
        }
        catch (Exception inner) {
            // if we can't even report the error, give up rather than recurse into another failing send
            logger.error($"Couldn't report the above: {describe(inner)}");
        }
    }

    /// Give new members the server's join role, if it has one configured (.joinrole).
    private static async Task roleHandler(DiscordClient sender, GuildMemberAddEventArgs e) {
        if (e.Member is not { } member) {
            return;
        }

        if (GuildSettingsStore.read(e.Guild.Id, s => s.joinRole) is not { } roleID) {
            return;
        }

        // spambots get banned within seconds of joining, so both of these routinely 404
        await tryDiscord($"granting the join role to {member.Id}", async () => {
            var role = await e.Guild.GetRoleAsync(roleID);
            await member.GrantRoleAsync(role);
        });
    }

    /// Post a "User joined" embed to the server's join log, if it has one configured (.joinlog).
    private static async Task joinLogHandler(DiscordClient sender, GuildMemberAddEventArgs e) {
        if (e.Member is not { } member) {
            return;
        }

        if (GuildSettingsStore.read(e.Guild.Id, s => s.joinLogChannel) is not { } channelId) {
            return;
        }

        if (e.Guild.GetChannel(channelId) is not { } channel) {
            logger.warn($"Join log channel {channelId} of {e.Guild.Id} is gone, not logging the join of {member.Id}");
            return;
        }

        var created = member.CreationTimestamp.ToUnixTimeSeconds();
        var embed = new DiscordEmbedBuilder()
            .WithTitle("\U0001F6AA User joined")
            .WithColor(DiscordColor.Green)
            .WithDescription($"**User:** `{member.Username} ({member.Id})`\n**User count:** `{e.Guild.MemberCount}`")
            .AddField(new DiscordEmbedField("Account created", $"<t:{created}:f> (<t:{created}:R>)"))
            .WithThumbnail(member.AvatarUrl)
            .WithTimestamp(DateTimeOffset.UtcNow);

        await tryDiscord($"logging the join of {member.Id}", () => channel.SendMessageAsync(embed.Build()));
    }

    /// Where deletions in this channel get logged, or null if its server has no deletion log (.deletelog) or this
    /// channel is the log itself. The log always lives in the same server as the deleted message.
    /// NOTE: use the channel from the event args, message.Channel is null for uncached messages!
    internal static DiscordChannel? deleteLogFor(DiscordChannel channel) {
        if (channel.Guild is not { } server ||
            GuildSettingsStore.read(server.Id, s => s.deleteLogChannel) is not { } logId || channel.Id == logId) {
            return null;
        }

        if (server.GetChannel(logId) is not { } log) {
            logger.warn($"Deletion log channel {logId} of {server.Id} is gone, not logging deletions in {channel.Id}");
            return null;
        }

        return log;
    }

    /// Messages .purge is deleting right now. The command logs those itself, with contents it fetched fresh, so the
    /// deletion events for them must not log the text a second time.
    internal static readonly System.Collections.Concurrent.ConcurrentDictionary<ulong, byte> purged = new();

    /// discord caps an embed description at 4096 chars
    private const int EMBED_LIMIT = 4000;

    /// Post what the deleted messages said to the deletion log. Goes into embeds, so nothing in the text can ping.
    /// Messages we know nothing about (deleted before we ever saw them, so not in the cache) are skipped.
    internal static async Task logDeletedText(DiscordChannel log, DiscordChannel source,
        IEnumerable<DiscordMessage> messages, bool includeOwn = false) {
        var self = client.CurrentUser.Id;
        var entries = messages
            .Where(m => m.Author is not null && (includeOwn || m.Author.Id != self) &&
                        (!string.IsNullOrEmpty(m.Content) || m.Attachments is { Count: > 0 }))
            .OrderBy(m => m.Id)
            .Select(m => {
                var text = string.IsNullOrEmpty(m.Content) ? "*no text*" : m.Content;
                var files = m.Attachments is { Count: > 0 } a ? $"\n*{a.Count} attachment(s)*" : "";
                var entry =
                    $"**{m.Author.Username}** (`{m.Author.Id}`) <t:{m.Timestamp.ToUnixTimeSeconds()}:f>\n{text}{files}";
                return entry.Length <= EMBED_LIMIT - 200 ? entry : entry[..(EMBED_LIMIT - 203)] + "...";
            })
            .ToList();
        if (entries.Count == 0) {
            return;
        }

        var title = entries.Count == 1 ? "Message deleted" : $"{entries.Count} messages deleted";
        var page = new StringBuilder($"In {source.Mention}");

        async Task flush() {
            await log.SendMessageAsync(new DiscordEmbedBuilder()
                .WithTitle(title)
                .WithColor(DiscordColor.Red)
                .WithDescription(page.ToString())
                .WithTimestamp(DateTimeOffset.UtcNow)
                .Build());
            page.Clear();
        }

        foreach (var entry in entries) {
            if (page.Length + entry.Length + 2 > EMBED_LIMIT) {
                await flush();
            }

            page.Append(page.Length == 0 ? "" : "\n\n").Append(entry);
        }

        await flush();
    }

    /// Post a "User left" embed to the same channel as the joins. Covers leaving, kicks and bans alike - discord
    /// doesn't tell us which it was.
    private static async Task leaveLogHandler(DiscordClient sender, GuildMemberRemoveEventArgs e) {
        if (e.Member is not { } member) {
            return;
        }

        if (GuildSettingsStore.read(e.Guild.Id, s => s.joinLogChannel) is not { } channelId ||
            e.Guild.GetChannel(channelId) is not { } channel) {
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithTitle("\U0001F44B User left")
            .WithColor(DiscordColor.Red)
            .WithDescription($"**User:** `{member.Username} ({member.Id})`\n**User count:** `{e.Guild.MemberCount}`")
            .WithThumbnail(member.AvatarUrl)
            .WithTimestamp(DateTimeOffset.UtcNow);

        // NOTE: only known if the member was in the cache; a default date means it wasn't
        if (member.JoinedAt.Year > 2015) {
            var joined = member.JoinedAt.ToUnixTimeSeconds();
            embed.AddField(new DiscordEmbedField("Joined", $"<t:{joined}:f> (<t:{joined}:R>)"));
        }

        await tryDiscord($"logging the departure of {member.Id}", () => channel.SendMessageAsync(embed.Build()));
    }

    private static async Task messageDeleteHandler(DiscordClient sender, MessageBulkDeleteEventArgs e) {
        if (deleteLogFor(e.Channel) is not { } log) {
            return;
        }

        // one entry for the whole lot, not a message per message
        var unlogged = e.Messages.Where(m => !purged.TryRemove(m.Id, out _)).ToList();
        await tryDiscord($"logging a bulk deletion in {e.Channel.Id}", () => logDeletedText(log, e.Channel, unlogged));

        foreach (var message in e.Messages) {
            try {
                actualMessageDeleteHandler(message, log);
            }
            catch (Exception ex) when (isExpected(ex)) {
                logger.warn($"Handling the bulk deletion of {message.Id}: {describe(ex)}");
            }
            catch (Exception ex) {
                await reportError($"handling the bulk deletion of {message.Id}", ex);
            }
        }
    }

    private static readonly HttpClient attachmentClient = new() {
        // backstop in case the CDN lies about (or omits) Content-Length; we check that too
        MaxResponseContentBufferSize = MAX_ARCHIVE_SIZE,
        Timeout = TimeSpan.FromSeconds(60)
    };

    internal const long MAX_ARCHIVE_SIZE = 8 * 1024 * 1024;

    /// discord takes at most 10 attachments per message
    internal const int FILES_PER_MESSAGE = 10;

    /// The slow parts of logging one deleted message: re-uploading its attachments, and for our own messages
    /// finding out who deleted them. Both run in the background.
    private static void actualMessageDeleteHandler(DiscordMessage message, DiscordChannel log) {
        var server = log.Guild!;

        if (message.Attachments is { Count: > 0 }) {
            // long wait so wrap it in task.run
            _ = Task.Run(() => archiveAttachments(message, log));
        }

        if (message.Author?.Id == client.CurrentUser.Id) {
            _ = Task.Run(async () => {
                try {
                    await Task.Delay(3000); // stupid discord doesnt update logs immediately
                    var logs = await server.GetAuditLogsAsync(10, actionType: AuditLogActionType.MessageDelete);
                    var deleter = logs.FirstOrDefault(l =>
                            l is DiscordMessageAuditLogEntry entry && entry.TargetMessageId == message.Id)?
                        .Actor?.Username ?? "unknown";
                    var content = string.IsNullOrEmpty(message.Content) ? "<no content>" : message.Content;
                    await log.SendMessageAsync($"{content} deleted by {deleter}");
                }
                catch (Exception e) when (isExpected(e)) {
                    // audit log unreadable / message vanished, nothing to report
                    logger.warn($"Couldn't work out who deleted {message.Id}: {describe(e)}");
                }
                catch (Exception e) {
                    await reportError($"reporting the deletion of {message.Id}", e);
                }
            });
        }
    }

    /// Re-upload a deleted message's attachments to its server's deletion log.
    /// NOTE: don't round-trip through disk. The response is buffered in memory anyway, so writing it out and reopening
    /// it only ever bought us file locks and name collisions.
    private static async Task archiveAttachments(DiscordMessage message, DiscordChannel log) {
        var files = new Dictionary<string, Stream>();
        try {
            foreach (var a in message.Attachments) {
                var data = await fetchAttachment(a, message.Id);
                if (data == null) {
                    continue;
                }

                // a spambot posting image.jpg five times in one message is very much a thing
                var name = string.IsNullOrEmpty(a.Filename) ? "attachment" : a.Filename;
                if (files.ContainsKey(name)) {
                    name = $"{Path.GetFileNameWithoutExtension(name)}-{a.Id}{Path.GetExtension(name)}";
                }

                files[name] = data;
            }

            if (files.Count == 0) {
                return;
            }

            // batch them: one REST call per batch instead of one per file, so a ten-image dump doesn't ratelimit us
            foreach (var batch in batchBySize(files)) {
                await tryDiscord($"archiving attachments of {message.Id}",
                    () => log.SendMessageAsync(new DiscordMessageBuilder().AddFiles(batch)));
            }
        }
        catch (Exception e) {
            await reportError($"archiving attachments of {message.Id}", e);
        }
        finally {
            foreach (var s in files.Values) {
                await s.DisposeAsync();
            }
        }
    }

    /// Pack files into messages bounded by both count and total size, so we never hand discord a payload it rejects.
    private static IEnumerable<Dictionary<string, Stream>> batchBySize(Dictionary<string, Stream> files) {
        var batch = new Dictionary<string, Stream>();
        long size = 0;
        foreach (var (name, stream) in files) {
            if (batch.Count > 0 && (batch.Count >= FILES_PER_MESSAGE || size + stream.Length > MAX_ARCHIVE_SIZE)) {
                yield return batch;
                batch = new Dictionary<string, Stream>();
                size = 0;
            }

            batch[name] = stream;
            size += stream.Length;
        }

        if (batch.Count > 0) {
            yield return batch;
        }
    }

    /// Pull an attachment off the CDN. Returns null if it's gone, too big, or the CDN told us to get lost.
    internal static async Task<Stream?> fetchAttachment(DiscordAttachment a, ulong messageId) {
        try {
            using var response = await attachmentClient.GetAsync(a.Url.ToString());
            if (!response.IsSuccessStatusCode) {
                // CDN link already dead (ban autodelete / purge), nothing to salvage here (coh2 sounds)
                logger.warn(
                    $"Attachment {a.Filename} of message {messageId} is unavailable ({(int)response.StatusCode})");
                return null;
            }

            var size = response.Content.Headers.ContentLength;
            if (size > MAX_ARCHIVE_SIZE) {
                logger.warn($"Attachment {a.Filename} of message {messageId} is {size} bytes, too big to re-upload");
                return null;
            }

            return new MemoryStream(await response.Content.ReadAsByteArrayAsync(), false);
        }
        catch (Exception e) when (isExpected(e)) {
            logger.warn($"Couldn't fetch attachment {a.Filename} of message {messageId}: {describe(e)}");
            return null;
        }
    }

    private static async Task messageDeleteHandler(DiscordClient sender, MessageDeleteEventArgs e) {
        try {
            if (deleteLogFor(e.Channel) is not { } log) {
                return;
            }

            if (!purged.TryRemove(e.Message.Id, out _)) {
                await logDeletedText(log, e.Channel, [e.Message]);
            }

            actualMessageDeleteHandler(e.Message, log);
        }
        catch (Exception ex) when (isExpected(ex)) {
            logger.warn($"Handling the deletion of {e.Message?.Id}: {describe(ex)}");
        }
        catch (Exception ex) {
            await reportError($"handling the deletion of {e.Message?.Id}", ex);
        }
    }

    private static async Task messageHandler(DiscordClient client, MessageCreateEventArgs e) {
        try {
            await actualMessageHandler(client, e);
        }
        catch (Exception ex) when (isExpected(ex)) {
            logger.warn($"Handling message {e.Message?.Id}: {describe(ex)}");
        }
        catch (Exception ex) {
            await reportError($"handling message {e.Message?.Id}", ex);
        }
    }

    private static async Task actualMessageHandler(DiscordClient client, MessageCreateEventArgs e) {
        if (!hasSetup) {
            return;
        }

        // a test bot doesn't moderate: the real one is doing that already, and two bots nagging about the same
        // crashlog is one too many
        if (Constants.profile != null) {
            return;
        }

        // gore protection
        if (e.Author is DiscordMember m && e.Guild is { } goreGuild &&
            (e.Message.Content.Contains("Screenshot_20230901_160903") ||
             e.Message.Attachments.Any(f => f.Url.ToString()!.Contains("Screenshot_20230901_160903")))) {
            // if the ban lands, the message is gone with it - don't carry on poking at it
            if (await tryDiscord($"banning {m.Id} for gore", () => goreGuild.BanMemberAsync(m, 6))) {
                return;
            }
        }

        if (client.CurrentUser.Id == e.Author.Id) {
            return;
        }

        // Don't reply to webhooks with embeds. The bot might have sent them
        if (e.Message.WebhookMessage && e.Message.Embeds.Count > 0) {
            return;
        }

        // Bug report reminder for new forum posts in the bug report category
        {
            var ch = e.Channel;
            bool inBugCategory = false;
            while (ch != null) {
                if (ch.Id == BUG_REPORT_CATEGORY) {
                    inBugCategory = true;
                    break;
                }
                // resolve through guild cache if possible
                ch = ch.Parent?.Id is { } pid && e.Guild?.Channels.TryGetValue(pid, out var cached) == true
                    ? cached
                    : ch.Parent;
            }
            if (inBugCategory) {
                var hasAttachment = e.Message.Attachments.Count > 0;
                var hasLogLink = e.Message.Content.Contains("pastebin", StringComparison.OrdinalIgnoreCase) ||
                                 e.Message.Content.Contains("paste.ee", StringComparison.OrdinalIgnoreCase) ||
                                 e.Message.Content.Contains("hastebin", StringComparison.OrdinalIgnoreCase) ||
                                 e.Message.Content.Contains("gist.github", StringComparison.OrdinalIgnoreCase);
                if (!hasAttachment && !hasLogLink) {
                    await tryDiscord("nagging about a crashlog", () => e.Message.RespondAsync(
                        "I'm not a mind-reader: can you please provide a crashlog (found in /reports), the system.log.txt and a detailed explanation of the crash's occurrence?"));
                }
            }
        }

        // Only run the spam filter on our server
        if (e.Guild?.Id != MY_GUILD) return;

        // Spam detection

        // base64 decode
        var word = new string(Encoding.UTF8.GetString(Convert.FromBase64String("bWZiem9iZQ==")).Select(c => char.IsLetter(c) ? (char)(c + (char.ToLower(c) < 'n' ? 13 : -13)) : c).ToArray());
        var word1 = new string(Encoding.UTF8.GetString(Convert.FromBase64String("Y2V2Zm1hbG54")).Select(c => char.IsLetter(c) ? (char)(c + (char.ToLower(c) < 'n' ? 13 : -13)) : c).ToArray());
        if (e.Message.Content.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            e.Message.Content.Contains(word1, StringComparison.OrdinalIgnoreCase)) {
            // routinely 404s: automod or a ban wave usually got there first
            await tryDiscord($"deleting spam message {e.Message.Id}", () => e.Message.DeleteAsync());
        }
    }

    private static async Task setup(DiscordClient client, LavalinkExtension lavalink,
        LavalinkConfiguration lavalinkConfig) {
        // Wait a bit with lavalink init, Lavalink seems to start slower than the bot. Lazy solution is pretty much a sleep
        await Task.Delay(2000);
        // NOTE: neither of these may take the rest of the bot down with it. No lavalink means no music, a missing
        // emoji means no cube in front of the replies - tickets and logs have to work regardless.
        try {
            LavalinkNode = await lavalink.ConnectAsync(lavalinkConfig);

            // dispose slsk client!!
            musicService?.slsk?.Dispose();

            musicService = new MusicService(lavalink, LavalinkNode);
            lavalinkInit = true;
        }
        catch (Exception e) {
            logger.error($"Couldn't connect to Lavalink, music is off: {describe(e)}");
        }

        imagesModule = new ImagesModule();
        wiltery = new WilteryHandler(Program.client);

        try {
            cube = await (await client.GetGuildAsync(838843082110664756)).GetEmojiAsync(839202645734457384);
        }
        catch (Exception e) {
            logger.warn($"Couldn't fetch the cube emoji: {describe(e)}");
        }

        hasSetup = true;

        // don't need to wait!
        _ = setupB(client, lavalink, lavalinkConfig);

        logger.info("Setup done!");
    }
    
    private static async Task forceReconnectLavalink(DiscordClient client, LavalinkExtension lavalink,
        LavalinkConfiguration lavalinkConfig) {
        if (!lavalinkInit) {
            logger.info("Lavalink not initialized yet, skipping forced reconnection attempt");
            return;
        }

        try {
            logger.info("Forcing Lavalink reconnection...");
            
            if (musicService != null) {
                logger.info("Disposing old MusicService...");
                musicService.slsk?.Dispose();
                // Clear all guild data to force garbage collection
                foreach (var guildData in musicService.GetExistingGuildData()) {
                    try {
                        await guildData.DestroyPlayerAsync();
                    } catch (Exception destroyEx) {
                        logger.warn($"Error destroying player for {guildData.Guild.Name}: {destroyEx}");
                    }
                }
                musicService = null;
            }
            
            // Properly dispose old connections and data to prevent memory leaks
            if (LavalinkNode != null) {
                logger.info("Destroying old Lavalink node...");
                try {
                    await LavalinkNode.DestroyAsync();
                    logger.info("Successfully destroyed old Lavalink node");
                } catch (Exception disposeEx) {
                    logger.warn($"Error destroying old Lavalink node: {disposeEx}");
                }
            }
            
            // Force garbage collection to clean up old instances
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Create fresh connection
            LavalinkNode = await lavalink.ConnectAsync(lavalinkConfig);
            musicService = new MusicService(lavalink, LavalinkNode);
            
            logger.info("Successfully force-reconnected to Lavalink!");
            
            await restoreAllPlaybackStates();
        }
        catch (Exception ex) {
            logger.error($"Failed to force-reconnect to Lavalink: {ex}");
            logger.error(ex);
        }
    }
    
    public static void markLavalinkReconnectionNeeded() {
        needsLavalinkReconnection = true;
        logger.info("Marked Lavalink reconnection as needed - will reconnect at end of Ready/Resumed event");
    }
    
    public static async Task lavalinkReconnect() {
        if (!lavalinkInit || musicService == null) {
            return;
        }
        
        lock (reconnectionLock) {
            // ignore if we attempted reconnection in the last 5 seconds (we're still doing it!!)
            if (DateTime.Now - lastReconnectionAttempt < TimeSpan.FromSeconds(5)) {
                logger.info("Skipping reconnection attempt - too recent");
                return;
            }
            lastReconnectionAttempt = DateTime.Now;
        }
        
        try {
            logger.info("Triggering Lavalink reconnection due to socket error...");
            
            var lavalinkConfig = new LavalinkConfiguration {
                Password = "youshallnotpass",
                RestEndpoint = new ConnectionEndpoint { Hostname = "127.0.0.1", Port = 2333 },
                SocketEndpoint = new ConnectionEndpoint { Hostname = "127.0.0.1", Port = 2333 }
            };
            
            var lavalink = client.GetLavalink();
            await forceReconnectLavalink(client, lavalink, lavalinkConfig);
        }
        catch (Exception ex) {
            logger.error($"Error during triggered Lavalink reconnection: {ex}");
        }
    }

    public static async Task saveAllPlaybackStates() {
        if (!lavalinkInit || musicService == null) {
            return;
        }

        lock (saveStateLock) {
            // Debounce: ignore if we attempted save in the last 5 seconds
            if (DateTime.Now - lastSaveAttempt < TimeSpan.FromSeconds(5)) {
                logger.info("Skipping save attempt - too recent");
                return;
            }
            lastSaveAttempt = DateTime.Now;
        }

        try {
            logger.info("Saving playback states for guilds with active music...");
            
            foreach (var musicData in musicService.GetExistingGuildData()) {
                if (musicData.Player is { IsConnected: true } && musicData.queue.NowPlaying != null) {
                    var state = new PlaybackState {
                        CurrentTrack = musicData.queue.NowPlaying,
                        Position = musicData.GetCurrentPosition(),
                        WasPlaying = musicData.Player.CurrentTrack != null && !musicData.Player.Player.Paused,
                        WasPaused = musicData.Player.Player.Paused,
                        VoiceChannelId = musicData.Player.Channel?.Id,
                        CommandChannelId = musicData.CommandChannel?.Id,
                        Queue = new List<Track>(musicData.queue.Queue),
                        AutoQueue = new List<Track>(musicData.queue.autoQueue),
                        History = new List<Track>(musicData.queue.history),
                        ArtistQueue = new List<string>(musicData.queue.artistQueue),
                        RepeatHolder = musicData.queue.repeatHolder,
                        RepeatQueue = musicData.queue.repeatQueue,
                        EarrapeMode = musicData.queue.earrapeMode,
                        Volume = musicData.volume,
                        EqEnabled = musicData.eq
                    };
                    
                    savedPlaybackStates[musicData.Guild.Id] = state;
                    logger.info($"Saved playback state for guild {musicData.Guild.Name}");
                    
                    // Properly disconnect from voice channel to prevent stuck states
                    try {
                        if (musicData.Player != null && musicData.Player.IsConnected) {
                            await musicData.Player.DisconnectAsync();
                            logger.info($"Disconnected from voice channel in {musicData.Guild.Name}");
                        }
                    }
                    catch (Exception disconnectEx) {
                        logger.warn($"Failed to disconnect from voice channel in {musicData.Guild.Name}: {disconnectEx}");
                    }
                }
            }
        }
        catch (Exception ex) {
            logger.error($"Error saving playback states: {ex}");
        }
    }
    
    private static async Task restoreAllPlaybackStates() {
        if (!lavalinkInit || musicService == null) {
            return;
        }

        lock (restoreStateLock) {
            // Debounce: ignore if we attempted restore in the last 5 seconds
            if (DateTime.Now - lastRestoreAttempt < TimeSpan.FromSeconds(5)) {
                logger.info("Skipping restore attempt - too recent");
                return;
            }
            lastRestoreAttempt = DateTime.Now;
        }

        try {
            logger.info("Restoring playback states for all guilds...");
            
            foreach (var (guildId, state) in savedPlaybackStates.ToList()) {
                try {
                    var guild = client.Guilds.Values.FirstOrDefault(g => g.Id == guildId);
                    if (guild == null) continue;

                    var musicData = await musicService.GetOrCreateDataAsync(guild);
                    
                    if (state.VoiceChannelId.HasValue && state.CurrentTrack != null) {
                        var voiceChannel = guild.GetChannel(state.VoiceChannelId.Value);
                        if (voiceChannel != null) {
                            // Ensure we're properly disconnected first
                            try {
                                if (musicData.Player != null && musicData.Player.IsConnected) {
                                    await musicData.Player.DisconnectAsync();
                                    logger.info($"Disconnected existing player from {guild.Name}");
                                }
                            }
                            catch (Exception disconnectEx) {
                                logger.warn($"Failed to disconnect existing player in {guild.Name}: {disconnectEx}");
                            }

                            // Create fresh player connection
                            await musicData.CreatePlayerAsync(voiceChannel);
                            logger.info($"Reconnected to voice channel in {guild.Name}");
                            
                            // Wait a moment for connection to stabilize
                            await Task.Delay(1000);
                            
                            musicData.queue.Queue.Clear();
                            musicData.queue.Queue.AddRange(state.Queue);
                            musicData.queue.autoQueue.Clear();
                            musicData.queue.autoQueue.AddRange(state.AutoQueue);
                            musicData.queue.history.Clear();
                            musicData.queue.history.AddRange(state.History);
                            musicData.queue.artistQueue.Clear();
                            musicData.queue.artistQueue.AddRange(state.ArtistQueue);
                            musicData.queue.repeatHolder = state.RepeatHolder;
                            musicData.queue.repeatQueue = state.RepeatQueue;
                            musicData.queue.earrapeMode = state.EarrapeMode;
                            
                            await musicData.SetVolumeAsync(state.Volume);
                            
                            if (state.EqEnabled != musicData.eq) {
                                musicData.toggleEQ();
                            }
                            
                            if (state.CommandChannelId.HasValue) {
                                musicData.CommandChannel = guild.GetChannel(state.CommandChannelId.Value);
                            }
                            
                            // Properly restore the current track through the queue system
                            musicData.queue.NowPlaying = state.CurrentTrack;
                            
                            if (musicData.Player != null && musicData.Player.IsConnected) {
                                if (state.WasPlaying && !state.WasPaused) {
                                    await musicData.Player.PlayAsync(state.CurrentTrack.track);
                                    await musicData.SeekAsync(state.Position, false);
                                    logger.info($"Resumed playback for guild {guild.Name} at {state.Position}");
                                } else if (state.WasPaused) {
                                    await musicData.Player.PlayAsync(state.CurrentTrack.track);
                                    await musicData.SeekAsync(state.Position, false);
                                    await musicData.PauseAsync();
                                    logger.info($"Restored paused state for guild {guild.Name} at {state.Position}");
                                }
                            } else {
                                logger.warn($"Cannot restore playback for guild {guild.Name} - player is null or disconnected");
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    logger.error($"Error restoring playback state for guild {guildId}: {ex}");
                }
            }
            
            savedPlaybackStates.Clear();
            logger.info("Finished restoring playback states");
        }
        catch (Exception ex) {
            logger.error($"Error during playback state restoration: {ex}");
        }
    }

    private static Task setupB(DiscordClient client, LavalinkExtension lavalink,
        LavalinkConfiguration lavalinkConfig) {
        foreach (var guild in client.Guilds) {
            logger.info($"{guild.Value.Name}, {guild.Value.JoinedAt.ToString()}");
        }
        return Task.CompletedTask;
    }

    private static async Task errorHandler(CommandsNextExtension sender, CommandErrorEventArgs e) {
        switch (e.Exception) {
            // wrong number of arguments
            case ArgumentException when e.Exception.Message.Contains("overload"): {
                var command = e.Command.Name;
                var suppliedArgumentsLength = e.Context.RawArgumentString.Split().Length;

                // if args are empty, we don't have arguments
                if (string.IsNullOrWhiteSpace(e.Context.RawArgumentString)) {
                    suppliedArgumentsLength = 0;
                }

                var minArgumentLength = int.MaxValue;
                var maxArgumentLength = 0;
                // too few arguments? loop through all overloads and check if we don't have enough
                foreach (var overload in e.Command.Overloads) {
                    // too few
                    minArgumentLength = Math.Min(overload.Arguments.Count, minArgumentLength);
                    // too many
                    maxArgumentLength = Math.Max(overload.Arguments.Count, maxArgumentLength);
                }

                // professional logging:tm:
                logger.debug(suppliedArgumentsLength);
                logger.debug(maxArgumentLength);
                logger.debug(minArgumentLength);

                if (suppliedArgumentsLength > maxArgumentLength) {
                    await sender.Client.SendMessageAsync(e.Context.Channel,
                        $"Too many arguments for command `{command}`!");
                    logger.warn(e.Exception);
                    return;
                }

                if (suppliedArgumentsLength < minArgumentLength) {
                    await sender.Client.SendMessageAsync(e.Context.Channel,
                        $"Too few arguments for command `{command}`!");
                    logger.warn(e.Exception);
                    return;
                }

                // if correct number of arguments but bad type; print info

                await sender.Client.SendMessageAsync(e.Context.Channel, $"Wrong parameters for command `{command}`!");
                logger.warn(e.Exception);
                return;
            }
            case CommandNotFoundException ex:
                if (e.Command is not null && !e.Command.Name.All(char.IsLetterOrDigit)) {
                    // ignore "command"...
                    return;
                }

                var closestCommand =
                    ActualFuzz.partialFuzzItem(ex.CommandName, e.Context.CommandsNext.RegisteredCommands.Keys);
                await sender.Client.SendMessageAsync(e.Context.Channel, new DiscordMessageBuilder().AddEmbed(
                    new DiscordEmbedBuilder().WithColor(DiscordColor.HotPink)
                        .WithDescription(
                            $"I have no bloody idea what that command is, sorry, did you mean {closestCommand}?")
                        //.WithImageUrl("https://c.tenor.com/CR9Or4gKoAUAAAAC/menhera-menhera-chan.gif").Build()));
                        .Build()));
                return;
            default:
                logger.warn(e.Exception);
                await sender.Client.SendMessageAsync(e.Context.Channel,
                    $"Exception occurred, details below:\n```{e.Exception}```");
                break;
        }
    }
}

public partial class CustomTimeSpanConverter : IArgumentConverter<TimeSpan> {
    private static Regex TimeSpanRegex { get; } =
        MyRegex();

    [GeneratedRegex(@"^(?<days>\d+d\s*)?(?<hours>\d{1,2}h\s*)?(?<minutes>\d{1,2}m\s*)?(?<seconds>\d{1,2}s\s*)?$",
        RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex MyRegex();

    public Task<Optional<TimeSpan>> ConvertAsync(string value, CommandContext? ctx) {
        if (value == "0") {
            return Task.FromResult(Optional.FromNullable(TimeSpan.Zero));
        }
        if (int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result1)) {
            return Task.FromResult(Optional<TimeSpan>.None);
        }
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result2)) {
            var _result2 = new TimeSpan(0, result2.Hours, result2.Minutes); // slash from h:m to m:s

            return Task.FromResult(Optional.FromNullable(_result2));
        }

        var strArray1 = new[] {
            "days",
            "hours",
            "minutes",
            "seconds"
        };
        var match = TimeSpanRegex.Match(value);
        if (!match.Success) {
            return Task.FromResult(Optional<TimeSpan>.None);
        }
        var days = 0;
        var hours = 0;
        var minutes = 0;
        var seconds = 0;
        for (result1 = 0; result1 < strArray1.Length; ++result1) {
            var groupname = strArray1[result1];
            var str = match.Groups[groupname].Value;
            if (!string.IsNullOrWhiteSpace(str)) {
                var ch = str[^1];
                int.TryParse(str.AsSpan(0, str.Length - 1), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var result3);
                switch (ch) {
                    case 'd':
                        days = result3;
                        continue;
                    case 'h':
                        hours = result3;
                        continue;
                    case 'm':
                        minutes = result3;
                        continue;
                    case 's':
                        seconds = result3;
                        continue;
                    default:
                        continue;
                }
            }
        }

        result2 = new TimeSpan(days, hours, minutes, seconds);
        return Task.FromResult(Optional.FromNullable(result2));
    }
}

public class PlaybackState {
    public Track? CurrentTrack;
    public TimeSpan Position;
    public bool WasPlaying;
    public bool WasPaused;
    public ulong? VoiceChannelId;
    public ulong? CommandChannelId;
    public List<Track> Queue = new();
    public List<Track> AutoQueue = new();
    public List<Track> History = new();
    public List<string> ArtistQueue = new();
    public Track? RepeatHolder;
    public bool RepeatQueue;
    public bool EarrapeMode;
    public int Volume = 100;
    public bool EqEnabled;
}