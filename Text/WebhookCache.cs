using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using Spectre.Console;

namespace EconomyBot;

/// <summary>
/// The DSharpPlus library is stupid, you can't get a webhook directly, only all webhooks for a channel.
/// So here we just cache all the webhooks, screw you.
/// </summary>
public class WebhookCache(Guild guild) {
    private readonly Dictionary<IGuildChannel, Webhook> webhooks = new();
    private List<Webhook> allWebhooks;


    /// <summary>
    /// Setup webhook mappings from channel to webhook.
    /// </summary>
    public async Task setup() {
        allWebhooks = (await guild.GetWebhooksAsync()).ToList();
        var enumerable = guild.Channels.AsParallel().Where(chn =>
            chn.Value.getChnType() != ChannelType.CategoryChannel &&
            chn.Value.getChnType() != ChannelType.VoiceGuildChannel && // not invalid channel
            chn.Value.Name != "admin" && // not admin
            (!chn.Value.getChnParent().HasValue || !DiscordShim.getChannel(guild.Id, chn.Value.getChnParent()!.Value).Name.Contains("archive", StringComparison.OrdinalIgnoreCase))); // not in archive
        await Parallel.ForEachAsync(enumerable,
            async (chn, token) => await setupForChannel(chn.Value));
        allWebhooks.Clear();
    }

    public async Task setupForChannel(IGuildChannel channel) {
        if (webhooks.TryGetValue(channel, out var w)) {
            return; // already initialised
        }
        var effectiveChannel = channel;
        if (channel.getChnType() is ChannelType.PrivateGuildThread or ChannelType.PublicGuildThread) {
            effectiveChannel = DiscordShim.getChannel(channel.GuildId, channel.getChnParent()!.Value);
        }

        AnsiConsole.WriteLine($"{effectiveChannel.Id}, {effectiveChannel.Name}");
        var webhooksForChannel = allWebhooks.Where(webhook => webhook.ChannelId == effectiveChannel.Id);
        var ourWebhook = webhooksForChannel.FirstOrDefault(webhook => webhook.Name == "jazz");
        if (ourWebhook == null) {
            ourWebhook = await Program.client.Rest.CreateWebhookAsync(effectiveChannel.Id, new WebhookProperties("jazz"));
        }

        webhooks[channel] = ourWebhook;
    }

    public Webhook getWebhook(IGuildChannel channel) {
        return webhooks[channel];
    }
}