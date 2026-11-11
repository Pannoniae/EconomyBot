using DisCatSharp;
using DisCatSharp.Entities;
using Newtonsoft.Json.Linq;

namespace EconomyBot;

/// The image commands themselves, shared by the text (ImagesModule) and slash (ImagesModuleSlash) stubs.
public static class ImagesCommands {
    private static readonly HttpClient client = new();

    public static async Task muvluv(Cmd c) {
        // thank you ghost lol
        await c.typing();
        var imgProvider = new RedditImageProvider();
        await sendFancyEmbed(c, await imgProvider.getImageFromSub("muvluv"), "Muv-Luv!");
    }

    public static async Task img(Cmd c, bool reddit) {
        await c.typing();
        if (reddit) {
            var imgProvider = new RedditImageProvider();
            var yuri = await imgProvider.getImageFromSub("yuri");
            if (yuri == "penis") {
                await c.respond("The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(c, yuri, "Cute girls!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            var yuri = await imgProvider.getRandomYuri();
            if (yuri == "penis") {
                await c.respond("The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(c, await imgProvider.getRandomYuri(), "Cute girls!");
        }
    }

    private static async Task sendFancyEmbed(Cmd c, string url, string title) {
        // send the image
        await Console.Out.WriteLineAsync($"URL: {url}");
        await c.respond(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Rose).WithDescription(title).WithImageUrl(url).Build());
    }

    public static async Task b(Cmd c) {
        var response = await client.GetAsync("https://en.wikipedia.org/api/rest_v1/page/random/summary");
        var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());

        string title;
        string? img;
        string? content;
        string url;
        try {
            title = responseJson["title"]?.Value<string>() ?? throw new NullReferenceException();
            img = responseJson["originalimage"]?["source"]?.Value<string>() ?? null;
            content = responseJson["extract"]?.Value<string>() ?? null;
            url = responseJson["content_urls"]?["desktop"]?["page"]?.Value<string>() ?? throw new NullReferenceException();
        }
        catch (Exception e) {
            await c.respond($"Something went wrong with fetching wikipedia, error: {e}");
            return;
        }

        await c.respond(new DiscordEmbedBuilder().WithTitle(title).WithThumbnail(img!)
            .WithColor(DiscordColor.Rose).WithDescription(content!).AddField(new DiscordEmbedField("Link:", url)).Build());
    }

    /// A specific XKCD, or a random one if no number is given.
    public static async Task xkcd(Cmd c, int? number) {
        if (number is not { } num) {
            try {
                var latestComic = await client.GetAsync("https://xkcd.com/info.0.json");
                var latestComicJson = JObject.Parse(await latestComic.Content.ReadAsStringAsync());
                var latest = latestComicJson["num"]?.Value<int>() ?? throw new NullReferenceException();
                // comics are numbered from 1
                num = new Random().Next(latest) + 1;
            }
            catch (Exception e) {
                await c.respond("Failed to get XKCD.");
                await Console.Out.WriteLineAsync(e.ToString());
                return;
            }
        }

        string title;
        string url;
        string alt;
        try {
            var comic = await client.GetAsync($"https://xkcd.com/{num}/info.0.json");
            var comicJson = JObject.Parse(await comic.Content.ReadAsStringAsync());
            title = comicJson["title"]?.Value<string>() ?? throw new NullReferenceException();
            url = comicJson["img"]?.Value<string>() ?? throw new NullReferenceException();
            alt = comicJson["alt"]?.Value<string>() ?? throw new NullReferenceException();
        }
        catch (Exception e) {
            await c.respond("Failed to get XKCD.");
            await Console.Out.WriteLineAsync(e.ToString());
            return;
        }

        var embed = new DiscordEmbedBuilder().WithTitle(title).WithColor(DiscordColor.Purple).WithImageUrl(url)
            .WithDescription(
                Formatter.MaskedUrl($"XKCD #{num}",
                    new Uri($"https://xkcd.com/{num}"),
                    "Two little squirrels!")
                + Environment.NewLine
                + alt);

        await c.respond(embed.Build());
    }
}
