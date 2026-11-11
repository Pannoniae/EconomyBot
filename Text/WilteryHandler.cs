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

    //noop
    public WilteryHandler(DiscordClient client) {
        this.client = client;
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
}
