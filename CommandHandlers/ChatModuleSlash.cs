﻿using DSharpPlus.SlashCommands;
using EconomyBot.CommandHandlers;

namespace EconomyBot;

public class ChatModuleSlash : ApplicationCommandModule {

    [SlashCommand("purge", "")]
    public async Task purge(InteractionContext ctx, [Option("amt", "Amount")] long amt) {
        var messages = await ctx.Channel.GetMessagesAsync((int)amt);
        await ctx.Channel.DeleteMessagesAsync(messages);
        await ctx.CreateResponseAsync($"Deleted {amt} messages!");
    }

    [SlashCommand("test2", "")]
    public async Task test2(InteractionContext ctx) {
        await ChatHandler.test2(ctx.Guild);
    }
    
    [SlashCommand("roll", "")]
    public async Task roll(InteractionContext ctx) {
        await roll(ctx, 6);
    }

    [SlashCommand("roll", "")]
    public async Task roll(InteractionContext ctx, [Option("sides", "Sides")] long sides) {
        var num = new Random().Next(1, (int)(sides+1));
        await ctx.CreateResponseAsync($"You rolled {num}!");
    }
}