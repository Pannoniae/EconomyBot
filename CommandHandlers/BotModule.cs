using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Attributes;

namespace EconomyBot;

public class BotModule : BaseCommandModule {

    [Command("gc"), Description("Clears the bot's memory.")]
    public async Task GCAsync(CommandContext ctx) {
        MemoryUtils.cleanGC();
    }
}