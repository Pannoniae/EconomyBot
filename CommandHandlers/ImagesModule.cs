using System.ComponentModel;
using System.Runtime.Serialization;
using NetCord.Rest;
using NetCord.Services.Commands;
using Newtonsoft.Json.Linq;
using Spectre.Console;

namespace EconomyBot;

public class ImagesModule : CommandModule<CommandContext> {
    private readonly HttpClient client = new();

    [Command("muv"), Description("Ghost's anime thing.")]
    public async Task muvluv(string luv) {
        if (luv == "luv") {
            // thank you ghost lol
            await Context.Channel!.TriggerTypingStateAsync();
            var imgProvider = new RedditImageProvider();
            await sendFancyEmbed(Context, await imgProvider.getImageFromSub("muvluv"), "Muv-Luv!");
        }
    }

    [Command("img"), Description("Fetch a wholesome image.")]
    public async Task img(string reddit = "no") {
        await Context.Channel!.TriggerTypingStateAsync();
        if (reddit == "reddit") {
            var imgProvider = new RedditImageProvider();
            var yuri = await imgProvider.getImageFromSub("yuri");
            if (yuri == "penis") {
                await ReplyAsync("The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(Context, yuri, "Cute girls!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            var yuri = await imgProvider.getRandomYuri();
            if (yuri == "penis") {
                await ReplyAsync("The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(Context, await imgProvider.getRandomYuri(), "Cute girls!");
        }
    }

    private async Task sendFancyEmbed(CommandContext ctx, string url, string title) {
        // send the image
        AnsiConsole.WriteLine($"URL: {url}");
        var messageProps = new ReplyMessageProperties().WithEmbeds([
            new EmbedProperties()
                .WithColor(DiscordColor.Rose).WithDescription(title).WithImage(url)
        ]);
        await ReplyAsync(messageProps);
    }

    [Command("b"), Description("I love you Msozod :3")]
    public async Task b() {
        var response = await client.GetAsync("https://en.wikipedia.org/api/rest_v1/page/random/summary");
        var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());

        var title = responseJson["title"].Value<string>();
        var img = responseJson["originalimage"]["source"].Value<string>() ?? null;
        var content = responseJson["extract"].Value<string>() ?? null;
        var url = responseJson["content_urls"]["desktop"]["page"].Value<string>();

        await ReplyAsync(new ReplyMessageProperties().WithEmbeds([
            new EmbedProperties {
                Title = title,
                Thumbnail = img,
                Color = DiscordColor.Rose,
                Description = content,
                Fields = [
                    new EmbedFieldProperties() {
                        Name = "Link:",
                        Value = url
                    }
                ]
            }
        ]));
    }

    [Command("xkcd"), Description("Gets a random XKCD.")]
    public async Task xkcd() {
        int num;
        try {
            var latestComic = await client.GetAsync("https://xkcd.com/info.0.json");
            var latestComicJson = JObject.Parse(await latestComic.Content.ReadAsStringAsync());
            num = latestComicJson["num"].Value<int>();
        }
        catch (Exception e) {
            await ReplyAsync("Failed to get XKCD.");
            AnsiConsole.WriteLine(e.ToString());
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
            await ReplyAsync("Failed to get XKCD.");
            AnsiConsole.WriteLine(e.ToString());
            return;
        }


        var embed = new ReplyMessageProperties().WithEmbeds([
            new EmbedProperties {
                Title = title,
                Color = DiscordColor.Purple,
                Url = Formatter.MaskedUrl($"XKCD #{randomXKCD}",
                          new Uri($"https://xkcd.com/{randomXKCD}"),
                          "Two little squirrels!")
                      + Environment.NewLine
                      + alt
            }
        ]);

        await ReplyAsync(embed);
    }

    [Command("xkcd"), Description("Gets a specific XKCD.")]
    public async Task xkcd(int number) {

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
            await ReplyAsync("Failed to get XKCD.");
            AnsiConsole.WriteLine(e.ToString());
            return;
        }


        var embed = new ReplyMessageProperties().WithEmbeds([new EmbedProperties {
            Title = title,
            Color = DiscordColor.Purple,
            Url = Formatter.MaskedUrl($"XKCD #{number}",
                      new Uri($"https://xkcd.com/{number}"),
                      "Two little squirrels!")
                  + Environment.NewLine
                  + alt
        }]);

        await ReplyAsync(embed);
    }
}