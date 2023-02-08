using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace EconomyBot.CommandHandlers; 

public class ChatHandler {
    public static async Task test2(DiscordGuild guild) {
        var messages = await guild.GetChannel(916804452193824809).GetMessagesAsync(1000);
        foreach (var message in messages.Reverse()) {
            await Console.Out.WriteLineAsync($"({message.Timestamp}) {message.Author}:{message.Content}");
        }
    }
}