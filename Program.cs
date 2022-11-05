using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Converters;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using DSharpPlus.Net;
using Emzi0767;
using Microsoft.Extensions.DependencyInjection;
using Optional = Emzi0767.Optional;

namespace EconomyBot;

static class Program {
    private static IServiceProvider services { get; set; }

    public static DiscordClient client;

    public static ulong LOG = 838920584879800343;

    static void Main(string[] args) {
        MainAsync().GetAwaiter().GetResult();
    }

    static async Task MainAsync() {
        Constants.init();
        var discord = new DiscordClient(new DiscordConfiguration {
            TokenType = TokenType.Bot,
            Token = Constants.token,
            Intents = DiscordIntents.All
        });
        client = discord;
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
            .AddTransient<SecureRandom>()
            .AddSingleton(new YouTubeSearchProvider())
            .BuildServiceProvider(true);
        var lavalink = discord.UseLavalink();
        var commands = discord.UseCommandsNext(new CommandsNextConfiguration {
            StringPrefixes = new[] { "." },
            Services = services
        });
        commands.CommandErrored += errorHandler;
        commands.RegisterCommands<EconomyModule>();
        commands.RegisterCommands<PartyModule>();
        commands.RegisterCommands<ChatModule>();
        commands.RegisterCommands<MusicModule>();
        commands.RegisterCommands<ImagesModule>();
        discord.UseInteractivity(new InteractivityConfiguration {
            Timeout = TimeSpan.FromSeconds(180),
            PollBehaviour = PollBehaviour.KeepEmojis
        });
        //discord.MessageCreated += messageHandler;
        discord.Ready += (sender, args) => setup(sender, args, lavalink, lavalinkConfig);
        discord.MessageDeleted += messageDeleteHandler;
        discord.GetCommandsNext().UnregisterConverter<TimeSpan>();
        discord.GetCommandsNext().RegisterConverter(new CustomTimeSpanConverter());
        await discord.ConnectAsync();
        // hold console window
        await Task.Delay(-1);
    }

    private static async Task messageDeleteHandler(DiscordClient sender, MessageDeleteEventArgs e) {
        if (e.Message.Attachments.Count != 0) {
            // long wait so wrap it in task.run
            Task.Run(async () => {
                var builder = new DiscordMessageBuilder();
                var guid = Guid.NewGuid();
                foreach (var a in e.Message.Attachments) {
                    var path = "";
                    try {
                        path = Directory.GetCurrentDirectory() + a.FileName;
                        var ext = Path.GetExtension(path);
                        path += guid + ext;
                        //slap the correct extension on it
                        new WebClient().DownloadFile(a.Url, path);
                    }
                    catch (WebException exception) {
                        Console.WriteLine(exception);
                        throw;
                    }
                    catch (Exception exception) {
                        await e.Channel.SendMessageAsync("Penis happened!");
                    }

                    var file = new FileStream(path, FileMode.Open);
                    builder = builder.WithFile(file);
                    await e.Guild.GetChannel(LOG).SendMessageAsync(builder);
                }

                return Task.CompletedTask;
            });
        }
    }

    private static async Task messageHandler(DiscordClient client, MessageCreateEventArgs e) {
        if (e.Message.Content.Contains("turkey", StringComparison.OrdinalIgnoreCase) && e.Author != client.CurrentUser) {
            
        }
    }

    private static async Task setup(DiscordClient client, ReadyEventArgs e, LavalinkExtension lavalink,
        LavalinkConfiguration lavalinkConfig) {
        //Constants.init();
        // dont do shit for the time being
        LavalinkNode = await lavalink.ConnectAsync(lavalinkConfig);
        musicService = new MusicService(new SecureRandom(), lavalink, LavalinkNode);
        imagesModule = new ImagesModule();
        //await MusicModule.setup(client);
        
        
        
        Console.WriteLine("Setup done!");
    }

    public static LavalinkNodeConnection LavalinkNode;
    public static MusicService musicService;
    public static ImagesModule imagesModule;

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
                Console.WriteLine(suppliedArgumentsLength);
                Console.WriteLine(maxArgumentLength);
                Console.WriteLine(minArgumentLength);

                if (suppliedArgumentsLength > maxArgumentLength) {
                    await sender.Client.SendMessageAsync(e.Context.Channel,
                        $"Too many arguments for command `{command}`!");
                    return;
                }

                if (suppliedArgumentsLength < minArgumentLength) {
                    await sender.Client.SendMessageAsync(e.Context.Channel,
                        $"Too few arguments for command `{command}`!");
                    return;
                }

                // if correct number of arguments but bad type; print info

                await sender.Client.SendMessageAsync(e.Context.Channel, $"Wrong parameters for command `{command}`!");
                return;
            }
            case CommandNotFoundException:
                await sender.Client.SendMessageAsync(e.Context.Channel, new DiscordMessageBuilder().WithEmbed(
                    new DiscordEmbedBuilder().WithColor(DiscordColor.HotPink).WithDescription("uwu")
                        .WithImageUrl("https://c.tenor.com/CR9Or4gKoAUAAAAC/menhera-menhera-chan.gif").Build()));
                return;
            default:
                Console.WriteLine(e.Exception);
                await sender.Client.SendMessageAsync(e.Context.Channel,
                    $"Exception occurred, details below:\n```{e.Exception}```");
                break;
        }
    }
}

public class CustomTimeSpanConverter : IArgumentConverter<TimeSpan> {
    
    private static Regex TimeSpanRegex { get; } = new("^(?<days>\\d+d\\s*)?(?<hours>\\d{1,2}h\\s*)?(?<minutes>\\d{1,2}m\\s*)?(?<seconds>\\d{1,2}s\\s*)?$", RegexOptions.Compiled | RegexOptions.ECMAScript);
    public Task<DSharpPlus.Entities.Optional<TimeSpan>> ConvertAsync(string value, CommandContext ctx) {
        if (value == "0")
            return Task.FromResult(DSharpPlus.Entities.Optional.FromValue(TimeSpan.Zero));
        if (int.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result1))
            return Task.FromResult(DSharpPlus.Entities.Optional.FromNoValue<TimeSpan>());
        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var result2)) {
            var _result2 = new TimeSpan(0, result2.Hours, result2.Minutes); // slash from h:m to m:s
            
            return Task.FromResult(DSharpPlus.Entities.Optional.FromValue(_result2));
        }

        var strArray1 = new[] {
            "days",
            "hours",
            "minutes",
            "seconds"
        };
        var match = TimeSpanRegex.Match(value);
        if (!match.Success)
            return Task.FromResult(DSharpPlus.Entities.Optional.FromNoValue<TimeSpan>());
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
        return Task.FromResult(DSharpPlus.Entities.Optional.FromValue(result2));
    }
}