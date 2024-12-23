using NetCord.Gateway;
using NetCord.Rest;

namespace EconomyBot.CommandHandlers;

public class ChatHandler {
    public static async Task test2(Guild guild) {
        var messages = Program.client.Rest.GetMessagesAsync(916804452193824809, new PaginationProperties<ulong> {
            Limit = 1000
        });
        var messagesList = await messages.ToListAsync();
        foreach (var message in messagesList.Reverse<RestMessage>()) {
            AnsiConsole.WriteLine($"({message.CreatedAt}) {message.Author}:{message.Content}");
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