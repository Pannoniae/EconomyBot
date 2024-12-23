using System.ComponentModel;
using NetCord.Services.Commands;

namespace EconomyBot;

public class BotModule : CommandModule<CommandContext> {

    [Command("gc"), Description("Clears the bot's memory.")]
    public string GCAsync() {
        MemoryUtils.cleanGC();
        return "cleaned";
    }
}