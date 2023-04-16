using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Extensions.Options;
using Optional = DSharpPlus.Entities.Optional;

namespace EconomyBot; 

public class WilteryHandler {
    
    private MusicService Music { get; set; }
    
    /// <summary>
    /// I know the name is bad, will refactor.
    /// </summary>
    public GuildMusicData GuildMusic { get; set; }
    
    //noop
    public WilteryHandler() {
        
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
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(channel.Guild);
        var webhook = await GuildMusic.getWebhook(channel);
        // we are in a thread/forum
        if (channel.Id != webhook.ChannelId) {
            await webhook.ExecuteAsync(new DiscordWebhookBuilder {
                Content = message,
                ThreadId = channel.Id,
                AvatarUrl = new Optional<string>(user.GetGuildAvatarUrl(ImageFormat.Auto)),
                Username = new Optional<string>(user.DisplayName)
            });
            return;
        }

        await webhook.ExecuteAsync(new DiscordWebhookBuilder {
            Content = message,
            AvatarUrl = new Optional<string>(user.GetGuildAvatarUrl(ImageFormat.Auto)),
            Username = new Optional<string>(user.DisplayName)
        });
    }

    public async Task handleMessage(DiscordClient client, DiscordMessage message) {
        // ensure everything is set up
        Music = Program.musicService;
        GuildMusic = await Music.GetOrCreateDataAsync(message.Channel.Guild);
        
        if (message.Content.Contains("ball")) {
            await replaceMessage(message, "ball", DiscordEmoji.FromName(client, ":chestnut:"));
        }
    }

    private async Task replaceMessage(DiscordMessage message, string from, string to) {
        string contents = message.Content;
        DiscordChannel channel = message.Channel;
        DiscordMember user = (DiscordMember)message.Author;
        // yeet
        await message.DeleteAsync();
        var newMessage = contents.Replace(from, to, StringComparison.CurrentCultureIgnoreCase);
        await sendWebhookToChannelAsUser(channel, newMessage, user);
    }
}