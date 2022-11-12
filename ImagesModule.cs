using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;

namespace EconomyBot;

public class ImagesModule : BaseCommandModule {
    [Command("muv"), Description("Ghost's anime thing.")]
    public async Task muvluv(CommandContext ctx, string luv) {
        if (luv == "luv") {
            // thank you ghost lol
            await ctx.TriggerTypingAsync();
            var imgProvider = new RedditImageProvider();
            await sendFancyEmbed(ctx, await imgProvider.getImageFromSub("muvluv"), "Muv-Luv!");
        }
    }
    
    // disabled
    //[Command("yaoi"), Description("Fetch a yaoi image.")]
    public async Task yaoi(CommandContext ctx, string reddit = "no") {
        await ctx.TriggerTypingAsync();
        if (reddit == "reddit") {
            var imgProvider = new RedditImageProvider();
            var yaoi = await imgProvider.getImageFromSub("yaoi");
            if (yaoi == "penis") {
                await ctx.RespondAsync("The bot is currently cuddling, sorry.^^");
                return;
            }
            await sendFancyEmbed(ctx, yaoi, "Cute boys!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            var yaoi = await imgProvider.getRandomYaoi();
            if (yaoi == "penis") {
                await ctx.RespondAsync("The bot is currently cuddling, sorry.^^");
                return;
            }
            await sendFancyEmbed(ctx, yaoi, "Cute boys!");
        }
    }

    [Command("img"), Description("Fetch a wholesome image.")]
    public async Task img(CommandContext ctx, string reddit = "no") {
        await ctx.TriggerTypingAsync();
        if (reddit == "reddit") {
            var imgProvider = new RedditImageProvider();
            var yuri = await imgProvider.getImageFromSub("yuri");
            if (yuri == "penis") {
                await ctx.RespondAsync("The bot is currently cuddling, sorry.^^");
                return;
            }
            await sendFancyEmbed(ctx, yuri, "Cute girls!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            var yuri = await imgProvider.getRandomYuri();
            if (yuri == "penis") {
                await ctx.RespondAsync("The bot is currently cuddling, sorry.^^");
                return;
            }
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