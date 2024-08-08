﻿using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Enums;
using Newtonsoft.Json.Linq;

namespace EconomyBot;

public class ImagesModuleSlash : ApplicationCommandsModule {


    private readonly HttpClient client = new();

    private async Task CreateResponseAsync(InteractionContext ctx, string text) {
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(text));
    }


    [SlashCommand("img", "Fetch a wholesome image.")]
    public async Task img(InteractionContext ctx, [Option("reddit", "Ignore this option.")] string reddit = "no") {
        if (reddit == "reddit") {
            var imgProvider = new RedditImageProvider();
            var yuri = await imgProvider.getImageFromSub("yuri");
            if (yuri == "penis") {
                await CreateResponseAsync(ctx, "The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(ctx, yuri, "Cute girls!");
        }
        else {
            var imgProvider = new BooruImageProvider();
            var yuri = await imgProvider.getRandomYuri();
            if (yuri == "penis") {
                await CreateResponseAsync(ctx, "The bot is currently cuddling, sorry.^^");
                return;
            }

            await sendFancyEmbed(ctx, await imgProvider.getRandomYuri(), "Cute girls!");
        }
    }

    private async Task sendFancyEmbed(InteractionContext ctx, string url, string title) {
        // send the image
        await Console.Out.WriteLineAsync($"URL: {url}");
        var messageBuilder = new DiscordInteractionResponseBuilder().AddEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Rose).WithDescription(title).WithImageUrl(url));
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, messageBuilder);
    }
    
    [SlashCommand("randomxkcd", "Gets a random XKCD.")]
    public async Task xkcd(InteractionContext ctx) {
        
        int num;
        try {
            var latestComic = await client.GetAsync("https://xkcd.com/info.0.json");
            var latestComicJson = JObject.Parse(await latestComic.Content.ReadAsStringAsync());
            num = latestComicJson["num"].Value<int>();
        }
        catch (Exception e) {
            await CreateResponseAsync(ctx, "Failed to get XKCD.");
            await Console.Out.WriteLineAsync(e.ToString());
            return;
        }
        
        var randomXKCD = new Random().Next(num);

        string title;
        string url;
        try {
            var randomComic = await client.GetAsync($"https://xkcd.com/{randomXKCD}/info.0.json");
            var randomComicJson = JObject.Parse(await randomComic.Content.ReadAsStringAsync());
            title = randomComicJson["title"].Value<string>();
            url = randomComicJson["img"].Value<string>();
        }
        catch (Exception e) {
            await CreateResponseAsync(ctx, "Failed to get XKCD.");
            await Console.Out.WriteLineAsync(e.ToString());
            return;
        }


        var embed = new DiscordEmbedBuilder().WithTitle(title).WithColor(DiscordColor.Purple).WithImageUrl(url).WithDescription($"XKCD #{randomXKCD}");
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embed));
    }
    
    [SlashCommand("xkcd", "Gets a specific XKCD.")]
    public async Task xkcd(InteractionContext ctx, [Option("xkcd", "The XKCD to get.")] long number) {

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
            await CreateResponseAsync(ctx, "Failed to get XKCD.");
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
    
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed(embed));
    }
}