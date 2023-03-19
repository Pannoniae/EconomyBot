using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;

namespace EconomyBot; 

public class MusicCommon {

    public static MusicCommon instance;
    
    public static Dictionary<int, DiscordEmoji> NumberMappings { get; }
    public static Dictionary<DiscordEmoji, int> NumberMappingsReverse { get; }

    static MusicCommon() {
        instance = new MusicCommon();

        NumberMappings = new Dictionary<int, DiscordEmoji> {
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

    public async Task respond(CommandContext ctx, string response) {
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {response}");
    }
    
    public async Task modify(CommandContext ctx, DiscordMessage msg, string response) {
        await msg.ModifyAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {response}");
    }
}