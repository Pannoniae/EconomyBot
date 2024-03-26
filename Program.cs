using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DetectLanguage;
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
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace EconomyBot;

class Program {
    private static IServiceProvider services { get; set; }

    private static readonly Logger logger = Logger.getClassLogger("Main");

    public static LavalinkSession LavalinkNode;
    public static MusicService musicService;
    public static ImagesModule imagesModule;
    public static ToxicityHandler toxicity;
    public static WilteryHandler wiltery;

    public static DetectLanguageClient languageClient;

    public static DiscordEmoji cube;

    public static bool lavalinkInit = false;
    public static bool hasSetup = false;

    public static DiscordClient client;

    public const ulong LOG = 838920584879800343;
    public static ulong HALLOFFAME = 1078991955633127474;
    public const ulong UKRAYINSKIJ_KANAL = 1153439320435335278;
    public const ulong POLISH_CHANNEL = 1156695554462597120;
    public const ulong ZOO = 1149817703922675823;
    public const ulong HUNGARY_CHANNEL = 1154903997510062181;


    // shut up compiler
    private Program() {
        Main(null!);
    }


    public static async Task Main(string[] args) {
        // logging

        Logger.setLogLevel(LogLevel.INFO);

        Constants.init();

        var discord = new DiscordClient(new DiscordConfiguration {
            TokenType = TokenType.Bot,
            Token = Constants.token,
            Intents = DiscordIntents.All
        });
        client = discord;

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
        services = new ServiceCollection()
            .AddSingleton(new YouTubeSearchProvider())
            .BuildServiceProvider(true);
        var lavalink = discord.UseLavalink();
        var commands = discord.UseCommandsNext(new CommandsNextConfiguration {
            StringPrefixes = new List<string>(new[] {
                "."
            }),
            ServiceProvider = services
        });
        //var ApplicationCommands = discord.UseApplicationCommands(new ApplicationCommandsConfiguration {
        //    ServiceProvider = services
        //});
        try {
            //ApplicationCommands.RegisterCommands<ChatModuleSlash>();
            //ApplicationCommands.RegisterGlobalCommands<MusicModuleSlash>();
            //ApplicationCommands.RegisterGlobalCommands<ImagesModuleSlash>();
            commands.CommandErrored += errorHandler;
            commands.RegisterCommands<ChatModule>();
            commands.RegisterCommands<MusicModule>();
            commands.RegisterCommands<ImagesModule>();
        }
        catch (Exception e) {
            if (e is BadRequestException ex) {
                logger.error(ex.JsonMessage);
            }

            logger.error(e.Message);
        }

        discord.UseInteractivity(new InteractivityConfiguration {
            Timeout = TimeSpan.FromSeconds(180),
            PollBehaviour = PollBehaviour.KeepEmojis
        });
        discord.MessageCreated += messageHandler;
        discord.MessagesBulkDeleted += messageDeleteHandler;
        discord.Ready += async (sender, _) => await setup(sender, lavalink, lavalinkConfig);
        //discord.GuildDownloadCompleted += (sender, _) => setupB(sender, lavalink, lavalinkConfig);
        discord.MessageDeleted += messageDeleteHandler;
        discord.GetCommandsNext().UnregisterConverter<TimeSpan>();
        discord.GetCommandsNext().RegisterConverter(new CustomTimeSpanConverter());
        await discord.ConnectAsync();
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        while (await timer.WaitForNextTickAsync()) {
            try {
                foreach (var file in Directory.GetParent(Directory.GetCurrentDirectory())!.EnumerateFiles()) {
                    file.Delete();
                }

                logger.info("Pruned cached images.");
            }
            catch (Exception e) {
                // file is in use, ignore
                AnsiConsole.WriteException(e);
            }
        }

        // hold console window
        await Task.Delay(-1);
    }

    private static async Task messageDeleteHandler(DiscordClient sender, MessageBulkDeleteEventArgs e) {
        foreach (var message in e.Messages) {
            await actualMessageDeleteHandler(e.Channel, message);
        }
    }

    private static async Task actualMessageDeleteHandler(DiscordChannel channel, DiscordMessage message) {
        if (message.Attachments.Count != 0 && message.Channel.Id != LOG) {
            // long wait so wrap it in task.run
            _ = Task.Run(async () => {
                var guid = Guid.NewGuid();
                foreach (var a in message.Attachments) {
                    var path = "";
                    try {
                        path = Directory.GetCurrentDirectory() + a.Filename;
                        var ext = Path.GetExtension(path);
                        path += guid + ext;
                        //slap the correct extension on it
                        new WebClient().DownloadFile(a.Url, path);
                    }
                    catch (WebException exception) {
                        logger.warn(exception);
                        throw;
                    }
                    catch (Exception) {
                        await channel.SendMessageAsync("Penis happened!");
                    }

                    var file = new FileStream(path, FileMode.Open);
                    await (await client.GetGuildAsync(838843082110664756)).GetChannel(LOG)
                        .SendMessageAsync(new DiscordMessageBuilder().WithFile(file));
                }
            });
        }

        if (message.Author == client.CurrentUser && message.Channel.Id != LOG) {
            var server = await client.GetGuildAsync(838843082110664756);
            _ = Task.Run(async () => {
                await Task.Delay(3000); // stupid discord doesnt update logs immediately
                var logs = await server.GetAuditLogsAsync(10, actionType: AuditLogActionType.MessageDelete);
                var deleter = logs.FirstOrDefault(log =>
                        log is DiscordAuditLogMessageEntry entry && entry.Target.Id == message.Id)?
                    .UserResponsible?.Username ?? "unknown";
                await server.GetChannel(LOG)
                    .SendMessageAsync($"{message.Content} deleted by {deleter}");
            });
        }
    }

    private static async Task messageDeleteHandler(DiscordClient sender, MessageDeleteEventArgs e) {
        await actualMessageDeleteHandler(e.Channel, e.Message);
    }

    private static async Task messageHandler(DiscordClient client, MessageCreateEventArgs e) {
        if (!hasSetup) {
            return;
        }

        // gore protection
        if (e.Message.Content.Contains("Screenshot_20230901_160903") ||
            e.Message.Attachments.Any(f => f.Url.Contains("Screenshot_20230901_160903"))) {
            await e.Guild.BanMemberAsync(e.Author as DiscordMember, 6);
        }

        // @everyone protection
        if (e.Message.Content.Contains("@everyone") || e.Message.Content.Contains("@here")) {
            await e.Message.RespondAsync("This server - and the world in general - would be better without your existence " + DiscordEmoji.FromName(client, ":pleading_face:"));
        }

        if (client.CurrentUser.Id == e.Author.Id) {
            return;
        }

        // Don't reply to webhooks with embeds. The bot might have sent them
        if (e.Message.WebhookMessage && e.Message.Embeds.Count > 0) {
            return;
        }

        // Funny replacement handling
        // todo

        var lizardry = new List<string> {
            "ą",
            "Ą",
            "ć",
            "Ć",
            "ę",
            "Ę",
            "ł",
            "Ł",
            "ń",
            "Ń",
            "ó",
            "Ó",
            "ś",
            "Ś",
            "ź",
            "Ź",
            "Ś",
            "ż",
            "Ż"
        };

        // Cringe
        if ((e.Channel.Id != POLISH_CHANNEL && e.Channel.Id != ZOO && e.Channel.Id != HUNGARY_CHANNEL) && lizardry.Any(
                word =>
                    e.Message.Content.Contains(word, StringComparison.OrdinalIgnoreCase))) {
            await e.Message.CreateReactionAsync(DiscordEmoji.FromName(client, ":lizard:"));
        }

        var cute = new List<string> {
            "skull",
            "cringe",
            "\ud83d\udc80" // skull emoji
        };

        // Hoholness

        var hohol = "hohol";
        if (e.Message.Content.Contains(hohol, StringComparison.OrdinalIgnoreCase)) {
            await ((DiscordMember)e.Author).TimeoutAsync(DateTimeOffset.Now + TimeSpan.FromHours(1), "russian simp");
        }

        // Lizardry
        if (cute.Any(word =>
                e.Message.Content.Contains(word, StringComparison.OrdinalIgnoreCase))) {
            await e.Message.RespondAsync("You are a meanie >.<");
        }

        // Ukrainian language promotion handler, don't trigger if it's a quote
        if (e.Channel.Id == UKRAYINSKIJ_KANAL && !e.Message.Content.Contains('"') && e.Message.Content.Length > 10) {
            var results = await languageClient.DetectAsync(e.Message.Content);
            bool isRussian = results.Any(r => r.language == "ru" && r.confidence > 1 && r.reliable);
            bool isNotUkrainian = results.All(r => r.language != "uk");
            logger.info($"Language analysis:");
            foreach (var result in results) {
                logger.info($"    {result.language}, {result.confidence}, {result.reliable}");
            }

            if (isRussian && isNotUkrainian) {
                await e.Message.RespondAsync("москальська свиня");
            }
        }

        // Toxicity handler
        if (!e.Message.Content.StartsWith('.') && !e.Message.Content.StartsWith('/') && e.Message.Embeds.Count == 0 &&
            e.Message.Attachments.Count == 0) {
            await toxicity.handleMessage(client, e.Message);
            await wiltery.handleMessage(client, e.Message);
        }

        if (e.Author.Id == 947229156448538634) {
            await e.Message.CreateReactionAsync(DiscordEmoji.FromName(client, ":pinkpill:"));
        }

        var meowList = new List<string> {
            "cat",
            "kitty",
            "kitten",
            "meow",
            "purr",
            "feline",
            "nya",
            "miau"
        };
        // Meowing is too common
        /*if (meowList.Any(word =>
                e.Message.Content.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                e.Message.Attachments.Any(a => a.Url.Contains(word, StringComparison.OrdinalIgnoreCase)))) {
            await e.Message.RespondAsync("*meow*");
        }*/
    }

    private static async Task setup(DiscordClient client, LavalinkExtension lavalink,
        LavalinkConfiguration lavalinkConfig) {
        // Wait a bit with lavalink init, Lavalink seems to start slower than the bot. Lazy solution is pretty much a sleep
        await Task.Delay(3000);
        LavalinkNode = await lavalink.ConnectAsync(lavalinkConfig);
        musicService = new MusicService(lavalink, LavalinkNode);
        lavalinkInit = true;
        imagesModule = new ImagesModule();
        toxicity = new ToxicityHandler();
        wiltery = new WilteryHandler(Program.client);
        languageClient = new DetectLanguageClient(Constants.detectlanguagetoken);

        cube = await (await client.GetGuildAsync(838843082110664756)).GetEmojiAsync(839202645734457384);

        hasSetup = true;

        logger.info("Setup done!");
    }

    private static async Task setupB(DiscordClient client, LavalinkExtension lavalink,
        LavalinkConfiguration lavalinkConfig) {
        foreach (var guild in client.Guilds) {
            logger.debug($"{guild.Value.Name}, {guild.Value.JoinedAt.ToString()}");
        }
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
                await sender.Client.SendMessageAsync(e.Context.Channel, new DiscordMessageBuilder().WithEmbed(
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
    static private Regex TimeSpanRegex { get; } =
        MyRegex();

    [GeneratedRegex(@"^(?<days>\d+d\s*)?(?<hours>\d{1,2}h\s*)?(?<minutes>\d{1,2}m\s*)?(?<seconds>\d{1,2}s\s*)?$",
        RegexOptions.Compiled | RegexOptions.ECMAScript)]
    static private partial Regex MyRegex();

    public Task<Optional<TimeSpan>> ConvertAsync(string value, CommandContext ctx) {
        if (value == "0")
            return Task.FromResult(Optional.FromNullable(TimeSpan.Zero));
        if (int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result1))
            return Task.FromResult(Optional<TimeSpan>.None);
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
        if (!match.Success)
            return Task.FromResult(Optional<TimeSpan>.None);
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