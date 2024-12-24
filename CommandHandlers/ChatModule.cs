using EconomyBot.CommandHandlers;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.Commands;
using Spectre.Console;

namespace EconomyBot;

public class ChatModule : CommandModule<CommandContext> {
    public const ulong ZEROX = 1091089609234059316;

    private MusicService Music { get; set; }

    /// <summary>
    /// I know the name is bad, will refactor.
    /// </summary>
    public GuildMusicData GuildMusic { get; set; }

    public ChatModule() {
        Music = Program.musicService;
        GuildMusic = Music.GetOrCreateDataAsync(Context.Guild).GetAwaiter().GetResult();
    }

    private static IEnumerable<string> ChunksUpTo(string str, int maxChunkSize) {
        for (int i = 0; i < str.Length; i += maxChunkSize)
            yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
    }

    /// <summary>
    /// Retards abused it so it's manage messages-only. Thank you.
    /// </summary>
    /// <param name="amt">How many messages to purge.</param>
    [Command]
    [RequireUserPermissions<CommandContext>(Permissions.ManageMessages)]
    public async Task purge(int amt) {
        IAsyncEnumerable<RestMessage> messagesA;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // the library is stupid
        if (Context.Message.MessageReference != null) {
            AnsiConsole.WriteLine("Purging from given message!");
            messagesA = Context.Channel.GetMessagesAsync(new PaginationProperties<ulong>() {
                From = Context.Message.ReferencedMessage!.Id,
                Direction = PaginationDirection.Before,
                Limit = amt
            });
        }
        else {
            messagesA = Context.Channel.GetMessagesAsync(new PaginationProperties<ulong>() {
                Limit = amt
            });
        }
        List<RestMessage> messages = [];
        await foreach (var message in messagesA) {
            messages.Add(message);
        }

        if (Context.Channel.Id != Program.LOG) {
            var message = string.Join("\n",
                messages.Select(m => $"{m.CreatedAt} {m.Author.Username}#{m.Author.Discriminator}: {m.Content}")
                    .Reverse());
            var logMessages = ChunksUpTo(message, 1984);
            foreach (var msg in logMessages) {
                await Context.Client.Cache.Guilds[838843082110664756].Channels[Program.LOG]
                    .SendMessageAsync(msg);
            }
        }

        await Context.Channel.DeleteMessagesAsync(messages.Select(m => m.Id));
        await ReplyAsync($"Deleted {amt} messages!");
    }

    [Command]
    public async Task test2() {
        await ChatHandler.test2(Context.Guild);
    }

    [Command]
    public async Task roll() {
        await roll(6);
    }

    [Command]
    public async Task roll(int sides) {
        var num = new Random().Next(1, sides + 1);
        await ReplyAsync($"You rolled {num}!");
    }

    [Command]
    public async Task save() {
        var names = (await Context.Guild.GetUsersAsync().ToListAsync()).Select(member => member.ToString());
        await File.WriteAllLinesAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/names.txt",
            names);
    }

    [Command("bishop")]
    public async Task saveBishop() {
        var messages = (await Context.Channel.GetMessagesAsync(new PaginationProperties<ulong> {
                Limit = 2000
            }).ToListAsync())
            .Where(msg => msg.Author.Id == 540265036141297676)
            .Select(msg => msg.Content);
        await File.WriteAllLinesAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/bishop.txt",
            messages);
    }

    [Command("postbishop")]
    public async Task postBishop() {
        var messages =
            await File.ReadAllLinesAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/bishop.txt");
        foreach (var message in messages) {
            if (!string.IsNullOrWhiteSpace(message)) {
                await Context.Channel.SendMessageAsync(message);
            }
        }
    }

    [Command("webhook")]
    public async Task sendWebhook([CommandParameter(Remainder = true)] string message) {
        await Program.wiltery.sendWebhookToChannel((TextGuildChannel)Context.Channel, message);
    }

    [Command("user")]
    public async Task userWebhook([CommandParameter(Remainder = true)] string message) {
        await Program.wiltery.sendWebhookToChannelAsUser((TextGuildChannel)Context.Channel, message, (GuildUser)Context.User);
    }

    [Command("squish")]
    public async Task squish(GuildUser member) {
        var cat = "https://cdn.discordapp.com/attachments/1101712131222683659/1128320701456195594/image.png";
        await Context.Message.DeleteAsync();
        await Program.wiltery.sendWebhookToChannelWithCustomUser((TextGuildChannel)Context.Channel, new MessageProperties()
            .WithContent($"{member.Nickname ?? member.Username} was squished by a giant kitten.").WithEmbeds([
                new EmbedProperties {
                    Url = cat,
                    Title = "Giant cat",
                    Image = new EmbedImageProperties(cat),
                    Description = "Giant cat"
                }
            ]), cat, "Giant cat");
    }

    [Command("0x")]
    public async Task love0x() {
        var zerox = "https://tenor.com/view/girl-anime-kiss-anime-i-love-you-girl-kiss-gif-14375355";
        await ReplyAsync(new ReplyMessageProperties()
            .WithContent($"{Context.Guild.Users[ZEROX].Mention()} is amazing and I love them so much!"));
        await Context.Channel.SendMessageAsync(zerox);
    }

    [Command("panno")]
    public async Task lovepanno() {
        var panno = "https://tenor.com/view/hug-gif-25588769";
        await ReplyAsync(new ReplyMessageProperties()
            .WithContent($"Pannoniae is a 12/10 human being that deserves love and appreciation <3"));
        await Context.Channel.SendMessageAsync(panno);
    }
}