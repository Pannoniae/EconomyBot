using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DetectLanguage;
using EconomyBot.Logging;
using Lavalink4NET.NetCord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.Commands;
using NetCord.Services;
using NetCord.Services.Commands;
using Spectre.Console;

namespace EconomyBot;

class Program {
    private static CommandService<CommandContext> commands;
    private static IHost host;
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

    public static GatewayClient client;

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

        var builder = Host.CreateDefaultBuilder(args)
            .UseDiscordGateway(options => {
                options.Token = Constants.token;
                options.Intents = GatewayIntents.All;
            })
            .UseLavalink(options => {
                options.BaseAddress = new("http://localhost:2333/");
                options.Passphrase = "youshallnotpass";
            })
            .UseCommands<CommandContext>(options => {
                options.Prefix = ".";
                options.TypeReaders.Remove(typeof(TimeSpan));
                options.TypeReaders.Add(typeof(TimeSpan), new CustomTimeSpanConverter());
            })
            .ConfigureServices(collection => collection.AddSingleton(new YouTubeSearchProvider()));

        host = builder.Build()
            .AddModules(typeof(Program).Assembly)
            .UseGatewayEventHandlers();


        client = host.Services.GetService<GatewayClient>();
        commands = host.Services.GetService<CommandService<CommandContext>>();

        try {
            //ApplicationCommands.RegisterCommands<ChatModuleSlash>();
            //ApplicationCommands.RegisterGlobalCommands<MusicModuleSlash>();
            //ApplicationCommands.RegisterGlobalCommands<ImagesModuleSlash>();
            commands.AddModule<ChatModule>();
            commands.AddModule<MusicModule>();
            commands.AddModule<ImagesModule>();
            commands.AddModule<BotModule>();
        }
        catch (Exception e) {
            logger.error(e.Message);
        }
        client.MessageCreate += messageHandler;
        client.MessageDeleteBulk += messageDeleteHandler;
        client.Ready += async (sender, _) => await setup(client, lavalink, lavalinkConfig);
        //discord.GuildDownloadCompleted += (sender, _) => setupB(sender, lavalink, lavalinkConfig);
        client.MessageDelete += messageDeleteHandler;




        MemoryUtils.cleanGC();
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        _ = Task.Run(async () => {
            while (await timer.WaitForNextTickAsync()) {
                try {
                    MemoryUtils.cleanGC();
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
        });

        // hold console window
        await host.RunAsync();
        await Task.Delay(-1);
    }

    private static async ValueTask messageDeleteHandler(MessageDeleteBulkEventArgs e) {
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

    private static async Task messageDeleteHandler(GatewayClient sender, MessageDeleteEventArgs e) {
        await actualMessageDeleteHandler(e.Channel, e.Message);
    }

    private static async ValueTask messageHandler(Message message) {
        if (!hasSetup) {
            return;
        }

        // gore protection
        if (message.Content.Contains("Screenshot_20230901_160903") ||
            message.Attachments.Any(f => f.Url.Contains("Screenshot_20230901_160903"))) {
            await message.Guild.BanMemberAsync(e.Author as DiscordMember, 6);
        }

        client.

        // @everyone protection
        if (message.Content.Contains("@everyone") || e.Message.Content.Contains("@here")) {
            await message.RespondAsync("This server - and the world in general - would be better without your existence " + BotEmoji.FromName(client, ":pleading_face:"));
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

    private static async Task setup(GatewayClient client, LavalinkExtension lavalink,
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

        // don't need to wait!
        _ = setupB(client, lavalink, lavalinkConfig);

        logger.info("Setup done!");
    }

    private static async Task setupB(GatewayClient client, LavalinkExtension lavalink,
        LavalinkConfiguration lavalinkConfig) {
        foreach (var guild in client.Guilds) {
            logger.info($"{guild.Value.Name}, {guild.Value.JoinedAt.ToString()}");
        }
    }
}

public partial class CustomTimeSpanConverter : CommandTypeReader<CommandContext> {
    private static Regex TimeSpanRegex { get; } =
        MyRegex();

    [GeneratedRegex(@"^(?<days>\d+d\s*)?(?<hours>\d{1,2}h\s*)?(?<minutes>\d{1,2}m\s*)?(?<seconds>\d{1,2}s\s*)?$",
        RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex MyRegex();

    public async override ValueTask<TypeReaderResult> ReadAsync(ReadOnlyMemory<char> input, CommandContext context, CommandParameter<CommandContext> parameter, CommandServiceConfiguration<CommandContext> configuration, IServiceProvider? serviceProvider) {
        var value = input.ToString();
        if (value == "0") {
            return TypeReaderResult.Success(TimeSpan.Zero);
        }
        if (int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result1))
            return TypeReaderResult.Fail("TimeSpan got a number?");
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result2)) {
            var _result2 = new TimeSpan(0, result2.Hours, result2.Minutes); // slash from h:m to m:s

            return TypeReaderResult.Success(_result2);
        }

        var strArray1 = new[] {
            "days",
            "hours",
            "minutes",
            "seconds"
        };
        var match = TimeSpanRegex.Match(value);
        if (!match.Success)
            return TypeReaderResult.ParseFail(parameter.Name);
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
        return TypeReaderResult.Success(result2);
    }
}