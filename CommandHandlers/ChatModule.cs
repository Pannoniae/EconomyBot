using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using EconomyBot.CommandHandlers;

namespace EconomyBot;

public class ChatModule : BaseCommandModule {

    [Command]
    public async Task purge(CommandContext ctx, int amt) {
        var messages = await ctx.Channel.GetMessagesAsync(amt);
        await ctx.Channel.DeleteMessagesAsync(messages);
        await ctx.RespondAsync($"Deleted {amt} messages!");
    }

    [Command]
    public async Task test2(CommandContext ctx) {
        await ChatHandler.test2(ctx.Guild);
    }
    
    [Command]
    public async Task roll(CommandContext ctx) {
        await roll(ctx, 6);
    }

    [Command]
    public async Task roll(CommandContext ctx, int sides) {
        var num = new Random().Next(1, sides+1);
        await ctx.RespondAsync($"You rolled {num}!");
    }
}