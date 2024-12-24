using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DetectLanguage;
using EconomyBot.Logging;
using Lavalink4NET;
using Lavalink4NET.NetCord;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.Commands;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.Commands;
using Soulseek;
using Spectre.Console;
using Directory = System.IO.Directory;

namespace EconomyBot;

class Program {
    private static CommandService<CommandContext> commands;
    private static IHost host;
    private static IServiceProvider services { get; set; }

    private static readonly Logger logger = Logger.getClassLogger("Main");

    public static IAudioService LavalinkNode;
    public static MusicService musicService;
    public static ImagesModule imagesModule;
    public static ToxicityHandler toxicity;
    public static WilteryHandler wiltery;

    public static DetectLanguageClient languageClient;

    public static GuildEmoji cube;

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


    public static async Task<int> Main(string[] args) {
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
            .ConfigureServices(collection => collection.AddSingleton(new YouTubeSearchProvider())
                .AddSingleton<IHostLifetime, ConsoleLifetime>());

        host = builder.Build()
            .AddModules(typeof(Program).Assembly)
            .UseGatewayEventHandlers();


        client = host.Services.GetService<GatewayClient>()!;
        commands = host.Services.GetService<CommandService<CommandContext>>()!;




        await Console.Out.WriteLineAsync("Intents:" + getIntents(client));

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
        client.Ready += async e => {
            await setup(client);
        };
        //discord.GuildDownloadCompleted += (sender, _) => setupB(sender, lavalink, lavalinkConfig);
        client.MessageDelete += messageDeleteHandler;
        client.InteractionCreate += interactionHandler;
        client.MessageCreate += messageInteractionHandler;

        client.Log += async message => {
            AnsiConsole.WriteLine(message.ToString());
        };




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
        return 0;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_intents")]
    private static extern ref GatewayIntents getIntents(GatewayClient client);

    private static async ValueTask messageInteractionHandler(Message m) {
        await InteractionHandler.messageHandler(m);
    }

    private static async ValueTask interactionHandler(Interaction i) {
        if (i is ButtonInteraction bi) {
            // TODO
            await InteractionHandler.buttonHandler(bi);
        }
    }

    private static async ValueTask messageDeleteHandler(MessageDeleteBulkEventArgs e) {
        if (e.GuildId == null) {
            return;
        }
        foreach (var message in await DiscordShim.getMessages(e.GuildId.Value, e.ChannelId, e.MessageIds)) {
            await actualMessageDeleteHandler(DiscordShim.getChannel(e.GuildId.Value, e.ChannelId), message);
        }
    }

    private static async Task actualMessageDeleteHandler(IGuildChannel channel, RestMessage message) {
        if (message.Attachments.Count != 0 && message.ChannelId != LOG) {
            // long wait so wrap it in task.run
            _ = Task.Run(async () => {
                var guid = Guid.NewGuid();
                foreach (var a in message.Attachments) {
                    var path = "";
                    try {
                        path = Directory.GetCurrentDirectory() + a.FileName;
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
                        await DiscordShim.sendMessage(channel, "Penis happened!");
                    }

                    var file = new FileStream(path, FileMode.Open);
                    await client.Cache.Guilds[838843082110664756].Channels[LOG]
                        .SendMessageAsync(new MessageProperties().WithAttachments([
                            new(a.FileName, file)
                        ]));
                }
            });
        }
    }

    private static async ValueTask messageDeleteHandler(MessageDeleteEventArgs e) {
        await actualMessageDeleteHandler(DiscordShim.getChannel(e.GuildId.Value, e.ChannelId), await DiscordShim.getMessage(e.GuildId.Value, e.ChannelId, e.MessageId));
    }

    private static async ValueTask messageHandler(Message message) {
        if (!hasSetup || message.GuildId == null) {
            return;
        }
        var guild = message.Guild!;

        // gore protection
        if (message.Content.Contains("Screenshot_20230901_160903") ||
            message.Attachments.Any(f => f.Url.Contains("Screenshot_20230901_160903"))) {
            await guild.BanUserAsync(message.Author.Id, 6);
        }

        // @everyone protection
        if (message.Content.Contains("@everyone") || message.Content.Contains("@here")) {
            await message.ReplyAsync("This server - and the world in general - would be better without your existence " + DiscordEmoji.FromName(client, ":pleading_face:"));
        }

        if (client.Cache.User.Id == message.Author.Id) {
            return;
        }

        // Don't reply to webhooks with embeds. The bot might have sent them
        if (message.WebhookId.HasValue && message.Embeds.Count > 0) {
            return;
        }
        var author = (message.Author as GuildUser)!;

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
        if ((message.Channel.Id != POLISH_CHANNEL && message.Channel.Id != ZOO && message.Channel.Id != HUNGARY_CHANNEL) && lizardry.Any(
                word =>
                    message.Content.Contains(word, StringComparison.OrdinalIgnoreCase))) {
            await message.AddReactionAsync(DiscordEmoji.FromName(client, ":lizard:"));
        }

        var cute = new List<string> {
            "skull",
            "cringe",
            "\ud83d\udc80" // skull emoji
        };

        // Hoholness

        var hohol = "hohol";
        if (message.Content.Contains(hohol, StringComparison.OrdinalIgnoreCase)) {
            await author.TimeOutAsync(DateTimeOffset.Now + TimeSpan.FromHours(1), new RestRequestProperties {
                AuditLogReason = "russian simp"
            });
        }

        // Lizardry
        if (cute.Any(word =>
                message.Content.Contains(word, StringComparison.OrdinalIgnoreCase))) {
            await message.ReplyAsync("You are a meanie >.<");
        }

        // Ukrainian language promotion handler, don't trigger if it's a quote
        if (message.Channel.Id == UKRAYINSKIJ_KANAL && !message.Content.Contains('"') && message.Content.Length > 10) {
            var results = await languageClient.DetectAsync(message.Content);
            bool isRussian = results.Any(r => r.language == "ru" && r.confidence > 1 && r.reliable);
            bool isNotUkrainian = results.All(r => r.language != "uk");
            logger.info($"Language analysis:");
            foreach (var result in results) {
                logger.info($"    {result.language}, {result.confidence}, {result.reliable}");
            }

            if (isRussian && isNotUkrainian) {
                await message.ReplyAsync("москальська свиня");
            }
        }

        // Toxicity handler
        if (!message.Content.StartsWith('.') && !message.Content.StartsWith('/') && message.Embeds.Count == 0 &&
            message.Attachments.Count == 0) {
            await toxicity.handleMessage(client, message);
            await wiltery.handleMessage(client, message);
        }

        if (author.Id == 947229156448538634) {
            await message.AddReactionAsync(DiscordEmoji.FromName(client, ":pinkpill:"));
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
            await e.Message.ReplyAsync("*meow*");
        }*/
    }

    private static async ValueTask setup(GatewayClient client) {
        // Wait a bit with lavalink init, Lavalink seems to start slower than the bot. Lazy solution is pretty much a sleep
        LavalinkNode = host.Services.GetService<IAudioService>()!;
        Console.Out.WriteLine(LavalinkNode);
        musicService = new MusicService(LavalinkNode);
        lavalinkInit = true;

        imagesModule = new ImagesModule();
        toxicity = new ToxicityHandler();
        wiltery = new WilteryHandler(Program.client);
        languageClient = new DetectLanguageClient(Constants.detectlanguagetoken);

        cube = await (await client.Rest.GetGuildAsync(838843082110664756)).GetEmojiAsync(839202645734457384);

        MusicService.slsk = new SoulseekClient();
        MusicService.slsk.ConnectAsync("jazzbot", "jazzbot");
        MusicService.slsk.ExcludedSearchPhrasesReceived += (sender, args) => {
            AnsiConsole.WriteLine("Excluded search phrases: ");
            foreach (var phrase in args) {
                AnsiConsole.WriteLine(phrase);
            }
        };

        hasSetup = true;

        // don't need to wait!
        _ = setupB(client);

        logger.info("Setup done!");
    }

    private static async ValueTask setupB(GatewayClient client) {
        foreach (var guild in client.Cache.Guilds) {
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