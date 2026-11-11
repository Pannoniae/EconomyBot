using System.Globalization;
using DisCatSharp;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.CommandsNext;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.Interactivity.Entities;
using DisCatSharp.Interactivity.Enums;
using DisCatSharp.Interactivity.Extensions;
using EconomyBot.Logging;

namespace EconomyBot;

/// One invocation of a command, however it arrived. Command bodies (MusicCommands, ChatCommands, ImagesCommands) are
/// written once against this; the text and slash modules are just attribute stubs forwarding here, so the two can't
/// drift apart.
public abstract class Cmd {
    public abstract DiscordClient client { get; }
    public abstract DiscordGuild guild { get; }
    public abstract DiscordChannel channel { get; }
    public abstract DiscordMember member { get; }

    /// What the user typed to get here: "." or "!!" for text commands, "/" for slash ones.
    public abstract string prefix { get; }

    /// Queue at the front instead of the back ("!!" for text commands, front:true for slash ones).
    public bool front;

    /// The message the invocation replied to, if any. Slash commands can't reply to anything.
    public virtual ulong? repliedTo => null;

    public abstract Task<DiscordMessage> respond(string? text, DiscordEmbed? embed = null);

    public Task<DiscordMessage> respond(DiscordEmbed embed) => respond(null, embed);

    /// Edit something we got back from <see cref="respond(string?, DiscordEmbed?)"/> or <see cref="choose"/>.
    public abstract Task modify(DiscordMessage message, string text);

    /// "I'm working on it" - typing indicator for text, nothing for slash (those are already deferred).
    public abstract Task typing();

    /// Show a paginated list.
    public abstract Task paginate(List<Page> pages);

    /// Let the user pick one of several results. <paramref name="lines"/> is the long formatted form for a list,
    /// <paramref name="labels"/> the short plain form for a dropdown.
    /// Returns the picked index (-1 if they didn't pick) and the message to keep reporting progress in.
    public abstract Task<(int index, DiscordMessage message)> choose(IReadOnlyList<string> lines,
        IReadOnlyList<string> labels);

    /// Remove the message which invoked us, where there is one.
    public virtual Task deleteInvocation() => Task.CompletedTask;

    /// Check the invoking user's permissions in this channel, and tell them off if they don't have them.
    public async Task<bool> require(Permissions needed) {
        if (channel.PermissionsFor(member).HasPermission(needed)) {
            return true;
        }

        await respond($"You need the {needed.ToPermissionString()} permission for that.");
        return false;
    }

    protected static List<Page> toPages(IReadOnlyList<string> lines) {
        var pageCount = (lines.Count + 9) / 10;
        return lines.Chunk(10)
            .Select((chunk, i) => new Page($"{string.Join("\n", chunk)}\n\nPage {i + 1}/{pageCount}"))
            .ToList();
    }
}

/// A text command (.play, !!play).
public sealed class PrefixCmd : Cmd {
    private readonly CommandContext ctx;

    public PrefixCmd(CommandContext ctx) {
        this.ctx = ctx;
        front = ctx.Prefix == "!!";
    }

    public override DiscordClient client => ctx.Client;
    public override DiscordGuild guild => ctx.Guild!;
    public override DiscordChannel channel => ctx.Channel;
    public override DiscordMember member => ctx.Member!;
    public override string prefix => ctx.Prefix;
    public override ulong? repliedTo => ctx.Message.Reference?.Message?.Id;

    public override Task<DiscordMessage> respond(string? text, DiscordEmbed? embed = null) =>
        embed == null ? ctx.RespondAsync(text!) : text == null ? ctx.RespondAsync(embed) : ctx.RespondAsync(text, embed);

    public override Task modify(DiscordMessage message, string text) => message.ModifyAsync(text);

    public override Task typing() => ctx.TriggerTypingAsync();

    public override Task deleteInvocation() => ctx.Message.DeleteAsync();

    public override async Task paginate(List<Page> pages) {
        if (pages.Count == 1) {
            await ctx.Channel.SendMessageAsync(pages[0].Content!);
            return;
        }

        // NOTE: not awaited on purpose, this only completes once the buttons time out
        _ = ctx.Client.GetInteractivity().SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages,
            TimeSpan.FromMinutes(2), PaginationBehaviour.Ignore, ButtonPaginationBehavior.Ignore);
    }

    public override async Task<(int index, DiscordMessage message)> choose(IReadOnlyList<string> lines,
        IReadOnlyList<string> labels) {
        await paginate(toPages(lines));

        var msg = await ctx.RespondAsync(
            $"Type a number 1-{lines.Count} to queue a track. To cancel, type cancel or {MusicCommon.NumberMappingsReverse.Last()}.");

        var res = await ctx.Client.GetInteractivity().WaitForMessageAsync(
            x => x.Author == ctx.User && x.Channel == ctx.Channel,
            TimeSpan.FromMinutes(2));

        if (res.TimedOut || res.Result is null) {
            await msg.ModifyAsync($"{Program.cube} No choice was made.");
            return (-1, msg);
        }

        var typed = res.Result.Content.Trim();
        if (typed.Equals("cancel", StringComparison.OrdinalIgnoreCase) || typed == "-1") {
            await msg.ModifyAsync($"{Program.cube} Choice cancelled.");
            return (-1, msg);
        }

        if (!int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 1 ||
            n > lines.Count) {
            await msg.ModifyAsync($"{Program.cube} Invalid choice was made.");
            return (-1, msg);
        }

        return (n - 1, msg);
    }
}

/// A slash command. Always deferred before the body runs (see <see cref="Run.slash"/>), so the first response
/// fills in the deferred one and anything after that is a follow-up.
public sealed class SlashCmd(InteractionContext ctx, bool ephemeral) : Cmd {
    /// discord's cap on select menu options
    private const int MAX_OPTIONS = 25;

    private const string PICK = "pick";
    private const string CANCEL = "cancel";

    private DiscordMessage? original;

    public override DiscordClient client => ctx.Client;
    public override DiscordGuild guild => ctx.Guild!;
    public override DiscordChannel channel => ctx.Channel;
    public override DiscordMember member => ctx.Member!;
    public override string prefix => "/";

    public override Task<DiscordMessage> respond(string? text, DiscordEmbed? embed = null) => send(text, embed, null);

    private async Task<DiscordMessage> send(string? text, DiscordEmbed? embed, DiscordComponent? component) {
        if (original is null) {
            var b = new DiscordWebhookBuilder();
            if (text != null) b.WithContent(text);
            if (embed != null) b.AddEmbed(embed);
            if (component != null) b.AddComponents(component);
            return original = await ctx.EditResponseAsync(b);
        }

        var f = new DiscordFollowupMessageBuilder();
        if (text != null) f.WithContent(text);
        if (embed != null) f.AddEmbed(embed);
        if (component != null) f.AddComponents(component);
        if (ephemeral) f.AsEphemeral();
        return await ctx.FollowUpAsync(f);
    }

    public override Task modify(DiscordMessage message, string text) => message.Id == original?.Id
        ? ctx.EditResponseAsync(text)
        : ctx.EditFollowupAsync(message.Id, text);

    public override Task typing() => Task.CompletedTask;

    public override async Task paginate(List<Page> pages) {
        if (pages.Count == 1 || original is not null) {
            // already answered once, so the paginator can't own the response any more: just send the lot
            foreach (var page in pages) {
                await respond(page.Content!);
            }

            return;
        }

        // the paginator takes over the deferred response from here; keep a handle so finish() doesn't write over it
        var task = ctx.Interaction.SendPaginatedResponseAsync(true, ephemeral, ctx.User, pages,
            behaviour: PaginationBehaviour.Ignore, deletion: ButtonPaginationBehavior.Ignore);
        original = await ctx.GetOriginalResponseAsync();
        // NOTE: not awaited on purpose, this only completes once the buttons time out
        _ = task;
    }

    public override async Task<(int index, DiscordMessage message)> choose(IReadOnlyList<string> lines,
        IReadOnlyList<string> labels) {
        // one slot goes to "cancel"
        var shown = Math.Min(labels.Count, MAX_OPTIONS - 1);
        var options = labels.Take(shown)
            .Select((label, i) => new DiscordStringSelectComponentOption(
                trim($"{i + 1}. {label}", 100), i.ToString(CultureInfo.InvariantCulture)))
            .Append(new DiscordStringSelectComponentOption("Cancel", CANCEL));

        var note = shown < labels.Count ? $" (showing the first {shown} of {labels.Count})" : "";
        var msg = await send($"{Program.cube} Pick a track{note}:", null,
            new DiscordStringSelectComponent("Pick a track", options, PICK));

        var res = await ctx.Client.GetInteractivity().WaitForSelectAsync(msg, ctx.User, PICK,
            ComponentType.StringSelect, TimeSpan.FromMinutes(2));

        if (res.TimedOut || res.Result is null) {
            await clear(msg, $"{Program.cube} No choice was made.");
            return (-1, msg);
        }

        var value = res.Result.Values.FirstOrDefault();
        var picked = value != CANCEL && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var i) &&
                     i < shown
            ? i
            : -1;

        // answering the dropdown with an update is what removes it from the message
        await res.Result.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage,
            new DiscordInteractionResponseBuilder().WithContent(picked == -1
                ? $"{Program.cube} Choice cancelled."
                : $"{Program.cube} Picked {labels[picked]}..."));
        return (picked, msg);
    }

    /// Replace a message's text and drop its components.
    private Task clear(DiscordMessage message, string text) {
        var b = new DiscordWebhookBuilder().WithContent(text);
        return message.Id == original?.Id ? ctx.EditResponseAsync(b) : ctx.EditFollowupAsync(message.Id, b);
    }

    /// A deferred interaction shows "thinking..." until it gets an answer, so commands which have nothing to say
    /// still need to say something.
    public Task finish() => original is null ? respond("Done.") : Task.CompletedTask;

    private static string trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}

public static class Run {
    private static readonly Logger logger = Logger.getClassLogger("Commands");

    /// Run a command body for a text command. Anything it throws goes to the CommandsNext error handler as before.
    public static async Task prefix(CommandContext ctx, Func<Cmd, Task> body) {
        if (ctx.Guild is null || ctx.Member is null) {
            await ctx.RespondAsync("That only works on a server.");
            return;
        }

        await body(new PrefixCmd(ctx));
    }

    /// Run a command body for a slash command: defer first (so nothing has to care about the 3 second deadline),
    /// make sure it ends up with some answer, and keep exceptions from vanishing into the library.
    public static async Task slash(InteractionContext ctx, Func<Cmd, Task> body, bool ephemeral = false) {
        if (ctx.Guild is null || ctx.Member is null) {
            await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("That only works on a server."));
            return;
        }

        var defer = new DiscordInteractionResponseBuilder();
        await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            ephemeral ? defer.AsEphemeral() : defer);

        var c = new SlashCmd(ctx, ephemeral);
        try {
            await body(c);
            await c.finish();
        }
        catch (Exception e) when (Program.isExpected(e)) {
            logger.warn($"/{ctx.FullCommandName}: {Program.describe(e)}");
        }
        catch (Exception e) {
            await Program.tryDiscord($"telling the user /{ctx.FullCommandName} broke",
                () => c.respond($"That broke: {e.GetType().Name}: {e.Message}"));
            await Program.reportError($"running /{ctx.FullCommandName}", e);
        }
    }
}
