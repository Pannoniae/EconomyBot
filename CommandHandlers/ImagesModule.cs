using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Net.Serialization;
using Newtonsoft.Json.Linq;

namespace EconomyBot;

public class ImagesModule : BaseCommandModule {
    private readonly HttpClient client = new();

    [Command("muv"), Description("Ghost's anime thing.")]
    public async Task muvluv(CommandContext ctx, string luv) {
        if (luv == "luv") {
            // thank you ghost lol
            await ctx.TriggerTypingAsync();
            var imgProvider = new RedditImageProvider();
            await sendFancyEmbed(ctx, await imgProvider.getImageFromSub("muvluv"), "Muv-Luv!");
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
        Console.Out.WriteLine($"URL: {url}");
        var messageBuilder = new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Rose).WithDescription(title).WithImageUrl(url));
        await ctx.RespondAsync(messageBuilder);
    }

    [Command("b"), Description("I love you Msozod :3")]
    public async Task b(CommandContext ctx) {
        var response = await client.GetAsync("https://en.wikipedia.org/api/rest_v1/page/random/summary");
        var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());

        var title = responseJson["title"].Value<string>();
        var img = responseJson["originalimage"]["source"].Value<string>() ?? null;
        var content = responseJson["extract"].Value<string>() ?? null;
        var url = responseJson["content_urls"]["desktop"]["page"].Value<string>();

        await ctx.RespondAsync(new DiscordEmbedBuilder().WithTitle(title).WithThumbnail(img)
            .WithColor(DiscordColor.Rose).WithDescription(content).AddField("Link:", url).Build());
    }

    [Command("xkcd"), Description("Gets a random XKCD.")]
    public async Task xkcd(CommandContext ctx) {
        int num;
        try {
            var latestComic = await client.GetAsync("https://xkcd.com/info.0.json");
            var latestComicJson = JObject.Parse(await latestComic.Content.ReadAsStringAsync());
            num = latestComicJson["num"].Value<int>();
        }
        catch (Exception e) {
            await ctx.RespondAsync("Failed to get XKCD.");
            await Console.Out.WriteLineAsync(e.ToString());
            return;
        }

        var randomXKCD = new Random().Next(num);

        string title;
        string url;
        string alt;
        try {
            var randomComic = await client.GetAsync($"https://xkcd.com/{randomXKCD}/info.0.json");
            var randomComicJson = JObject.Parse(await randomComic.Content.ReadAsStringAsync());
            title = randomComicJson["title"].Value<string>();
            url = randomComicJson["img"].Value<string>();
            alt = randomComicJson["alt"].Value<string>();
        }
        catch (Exception e) {
            await ctx.RespondAsync("Failed to get XKCD.");
            await Console.Out.WriteLineAsync(e.ToString());
            return;
        } 


        var embed = new DiscordEmbedBuilder().WithTitle(title).WithColor(DiscordColor.Purple).WithImageUrl(url)
            .WithDescription(
                Formatter.MaskedUrl($"XKCD #{randomXKCD}",
                    new Uri($"https://xkcd.com/{randomXKCD}"),
                    "Two little squirrels!")
                + Environment.NewLine
                + alt);
    
        await ctx.RespondAsync(embed);
    }
    
    [Command("xkcd"), Description("Gets a specific XKCD.")]
    public async Task xkcd(CommandContext ctx, int number) {

        string title;
        string url;
        string alt;
        try {
            var randomComic = await client.GetAsync($"https://xkcd.com/{number}/info.0.json");
            var randomComicJson = JObject.Parse(await randomComic.Content.ReadAsStringAsync());
            title = randomComicJson["title"].Value<string>();
            url = randomComicJson["img"].Value<string>();
            alt = randomComicJson["alt"].Value<string>();
        }
        catch (Exception e) {
            await ctx.RespondAsync("Failed to get XKCD.");
            await Console.Out.WriteLineAsync(e.ToString());
            return;
        } 


        var embed = new DiscordEmbedBuilder().WithTitle(title).WithColor(DiscordColor.Purple).WithImageUrl(url)
            .WithDescription(
                Formatter.MaskedUrl($"XKCD #{number}",
                    new Uri($"https://xkcd.com/{number}"),
                    "Two little squirrels!")
                + Environment.NewLine
                + alt);
    
        await ctx.RespondAsync(embed);
    }
}