using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using DisCatSharp;
using DisCatSharp.Entities;
using EconomyBot.Logging;
using Newtonsoft.Json.Linq;

namespace EconomyBot;

public class WilteryHandler {
    private static readonly Logger logger = Logger.getClassLogger("WilteryHandler");
    private MusicService? Music { get; set; }

    /// <summary>
    /// I know the name is bad, will refactor.
    /// </summary>
    public GuildMusicData? GuildMusic { get; set; }

    public DiscordClient client;
    public readonly HttpClient httpClient = new();

    private readonly List<MessageHandler> messageHandlers = [];

    //noop
    public WilteryHandler(DiscordClient client) {
        this.client = client;

        messageHandlers.Add(new WordExceptionMessageHandler("ball", DiscordEmoji.FromName(client, ":chestnut:"),
            "basket"));
        messageHandlers.Add(new WordMessageHandler("hrt", "hurt"));
        messageHandlers.Add(new ResponseWordMessageHandler("anal", "Have fun getting HIV"));
        messageHandlers.Add(new ExactWordMessageHandler("bro", DiscordEmoji.FromName(client, ":cat2:")));
        messageHandlers.Add(new ExactWordMessageHandler("bruv", DiscordEmoji.FromName(client, ":cat2:")));
        messageHandlers.Add(new ExactWordMessageHandler("brah", DiscordEmoji.FromName(client, ":cat2:")));
        messageHandlers.Add(new ExactWordMessageHandler("bra", DiscordEmoji.FromName(client, ":cat2:")));
        messageHandlers.Add(new ExactWordMessageHandler("bruh", DiscordEmoji.FromName(client, ":cat2:")));
        messageHandlers.Add(new ExactWordMessageHandler("dude", DiscordEmoji.FromName(client, ":zero:")));
        messageHandlers.Add(new ExactWordMessageHandler("sir", "gracious m'lady"));
        messageHandlers.Add(new ExactWordMessageHandler("bud", DiscordEmoji.FromName(client, ":deciduous_tree:")));
        messageHandlers.Add(new ExactWordMessageHandler("buddy", DiscordEmoji.FromName(client, ":deciduous_tree:")));

        // stop the stupidity
        // replace every gendered pronoun with neutral ones
        //messageHandlers.Add(new AIWordMessageHandler("hers "));
        //messageHandlers.Add(new AIWordMessageHandler("her "));
        //messageHandlers.Add(new AIWordMessageHandler("him "));
        //messageHandlers.Add(new AIWordMessageHandler("his "));
        //messageHandlers.Add(new AIWordMessageHandler("she "));
        //messageHandlers.Add(new AIWordMessageHandler("he "));
        //messageHandlers.Add(new AIWordMessageHandler("herself "));
        //messageHandlers.Add(new AIWordMessageHandler("himself "));
    }

    public async Task sendWebhookToChannel(DiscordChannel channel, string message) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(channel.Guild);
        var webhook = await GuildMusic.getWebhook(channel);
        // we are in a thread/forum
        if (channel.Id != webhook.ChannelId) {
            await webhook.ExecuteAsync(new DiscordWebhookBuilder {
                Content = message,
                ThreadName = channel.Name
            });
            return;
        }

        await webhook.ExecuteAsync(new DiscordWebhookBuilder {
            Content = message
        });
    }

    public async Task sendWebhookToChannelAsUser(DiscordChannel channel, string message, DiscordMember user) {
        await sendWebhookToChannelWithCustomUser(channel, new DiscordMessageBuilder().WithContent(message),
            user.GuildAvatarUrl, user.DisplayName);
    }

    public async Task sendWebhookToChannelWithCustomUser(DiscordChannel channel, DiscordMessageBuilder message,
        string avatarURL,
        string username) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(channel.Guild);
        var webhook = await GuildMusic.getWebhook(channel);
        // we are in a thread/forum
        if (channel.Id != webhook.ChannelId) {
            await webhook.ExecuteAsync(new DiscordWebhookBuilder {
                Content = message.Content,
                ThreadName = channel.Name,
                AvatarUrl = new Optional<string>(avatarURL),
                Username = new Optional<string>(username)
            }.AddEmbeds(message.Embeds ?? []));
            return;
        }

        await webhook.ExecuteAsync(new DiscordWebhookBuilder {
            Content = message.Content,
            AvatarUrl = new Optional<string>(avatarURL),
            Username = new Optional<string>(username)
        }.AddEmbeds(message.Embeds ?? []));
    }

    public async Task handleMessage(DiscordClient client, DiscordMessage message) {
        // ensure everything is set up
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(message.Channel.Guild);

        var textContent = message.Content;
        var processed = false;

        // process handlers
        foreach (var handler in messageHandlers) {
            if (handler.shouldProcess(message)) {
                textContent = await handler.process(this, textContent);
                await handler.effect(this, message);
                processed = true;
            }
        }

        // replace with final message
        if (processed) {
            await editMessage(message, message.Content, textContent);
        }
    }

    /// <summary>
    /// Transforms messages so they are neutral.
    /// </summary>
    public async Task replaceMessageAINeutral(DiscordMessage message) {
        string contents = message.Content;
        var channel = message.Channel;
        var user = (DiscordMember)message.Author;

        const string API_URL =
            "https://api.cloudflare.com/client/v4/accounts/2498ecc574e198ccf65813d2aa3af4ca/ai/run/";

        var h_json = $$"""
                       {
                         "prompt": "Replace all the gendered pronouns in the following sentence with neutral ones while making sure the sentence remains grammatically valid.. For example, replace 'she' with 'they'. Only output the modified sentence and nothing else. If the sentence is not gendered, output 'nothing'. Sentence: {{HttpUtility.JavaScriptStringEncode(message.Content)}}"
                       }
                       """;
        var httpRequestMessage = new HttpRequestMessage {
            Method = HttpMethod.Post,
            RequestUri = new Uri(API_URL + "@cf/meta/llama-3-8b-instruct"),
            Headers = {
                { nameof(HttpRequestHeader.Authorization), $"Bearer {Constants.apikey_cloudflareAI}" },
            },
            Content = new StringContent(h_json, Encoding.UTF8, "application/json")
        };
        var h_response = await httpClient.SendAsync(httpRequestMessage);
        var h_responseString = await h_response.Content.ReadAsStringAsync();
        JObject h_responseJson;
        try {
            h_responseJson = JObject.Parse(h_responseString);
        }
        catch {
            logger.error(h_responseString);
            return;
        }

        try {
            var success = h_responseJson["success"];
            // if not success, return
            if (success?.Value<bool>() == false) {
                logger.error(h_responseString);
                return;
            }
            // get the response from the json
            var response = h_responseJson["result"]?["response"]?.Value<string>();
            if (response == null) {
                logger.error(h_responseString);
                return;
            }
            // if nothing (bot detected neutral message), don't change
            if (response == "nothing") {
                return;
            }
            // send the response
            try {
                // yeet
                await message.DeleteAsync();
                string newMessage = response;
                await sendWebhookToChannelAsUser(channel, newMessage, user);
            }
            catch (Exception e) {
                logger.error(e);
            }

        }
        catch (Exception e) {
            logger.error(e);
        }
    }

    public async Task replaceMessage(DiscordMessage message, string from, string to) {
        string contents = message.Content;
        var channel = message.Channel;
        var user = (DiscordMember)message.Author;
        try {
            // yeet
            await message.DeleteAsync();
            var newMessage = contents.Replace(from, to, StringComparison.CurrentCultureIgnoreCase);
            await sendWebhookToChannelAsUser(channel, newMessage, user);
        }
        catch (Exception e) {
            logger.error(e);
        }
    }

    public async Task editMessage(DiscordMessage message, string from, string to) {
        var channel = message.Channel;
        var user = (DiscordMember)message.Author;
        try {
            // yeet
            await message.DeleteAsync();
            await sendWebhookToChannelAsUser(channel, to, user);
        }
        catch (Exception e) {
            logger.error(e);
        }
    }
}

public interface MessageHandler {

    /// <summary>
    /// Should process the given message?
    /// </summary>
    bool shouldProcess(DiscordMessage message);


    /// <summary>
    /// Takes the string and returns a new one.
    /// </summary>
    Task<string> process(WilteryHandler handler, string message);

    /// <summary>
    /// Executes a custom effect on the message. Doesn't have to be implemented, it does nothing by default. Used for side effects.
    /// </summary>
    Task effect(WilteryHandler handler, DiscordMessage message) {
        return Task.CompletedTask;
    }
}

public class WordMessageHandler(string target, string replacement) : MessageHandler {
    public virtual bool shouldProcess(DiscordMessage message) {
        return message.Content.Contains(target, StringComparison.CurrentCultureIgnoreCase);
    }

    public virtual Task<string> process(WilteryHandler handler, string message) {
        return Task.FromResult(message.Replace(target, replacement, StringComparison.CurrentCultureIgnoreCase));
    }

    public virtual Task effect(WilteryHandler handler, DiscordMessage message) {
        return Task.CompletedTask;
    }
}

public class ExactWordMessageHandler(string target, string replacement) : WordMessageHandler(target, replacement) {
    private readonly string target = target;
    private readonly string replacement = replacement;
    public override bool shouldProcess(DiscordMessage message) {
        return Regex.IsMatch(message.Content, $@"\b{target}\b", RegexOptions.IgnoreCase);

    }

    public override Task<string> process(WilteryHandler handler, string message) {
        return Task.FromResult(message.Replace(target, replacement, StringComparison.CurrentCultureIgnoreCase));
    }
}

public class AIWordMessageHandler(string target) : WordMessageHandler(target, "") {
    private readonly string target = target;
    public override bool shouldProcess(DiscordMessage message) {
        return message.Content.Contains(target, StringComparison.CurrentCultureIgnoreCase) && !message.Author.IsBot;
    }

    public override async Task effect(WilteryHandler handler, DiscordMessage message) {
        await handler.replaceMessageAINeutral(message);
    }

}

public class WordExceptionMessageHandler(string target, string replacement, params string[] exceptions) : MessageHandler {
    public bool shouldProcess(DiscordMessage message) {
        return message.Content.Contains(target, StringComparison.CurrentCultureIgnoreCase)
               && exceptions.All(e => !message.Content.Contains(e, StringComparison.CurrentCultureIgnoreCase));
    }

    public Task<string> process(WilteryHandler handler, string message) {
        return Task.FromResult(message.Replace(target, replacement, StringComparison.CurrentCultureIgnoreCase));
    }
}

public class ResponseWordMessageHandler(string target, string response) : WordMessageHandler(target, response) {
    private readonly string response = response;
    public override async Task effect(WilteryHandler handler, DiscordMessage message) {
        await message.RespondAsync(response);
    }
}