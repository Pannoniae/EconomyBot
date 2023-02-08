using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace EconomyBot;

public class ImagesModuleSlash : ApplicationCommandModule {
    [SlashCommand("img", "Fetch a wholesome image.")]
    public async Task img(InteractionContext ctx, [Option("reddit", "Ignore this option.")] string reddit = "no") {
        if (reddit == "reddit") {
            var imgProvider = new RedditImageProvider();
            var yuri = await imgProvider.getImageFromSub("yuri");
            if (yuri == "penis") {
                await ctx.CreateResponseAsync("The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(ctx, yuri, "Cute girls!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            var yuri = await imgProvider.getRandomYuri();
            if (yuri == "penis") {
                await ctx.CreateResponseAsync("The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(ctx, await imgProvider.getRandomYuri(), "Cute girls!");
        }
    }

    private async Task sendFancyEmbed(InteractionContext ctx, string url, string title) {
        // send the image
        Console.Out.WriteLine($"URL: {url}");
        var messageBuilder = new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Rose).WithDescription(title).WithImageUrl(url));
        await ctx.CreateResponseAsync(messageBuilder);
    }
}