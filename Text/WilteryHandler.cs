using DSharpPlus;
using DSharpPlus.Entities;
using EconomyBot.Logging;

namespace EconomyBot;

public class WilteryHandler {
    private static readonly Logger logger = Logger.getClassLogger("WilteryHandler");
    private MusicService Music { get; set; }

    /// <summary>
    /// I know the name is bad, will refactor.
    /// </summary>
    public GuildMusicData GuildMusic { get; set; }

    public DiscordClient client;

    private List<MessageHandler> messageHandlers = new();

    //noop
    public WilteryHandler(DiscordClient client) {
        this.client = client;

        messageHandlers.Add(new WordMessageHandler("ball", DiscordEmoji.FromName(client, ":chestnut:")));
        messageHandlers.Add(new ResponseWordMessageHandler("anal", "Have fun getting HIV"));
    }

    public async Task sendWebhookToChannel(DiscordChannel channel, string message) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(channel.Guild);
        var webhook = await GuildMusic.getWebhook(channel);
        // we are in a thread/forum
        if (channel.Id != webhook.ChannelId) {
            await webhook.ExecuteAsync(new DiscordWebhookBuilder {
                Content = message,
                ThreadId = channel.Id
            });
            return;
        }

        await webhook.ExecuteAsync(new DiscordWebhookBuilder {
            Content = message
        });
    }

    public async Task sendWebhookToChannelAsUser(DiscordChannel channel, string message, DiscordMember user) {
        await sendWebhookToChannelWithCustomUser(channel, new DiscordMessageBuilder().WithContent(message), user.GetGuildAvatarUrl(ImageFormat.Auto), user.DisplayName);
    }

    public async Task sendWebhookToChannelWithCustomUser(DiscordChannel channel, DiscordMessageBuilder message, string avatarURL,
        string username) {
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(channel.Guild);
        var webhook = await GuildMusic.getWebhook(channel);
        // we are in a thread/forum
        if (channel.Id != webhook.ChannelId) {
            await webhook.ExecuteAsync(new DiscordWebhookBuilder {
                Content = message.Content,
                ThreadId = channel.Id,
                AvatarUrl = new Optional<string>(avatarURL),
                Username = new Optional<string>(username)
            }.AddEmbeds(message.Embeds));
            return;
        }

        await webhook.ExecuteAsync(new DiscordWebhookBuilder {
            Content = message.Content,
            AvatarUrl = new Optional<string>(avatarURL),
            Username = new Optional<string>(username)
        }.AddEmbeds(message.Embeds));
    }

    public async Task handleMessage(DiscordClient client, DiscordMessage message) {
        // ensure everything is set up
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(message.Channel.Guild);


        // process handlers
        foreach (var handler in messageHandlers) {
            if (handler.shouldProcess(message)) {
                handler.process(this, message);
            }
        }
    }

    public async Task replaceMessage(DiscordMessage message, string from, string to) {
        string contents = message.Content;
        DiscordChannel channel = message.Channel;
        DiscordMember user = (DiscordMember)message.Author;
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
    bool shouldProcess(DiscordMessage message);

    void process(WilteryHandler handler, DiscordMessage message);
}

public class WordMessageHandler : MessageHandler {
    private string target;
    private string replacement;


    public WordMessageHandler(string target, string replacement) {
        this.target = target;
        this.replacement = replacement;
    }

    public virtual bool shouldProcess(DiscordMessage message) {
        return message.Content.Contains(target, StringComparison.CurrentCultureIgnoreCase);
    }

    public virtual async void process(WilteryHandler handler, DiscordMessage message) {
        await handler.replaceMessage(message, target, replacement);
    }
}

public class ResponseWordMessageHandler : WordMessageHandler {
    private string target;
    private string response;


    public ResponseWordMessageHandler(string target, string response) : base(target, response) {
        this.target = target;
        this.response = response;
    }

    public override async void process(WilteryHandler handler, DiscordMessage message) {
        await message.RespondAsync(response);
    }
}