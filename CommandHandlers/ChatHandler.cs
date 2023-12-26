using DSharpPlus.Entities;

namespace EconomyBot.CommandHandlers; 

public class ChatHandler {
    public static async Task test2(DiscordGuild guild) {
        var messages = await guild.GetChannel(916804452193824809).GetMessagesAsync(1000).ToListAsync();
        foreach (var message in messages.Reverse<DiscordMessage>()) {
            await Console.Out.WriteLineAsync($"({message.Timestamp}) {message.Author}:{message.Content}");
        }
    }
}

public static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> items,
        CancellationToken cancellationToken = default)
    {
        var results = new List<T>();
        await foreach (var item in items.WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
            results.Add(item);
        return results;
    }
}