
using System.Collections.Immutable;
using DSharpPlus.Entities;

namespace EconomyBot.CommandHandlers; 

public class MusicCommon {

    public static MusicCommon instance;
    
    public static ImmutableDictionary<int, DiscordEmoji> NumberMappings { get; }

    public static ImmutableDictionary<DiscordEmoji, int> NumberMappingsReverse { get; }
    public static ImmutableArray<DiscordEmoji> Numbers { get; }

    static MusicCommon() {
        instance = new MusicCommon();
        var iab = ImmutableArray.CreateBuilder<DiscordEmoji>();
        iab.Add(DiscordEmoji.FromUnicode("1\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("2\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("3\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("4\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("5\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("6\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("7\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("8\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("9\u20e3"));
        iab.Add(DiscordEmoji.FromName(Program.client, ":keycap_ten:"));
        iab.Add(DiscordEmoji.FromUnicode("\u274c"));
        Numbers = iab.ToImmutable();

        var idb = ImmutableDictionary.CreateBuilder<int, DiscordEmoji>();
        idb.Add(1, DiscordEmoji.FromUnicode("1\u20e3"));
        idb.Add(2, DiscordEmoji.FromUnicode("2\u20e3"));
        idb.Add(3, DiscordEmoji.FromUnicode("3\u20e3"));
        idb.Add(4, DiscordEmoji.FromUnicode("4\u20e3"));
        idb.Add(5, DiscordEmoji.FromUnicode("5\u20e3"));
        idb.Add(6, DiscordEmoji.FromUnicode("6\u20e3"));
        idb.Add(7, DiscordEmoji.FromUnicode("7\u20e3"));
        idb.Add(8, DiscordEmoji.FromUnicode("8\u20e3"));
        idb.Add(9, DiscordEmoji.FromUnicode("9\u20e3"));
        idb.Add(10, DiscordEmoji.FromName(Program.client, ":keycap_ten:"));
        idb.Add(-1, DiscordEmoji.FromUnicode("\u274c"));
        NumberMappings = idb.ToImmutable();
        var idb2 = ImmutableDictionary.CreateBuilder<DiscordEmoji, int>();
        idb2.AddRange(NumberMappings.ToDictionary(x => x.Value, x => x.Key));
        NumberMappingsReverse = idb2.ToImmutable();
    }
}