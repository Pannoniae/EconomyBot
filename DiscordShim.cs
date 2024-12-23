using NetCord;
using NetCord.Gateway;
using NetCord.Rest;

namespace EconomyBot;

public static class DiscordShim {

    public static GatewayClient client = Program.client;

    public static async Task<List<RestMessage>> getMessages(ulong eGuildId, ulong eChannelId, IReadOnlyList<ulong> eMessageIds) {
        var guild = client.Cache.Guilds[eGuildId];
        var channel = guild.Channels[eChannelId];
        var messages = new List<RestMessage>();
        foreach (var id in eMessageIds) {
            messages.Add(await client.Rest.GetMessageAsync(eChannelId, id));
        }
        return messages;
    }

    public static async Task<RestMessage> getMessage(ulong eGuildId, ulong eChannelId, ulong eMessageId) {
        return await client.Rest.GetMessageAsync(eChannelId, eMessageId);
    }

    public static IGuildChannel getChannel(ulong eGuildId, ulong eChannelId) {
        var guild = client.Cache.Guilds[eGuildId];
        return guild.Channels[eChannelId];
    }

    public static async Task sendMessage(IGuildChannel channel, string message) {
        await client.Rest.SendMessageAsync(channel.Id, message);
    }

    public static Task SendMessageAsync(this IGuildChannel channel, string message) {
        return sendMessage(channel, message);
    }

    public static async Task SendMessageAsync(this IGuildChannel channel, MessageProperties message) {
        await client.Rest.SendMessageAsync(channel.Id, message);
    }
}