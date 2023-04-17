﻿using DSharpPlus;
using DSharpPlus.Entities;

namespace EconomyBot;

/// <summary>
/// The DSharpPlus library is stupid, you can't get a webhook directly, only all webhooks for a channel.
/// So here we just cache all the webhooks, screw you.
/// </summary>
public class WebhookCache {
    private DiscordGuild Guild;

    private readonly Dictionary<DiscordChannel, DiscordWebhook> webhooks = new();

    public WebhookCache(DiscordGuild guild) {
        Guild = guild;
    }


    /// <summary>
    /// Setup webhook mappings from channel to webhook.
    /// </summary>
    public void setup() {
        var enumerable = Guild.Channels.AsParallel().Where(chn =>
            chn.Value.Type != ChannelType.Category &&
            chn.Value.Type != ChannelType.Voice && // not invalid channel
            chn.Value.Name != "admin" && // not admin
            (chn.Value.Parent == null || !chn.Value.Parent.Name.ToLower().Contains("archive"))); // not in archive
        Parallel.ForEachAsync(enumerable,
            async (chn, token) => await setupForChannel(chn.Value));
    }

    public async Task setupForChannel(DiscordChannel channel) {
        if (webhooks.TryGetValue(channel, out var w)) {
            return; // already initialised
        }
        var effectiveChannel = channel;
        if (channel.Type is ChannelType.PrivateThread or ChannelType.PublicThread) {
            effectiveChannel = channel.Parent;
        }

        await Console.Out.WriteLineAsync($"{effectiveChannel.Id}, {effectiveChannel.Name}");
        var webhooksForChannel = await effectiveChannel.GetWebhooksAsync();
        var ourWebhook = webhooksForChannel.FirstOrDefault(webhook => webhook.Name == "jazz");
        if (ourWebhook == null) {
            ourWebhook = await effectiveChannel.CreateWebhookAsync("jazz");
        }

        webhooks[channel] = ourWebhook;
    }

    public DiscordWebhook getWebhook(DiscordChannel channel) {
        return webhooks[channel];
    }
}