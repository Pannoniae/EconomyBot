using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Attributes;

namespace EconomyBot;

public class BotModule : BaseCommandModule {

    [Command("gc"), Description("Clears the bot's memory.")]
    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
    public async Task GCAsync(CommandContext ctx) {
        #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        MemoryUtils.cleanGC();
    }
}