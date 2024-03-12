using DisCatSharp.Entities;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Enums;
using EconomyBot.CommandHandlers;

namespace EconomyBot;

public class ChatModuleSlash : ApplicationCommandsModule {

    private async Task CreateResponseAsync(InteractionContext ctx, string text) {
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().WithContent(text));
    }

    [SlashCommand("purge", "")]
    public async Task purge(InteractionContext ctx, [Option("amt", "Amount")] long amt) {
        var messages = await ctx.Channel.GetMessagesAsync((int)amt);
        var message = string.Join("\n", messages.Select(m => $"{m.Timestamp} {m.Author}: {m.Content}"));
        await ctx.Guild.GetChannel(Program.LOG).SendMessageAsync(message);
        await ctx.Channel.DeleteMessagesAsync(messages);
        await CreateResponseAsync(ctx, $"Deleted {amt} messages!");
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
        await CreateResponseAsync(ctx, $"You rolled {num}!");
    }
    
    [SlashCommand("squish", "Squish someone!")]
    public async Task squish(InteractionContext ctx, DiscordMember member) {
        var cat = "https://cdn.discordapp.com/attachments/1101712131222683659/1128320701456195594/image.png";
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);
        await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder()
            .WithContent($"{member.DisplayName} was squished by a giant kitten.").WithEmbed(
                new DiscordEmbedBuilder().WithImageUrl(
                    cat)));
    }
}