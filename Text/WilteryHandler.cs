using System.Net;
using System.Text;
using System.Web;
using EconomyBot.Logging;
using Lavalink4NET.Protocol;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Newtonsoft.Json.Linq;

namespace EconomyBot;

public class WilteryHandler {
    private static readonly Logger logger = Logger.getClassLogger("WilteryHandler");
    private MusicService Music { get; set; }

    /// <summary>
    /// I know the name is bad, will refactor.
    /// </summary>
    public GuildMusicData GuildMusic { get; set; }

    public GatewayClient client;
    public HttpClient httpClient = new();

    private readonly List<MessageHandler> messageHandlers = [];

    //noop
    public WilteryHandler(GatewayClient client) {
        this.client = client;

        messageHandlers.Add(new WordExceptionMessageHandler("ball", DiscordEmoji.FromName(client, ":chestnut:"),
            "basket"));
        messageHandlers.Add(new WordMessageHandler("hrt", "hurt"));
        messageHandlers.Add(new ResponseWordMessageHandler("anal", "Have fun getting HIV"));

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

    public async Task sendWebhookToChannel(IGuildChannel channel, string message) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(Program.client.Cache.Guilds[channel.GuildId]);
        var webhook = (IncomingWebhook)await GuildMusic.getWebhook(channel);
        // we are in a thread/forum
        if (channel.Id != webhook.ChannelId) {
            await webhook.ExecuteAsync(new WebhookMessageProperties {
                Content = message,
                ThreadName = channel.Name
            });
            return;
        }

        await webhook.ExecuteAsync(new WebhookMessageProperties {
            Content = message
        });
    }

    public async Task sendWebhookToChannelAsUser(IGuildChannel channel, string message, GuildUser user) {
        await sendWebhookToChannelWithCustomUser(channel, new MessageProperties().WithContent(message),
            user.GetGuildAvatarUrl().ToString(), user.Nickname ?? user.Username);
    }

    public async Task sendWebhookToChannelWithCustomUser(IGuildChannel channel, MessageProperties message,
        string avatarURL,
        string username) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(Program.client.Cache.Guilds[channel.GuildId]);
        var webhook = (IncomingWebhook)await GuildMusic.getWebhook(channel);
        // we are in a thread/forum
        if (channel.Id != webhook.ChannelId) {
            await webhook.ExecuteAsync(new WebhookMessageProperties {
                Content = message.Content,
                ThreadName = channel.Name,
                AvatarUrl = avatarURL,
                Username = username
            }.AddEmbeds(message.Embeds));
            return;
        }

        await webhook.ExecuteAsync(new WebhookMessageProperties {
            Content = message.Content,
            AvatarUrl = avatarURL,
            Username = username
        }.AddEmbeds(message.Embeds));
    }

    public async Task handleMessage(GatewayClient client, Message message) {
        // ensure everything is set up
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(message.Guild);


        // process handlers
        foreach (var handler in messageHandlers) {
            if (handler.shouldProcess(message)) {
                handler.process(this, message);
                // if processed, don't bother with the rest
                break;
            }
        }
    }

    /// <summary>
    /// Transforms messages so they are neutral.
    /// </summary>
    public async Task replaceMessageAINeutral(Message message) {
        string contents = message.Content;
        IGuildChannel channel = (IGuildChannel)message.Channel!;
        GuildUser user = (GuildUser)message.Author;

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
                { HttpRequestHeader.Authorization.ToString(), $"Bearer {Constants.apikey_cloudflareAI}" },
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

    public async Task replaceMessage(Message message, string from, string to) {
        string contents = message.Content;
        IGuildChannel channel = (IGuildChannel)message.Channel!;
        GuildUser user = (GuildUser)message.Author;
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
}

public interface MessageHandler {
    bool shouldProcess(Message message);

    void process(WilteryHandler handler, Message message);
}

public class WordMessageHandler(string target, string replacement) : MessageHandler {
    public virtual bool shouldProcess(Message message) {
        return message.Content.Contains(target, StringComparison.CurrentCultureIgnoreCase);
    }

    public virtual async void process(WilteryHandler handler, Message message) {
        await handler.replaceMessage(message, target, replacement);
    }
}

public class ExactWordMessageHandler(string target, string replacement) : WordMessageHandler(target, replacement) {
    private readonly string target = target;
    private readonly string replacement = replacement;
    public override bool shouldProcess(Message message) {
        return message.Content.Contains($"{target} ", StringComparison.CurrentCultureIgnoreCase);
    }

    public override async void process(WilteryHandler handler, Message message) {
        await handler.replaceMessage(message, target, replacement);
    }
}

public class AIWordMessageHandler(string target) : WordMessageHandler(target, "") {
    private readonly string target = target;
    public override bool shouldProcess(Message message) {
        return message.Content.Contains(target, StringComparison.CurrentCultureIgnoreCase) && !message.Author.IsBot;
    }

    public override async void process(WilteryHandler handler, Message message) {
        await handler.replaceMessageAINeutral(message);
    }
}

public class WordExceptionMessageHandler(string target, string replacement, params string[] exceptions) : MessageHandler {
    public virtual bool shouldProcess(Message message) {
        return message.Content.Contains(target, StringComparison.CurrentCultureIgnoreCase)
               && exceptions.All(e => !message.Content.Contains(e, StringComparison.CurrentCultureIgnoreCase));
    }

    public virtual async void process(WilteryHandler handler, Message message) {
        await handler.replaceMessage(message, target, replacement);
    }
}

public class ResponseWordMessageHandler(string target, string response) : WordMessageHandler(target, response) {
    public override async void process(WilteryHandler handler, Message message) {
        await message.ReplyAsync(response);
    }
}