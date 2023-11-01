using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using EconomyBot.CommandHandlers;

namespace EconomyBot;

[ModuleLifespan(ModuleLifespan.Singleton)]
public class ChatModule : BaseCommandModule {

    public const ulong ZEROX = 1091089609234059316;

    private MusicService Music { get; set; }

    /// <summary>
    /// I know the name is bad, will refactor.
    /// </summary>
    public GuildMusicData GuildMusic { get; set; }
    public override async Task BeforeExecutionAsync(CommandContext ctx) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
    }

    static IEnumerable<string> ChunksUpTo(string str, int maxChunkSize) {
        for (int i = 0; i < str.Length; i += maxChunkSize)
            yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
    }

    /// <summary>
    /// Retards abused it so it's manage messages-only. Thank you.
    /// </summary>
    /// <param name="amt">How many messages to purge.</param>
    [Command]
    [RequirePermissions(Permissions.ManageMessages)]
    public async Task purge(CommandContext ctx, int amt) {
        var messages = new List<DiscordMessage>();
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        // the library is stupid
        if (ctx.Message.Reference != null) {
            await Console.Out.WriteLineAsync("Purging from given message!");
            await foreach (var message in ctx.Channel.GetMessagesBeforeAsync(ctx.Message.Reference.Message.Id, amt)) {
                messages.Add(message);
            }
        }
        else {
            await foreach (var message in ctx.Channel.GetMessagesAsync(amt)) {
                messages.Add(message);
            }
        }

        if (ctx.Channel.Id != Program.LOG) {
            var message = string.Join("\n",
                messages.Select(m => $"{m.Timestamp} {m.Author.Username}#{m.Author.Discriminator}: {m.Content}").Reverse());
            var logMessages = ChunksUpTo(message, 1984);
            foreach (var msg in logMessages) {
                await (await ctx.Client.GetGuildAsync(838843082110664756)).GetChannel(Program.LOG)
                    .SendMessageAsync(msg);
            }
        }

        await ctx.Channel.DeleteMessagesAsync(messages);
        await ctx.RespondAsync($"Deleted {amt} messages!");
    }

    [Command]
    public async Task test2(CommandContext ctx) {
        await ChatHandler.test2(ctx.Guild);
    }

    [Command]
    public async Task roll(CommandContext ctx) {
        await roll(ctx, 6);
    }

    [Command]
    public async Task roll(CommandContext ctx, int sides) {
        var num = new Random().Next(1, sides + 1);
        await ctx.RespondAsync($"You rolled {num}!");
    }

    [Command]
    public async Task save(CommandContext ctx) {
        var names = await ctx.Guild.GetAllMembersAsync().Select(member => member.ToString()).ToListAsync();
        await File.WriteAllLinesAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/names.txt",
            names);
    }

    [Command("bishop")]
    public async Task saveBishop(CommandContext ctx) {
        var messages = await ctx.Channel.GetMessagesAsync(2000).Where(msg => msg.Author.Id == 540265036141297676)
            .Select(msg => msg.Content).ToListAsync();
        await File.WriteAllLinesAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/bishop.txt",
            messages);
    }

    [Command("postbishop")]
    public async Task postBishop(CommandContext ctx) {
        var messages =
            await File.ReadAllLinesAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/bishop.txt");
        foreach (var message in messages) {
            if (!string.IsNullOrWhiteSpace(message)) {
                await ctx.Channel.SendMessageAsync(message);
            }
        }
    }

    [Command("webhook")]
    public async Task sendWebhook(CommandContext ctx, [RemainingText] string message) {
        await Program.wiltery.sendWebhookToChannel(ctx.Channel, message);
    }

    [Command("user")]
    public async Task userWebhook(CommandContext ctx, [RemainingText] string message) {
        await Program.wiltery.sendWebhookToChannelAsUser(ctx.Channel, message, ctx.Member);
    }

    [Command("squish")]
    public async Task squish(CommandContext ctx, DiscordMember member) {
        var cat = "https://cdn.discordapp.com/attachments/1101712131222683659/1128320701456195594/image.png";
        await ctx.Message.DeleteAsync();
        await Program.wiltery.sendWebhookToChannelWithCustomUser(ctx.Channel, new DiscordMessageBuilder()
            .WithContent($"{member.DisplayName} was squished by a giant kitten.").WithEmbed(
                new DiscordEmbedBuilder().WithImageUrl(
                    cat)), cat, "Giant cat");
    }
    
    [Command("0x")]
    public async Task love0x(CommandContext ctx) {
        var zerox = "https://tenor.com/view/girl-anime-kiss-anime-i-love-you-girl-kiss-gif-14375355";
        await ctx.RespondAsync(new DiscordMessageBuilder()
            .WithContent($"{(await ctx.Guild.GetMemberAsync(ZEROX)).Mention} is amazing and I love them so much!"));
        await ctx.Channel.SendMessageAsync(zerox);
    }
    
    [Command("panno")]
    public async Task lovepanno(CommandContext ctx) {
        var panno = "https://tenor.com/view/hug-gif-25588769";
        await ctx.RespondAsync(new DiscordMessageBuilder()
            .WithContent($"Pannoniae is a 12/10 human being that deserves love and appreciation <3"));
        await ctx.Channel.SendMessageAsync(panno);
    }
}