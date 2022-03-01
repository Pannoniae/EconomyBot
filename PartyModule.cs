using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Extensions;
using Emzi0767;

namespace EconomyBot;

[Group("party"), Description("Commands for manipulating a party.")]
public class PartyModule : BaseCommandModule {
    public static readonly DiscordEmbedBuilder defaultBuilder =
        new DiscordEmbedBuilder().WithColor(DiscordColor.Aquamarine);

    [Command("create"), Description("Makes a new political party and channels for it.")]
    public async Task MakeParty(CommandContext ctx) {
        InteractivityExtension inter = ctx.Client.GetInteractivity();
        await ctx.Channel.SendMessageAsync(
            new DiscordMessageBuilder().WithEmbed(defaultBuilder.WithDescription("Enter your party's short name.")));
        var response = await inter.WaitForMessageAsync(message => message.Content.All(c => c.IsBasicLetter()));
        if (response.TimedOut) {
            return;
        }

        var shortName = response.Result.Content;

        await ctx.Channel.SendMessageAsync(
            new DiscordMessageBuilder().WithEmbed(defaultBuilder.WithDescription("Enter your party's full name.")));
        var response2 = await inter.WaitForMessageAsync(message => message.MessageType == MessageType.Default && message.Author == ctx.Member);
        if (response2.TimedOut) {
            return;
        }

        var fullName = response2.Result.Content;

        await ctx.Channel.SendMessageAsync(
            new DiscordMessageBuilder().WithEmbed(defaultBuilder.WithDescription("Enter your party's colour in hex.")));
        var response3 = await inter.WaitForMessageAsync(message => message.MessageType == MessageType.Default && message.Author == ctx.Member);
        if (response3.TimedOut) {
            return;
        }

        var colour = new DiscordColor(response3.Result.Content);

        await ctx.Channel.SendMessageAsync(
            new DiscordMessageBuilder().WithEmbed(defaultBuilder.WithDescription("Enter your party's emoji.")));
        var response4 = await inter.WaitForMessageAsync(message => message.MessageType == MessageType.Default && message.Author == ctx.Member);
        if (response4.TimedOut) {
            return;
        }

        var emoji = response4.Result.Content;

        await ctx.Channel.SendMessageAsync(
            new DiscordMessageBuilder().WithEmbed(defaultBuilder.WithDescription("Enter your party's description.")));
        var response5 = await inter.WaitForMessageAsync(message => message.MessageType == MessageType.Default && message.Author == ctx.Member);
        if (response5.TimedOut) {
            return;
        }

        var desc = response5.Result.Content;

        await ctx.Channel.SendMessageAsync(
            new DiscordMessageBuilder().WithEmbed(defaultBuilder.WithDescription("Enter your party's president.")));
        var response6 = await inter.WaitForMessageAsync(message => message.MessageType == MessageType.Default && message.Author == ctx.Member);
        if (response6.TimedOut) {
            return;
        }

        var president = response6.Result.Content;

        await ctx.Channel.SendMessageAsync(
            new DiscordMessageBuilder().WithEmbed(defaultBuilder.WithDescription("Upload your party's logo.")));
        var response7 = await inter.WaitForMessageAsync(message => Uri.IsWellFormedUriString(message.Content, UriKind.RelativeOrAbsolute) || // is url
            message.Attachments[0].MediaType.Contains("image")); // is image
        if (response7.TimedOut) {
            return;
        }

        string img;
        img = !string.IsNullOrWhiteSpace(response7.Result.Content) ? response7.Result.Content : response7.Result.Attachments[0].Url;

        //create the role
        var role = await ctx.Guild.CreateRoleAsync(shortName + "︱Követő", Permissions.None, colour, hoist: false,
            mentionable: true);

        var category = await ctx.Guild.CreateChannelCategoryAsync(emoji + "︱" + shortName,
            new[] { new DiscordOverwriteBuilder(role).Allow(Permissions.AccessChannels) });

        
        var info = await ctx.Guild.CreateChannelAsync("「" + emoji + "」" + shortName.ToLower(), ChannelType.Text,
            category, overwrites:
            new[] {
                new DiscordOverwriteBuilder(ctx.Guild.GetRole(Constants.szerepjatek)).Allow(Permissions.AccessChannels)
                    .Deny(Permissions.SendMessages),
                new DiscordOverwriteBuilder(ctx.Guild.EveryoneRole).Deny(Permissions.AccessChannels)
            });
        var general =
            await ctx.Guild.CreateChannelAsync("「" + emoji + "」" + "beszélgető", ChannelType.Text, category);

        var embed = info.SendMessageAsync(new DiscordMessageBuilder().WithEmbed(
            new DiscordEmbedBuilder().WithColor(colour).WithImageUrl(img).WithDescription(
                @$"**{emoji}{fullName} ({shortName}){emoji}**
━━━━━━━━━━━━━━━━━━━━
**LEÍRÁS**:
{desc}
**ELNÖK**:
{president}
")));

        await ctx.RespondAsync("Successfully created the party!");
    }

    [Command("editraw"), Description("Replaces the party embed with the message supplied.")]
    public async Task EditParty(CommandContext ctx, DiscordMessage message, DiscordColor? colour = null,
        string? desc = null, string? img = null, string? emoji = null, string? shortName = null,
        string? fullName = null, string? president = null) {
        var embed = message;
        await embed.ModifyAsync(new DiscordMessageBuilder().WithEmbed(
            new DiscordEmbedBuilder().WithColor(colour!.Value).WithImageUrl(img).WithDescription(
                @$"{emoji}{fullName} ({shortName}){emoji}
━━━━━━━━━━━━━━━━━━━━
**LEÍRÁS**:
{desc}
**ELNÖK**:
{president}
")));
        await ctx.RespondAsync("Successfully edited the party embed!");
    }

    // TODO
    [Command("edit"), Description("Edits the party embed. TODO")]
    public async Task EditParty(CommandContext ctx) {
    }
}