using DSharpPlus.Entities;

namespace EconomyBot.CommandHandlers;

public class TicTacToe {

    public string stringState() 
    {
        string message;
        for (int y = 0; y < height; ++y) {
            message.Append(("-" * 13) + "\n");
            for (int x = 0; x < width; ++x) {
                switch (state[y * width + x])
                {
                    case 0: message.Append('-');
                    case 1: message.Append('X');
                    case 2: message.Append('O');
                }
                message.Append(" " * (x == 2) ? 0 : 9);
            }
            if (y != 2) message.Append('\n');
        }

        message.Append("-" * 13);

        return message;
    }

    public bool update(int row, int col, bool x)
    {
        if (row < 0 || row > height ||
            col < 0 || col > width) return false;

        if (state[row * width + col] == 0){
            state[row * width + col] = (x) ? 1 : 2;
            return true;
        }

        return false;
    }

    public int won() 
    {
        int vert = checkVertical();
        int horz = checkHorizontal();
        int diag = chechDiagonal();

        if (vert) return vert;
        if (horz) return horz;
        if (diag) return diag;

        return 0;
    }

    private int checkVertical()
    {
        for (int x = 0; x < height; ++x) {
            bool won = true;
            int type = 0;
            for (int y = 0; y < width; ++y) {

                if (type == 0) type = state[y * width + x];
                if (state[y * width + x] == 0 || state[y * width + x] != type) { 
                    won = false;
                    break;
                }
            }

            if (won) return type;
        }

        return 0;
    }

    private int checkHorizontal()
    {
        for (int y = 0; y < height; ++y) {
            bool won = true;
            int type = 0;
            for (int x = 0; x < width; ++x) {
                if (type == 0) type = state[y * width + x];
                if (state[y * width + x] == 0 || state[y * width + x] != type)
                {
                    won = false;
                    break;
                }
            }

            if (won) return type;
        }

        return 0;
    }

    private int checkDiag()
    {
        // because much easier
        if (state[0 * width + 0] == state[1 * width + 1] == state[2 * width + 2] && state[0 * width + 0] != 0)
            return state[0 * width + 0];
        if (state[0 * width + 2] == state[1 * width + 1] == state[2 * width + 0] && state[0 * width + 2])
            return state[0 * width + 2];
    }

    private const int width = 3;
    private const int height = 3;
    private fixed int state[width * height];
}

public class ChatHandler {
    public static async Task test2(DiscordGuild guild) {
        var messages = await guild.GetChannel(916804452193824809).GetMessagesAsync(1000).ToListAsync();
        foreach (var message in messages.Reverse<DiscordMessage>()) {
            await Console.Out.WriteLineAsync($"({message.Timestamp}) {message.Author}:{message.Content}");
        }
    }

    public static async Task starXO(DiscordGuild guild) {
        Console.Out.WriteLine("Tic Tac Toe started!");
        
    }

    public static async Task playXO(DiscordGuild guild) {

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
