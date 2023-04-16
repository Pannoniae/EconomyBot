using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using EconomyBot.CommandHandlers;

namespace EconomyBot;

[ModuleLifespan(ModuleLifespan.Singleton)]
public class ChatModule : BaseCommandModule {

    
    private MusicService Music { get; set; }
    
    /// <summary>
    /// I know the name is bad, will refactor.
    /// </summary>
    public GuildMusicData GuildMusic { get; set; }


    public ChatModule() {
        Music = Program.musicService;
    }

    public override async Task BeforeExecutionAsync(CommandContext ctx) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
        await base.BeforeExecutionAsync(ctx);

        //await GuildMusic.setupForChannel(ctx.Channel);
    }

    /// <summary>
    /// Retards abused it so it's manage messages-only. Thank you.
    /// </summary>
    /// <param name="amt">How many messages to purge.</param>
    [Command]
    [RequirePermissions(Permissions.ManageMessages)]
    public async Task purge(CommandContext ctx, int amt) {
        var messages = await ctx.Channel.GetMessagesAsync(amt);
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
        var names = (await ctx.Guild.GetAllMembersAsync()).Select(member => member.ToString());
        await File.WriteAllLinesAsync(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "/names.txt",
            names);
    }

    [Command("bishop")]
    public async Task saveBishop(CommandContext ctx) {
        var messages = (await ctx.Channel.GetMessagesAsync(2000)).Where(msg => msg.Author.Id == 540265036141297676)
            .Select(msg => msg.Content);
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
    public async Task sendWebhook(CommandContext ctx, string message) {
        await Program.wiltery.sendWebhookToChannel(ctx.Channel, message);
    }
    
    [Command("user")]
    public async Task userWebhook(CommandContext ctx, string message) {
        await Program.wiltery.sendWebhookToChannelAsUser(ctx.Channel, message, ctx.Member);
    }

    
}