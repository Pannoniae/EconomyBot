using NetCord.Gateway;
using NetCord.Services.Commands;

namespace EconomyBot;

public class MusicCommon {

    public static MusicCommon instance;

    public static Dictionary<int, string> NumberMappings { get; }
    public static Dictionary<string, int> NumberMappingsReverse { get; }

    static MusicCommon() {
        instance = new MusicCommon();

        NumberMappings = new Dictionary<int, string> {
            { 0, DiscordEmoji.FromUnicode("0\u20e3") },
            { 1, DiscordEmoji.FromUnicode("1\u20e3") },
            { 2, DiscordEmoji.FromUnicode("2\u20e3") },
            { 3, DiscordEmoji.FromUnicode("3\u20e3") },
            { 4, DiscordEmoji.FromUnicode("4\u20e3") },
            { 5, DiscordEmoji.FromUnicode("5\u20e3") },
            { 6, DiscordEmoji.FromUnicode("6\u20e3") },
            { 7, DiscordEmoji.FromUnicode("7\u20e3") },
            { 8, DiscordEmoji.FromUnicode("8\u20e3") },
            { 9, DiscordEmoji.FromUnicode("9\u20e3") },
            { 10, DiscordEmoji.FromName(Program.client, ":keycap_ten:") },
            { -1, DiscordEmoji.FromUnicode("\u274c") }
        };
        NumberMappingsReverse = NumberMappings.ToDictionary(x => x.Value, x => x.Key);
    }

    public static async Task respond(CommandContext ctx, string response) {
        await ctx.Message.ReplyAsync($"{Program.cube} {response}");
    }

    public static async Task modify(CommandContext ctx, Message msg, string response) {
        await msg.ModifyAsync(options => options.Content = $"{Program.cube} {response}");
    }
}