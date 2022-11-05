using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace EconomyBot;

public class ImagesModule : BaseCommandModule {
    [Command("muv"), Description("Ghost's gay shit lol")]
    public async Task muvluv(CommandContext ctx, string luv) {
        if (luv == "luv") {
            // thank you ghost lol
            await ctx.TriggerTypingAsync();
            var imgProvider = new RedditImageProvider();
            await sendFancyEmbed(ctx, await imgProvider.getImageFromSub("muvluv"), "Muv-Luv!");
        }
    }

    [Command("yaoi"), Description("Fetch a yaoi image.")]
    public async Task yaoi(CommandContext ctx, string reddit = "no") {
        await ctx.TriggerTypingAsync();
        if (reddit == "reddit") {
            var imgProvider = new RedditImageProvider();
            await sendFancyEmbed(ctx, await imgProvider.getImageFromSub("yaoi"), "Cute boys!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            await sendFancyEmbed(ctx, await imgProvider.getRandomYaoi(), "Cute boys!");
        }
    }

    [Command("img"), Description("Fetch a wholesome image.")]
    public async Task img(CommandContext ctx, string reddit = "no") {
        await ctx.TriggerTypingAsync();
        if (reddit == "reddit") {
            var imgProvider = new RedditImageProvider();
            await sendFancyEmbed(ctx, await imgProvider.getImageFromSub("yuri"), "Cute girls!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            await sendFancyEmbed(ctx, await imgProvider.getRandomYuri(), "Cute girls!");
        }
    }

    private async Task sendFancyEmbed(CommandContext ctx, string url, string title) {
        // send the image
        var messageBuilder = new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Rose).WithDescription(title).WithImageUrl(url));
        await ctx.RespondAsync(messageBuilder);
    }
}