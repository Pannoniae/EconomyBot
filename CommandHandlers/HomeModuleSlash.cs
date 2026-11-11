using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;

namespace EconomyBot;

/// Slash commands which only get registered on our own server: the in-jokes, the maintenance ones. They'd only be noise in every other server's command list.
public class HomeModuleSlash : ApplicationCommandsModule {
    private const string FRONT = "Play it next instead of adding it to the end of the queue.";

    private static Task music(InteractionContext ctx, Func<MusicCommands, Task> body, bool voiceChecks = true,
        bool front = false) =>
        Run.slash(ctx, c => {
            c.front = front;
            return MusicCommands.run(c, body, voiceChecks);
        });

    [SlashCommand("earrape", "Toggles annoying users.")]
    public Task earrape(InteractionContext ctx) => music(ctx, m => m.earrape());

    [SlashCommand("soulseek", "Searches Soulseek and plays what you pick.")]
    public Task soulseek(InteractionContext ctx,
        [Option("query", "What to search for.")]
        string query,
        [Option("front", FRONT)] bool front = false) => music(ctx, m => m.soulseek(query), front: front);

    [SlashCommand("soulseekuser", "Searches one Soulseek user's files and plays what you pick.")]
    public Task soulseekUser(InteractionContext ctx,
        [Option("user", "The user to search.")]
        string user,
        [Option("query", "What to search for.")]
        string query,
        [Option("front", FRONT)] bool front = false) => music(ctx, m => m.soulseekUser(user, query), front: front);

    [SlashCommand("analyse", "Analyses the frequency of artists.")]
    public Task analyse(InteractionContext ctx) => music(ctx, m => m.analyse());

    [SlashCommand("rl", "Reloads music data.")]
    public Task reload(InteractionContext ctx) => music(ctx, m => m.reload());

    [SlashCommand("muv", "Ghost's anime thing.")]
    public Task muvluv(InteractionContext ctx) => Run.slash(ctx, ImagesCommands.muvluv);

    [SlashCommand("img", "Fetch a wholesome image.")]
    public Task img(InteractionContext ctx,
        [Option("reddit", "Get it from reddit instead.")]
        bool reddit = false) => Run.slash(ctx, c => ImagesCommands.img(c, reddit));

    [SlashCommand("b", "I love you Msozod :3")]
    public Task b(InteractionContext ctx) => Run.slash(ctx, ImagesCommands.b);

    [SlashCommand("webhook", "Says something through the channel's webhook.")]
    public Task webhook(InteractionContext ctx, [Option("message", "What to say.")] string message) =>
        Run.slash(ctx, c => ChatCommands.webhook(c, message), true);

    [SlashCommand("user", "Says something through the channel's webhook, as you.")]
    public Task userWebhook(InteractionContext ctx, [Option("message", "What to say.")] string message) =>
        Run.slash(ctx, c => ChatCommands.userWebhook(c, message), true);

    [SlashCommand("squish", "Squish someone!")]
    public Task squish(InteractionContext ctx, [Option("member", "Who gets squished.")] DiscordUser member) =>
        Run.slash(ctx, async c => {
            if (member is not DiscordMember m) {
                await c.respond("They aren't on this server.");
                return;
            }

            await ChatCommands.squish(c, m);
        }, true);

    [SlashCommand("0x", "0x appreciation.")]
    public Task love0x(InteractionContext ctx) => Run.slash(ctx, ChatCommands.love0x);

    [SlashCommand("panno", "Panno appreciation.")]
    public Task lovepanno(InteractionContext ctx) => Run.slash(ctx, ChatCommands.lovepanno);

    [SlashCommand("gc", "Clears the bot's memory.")]
    public Task gc(InteractionContext ctx) => Run.slash(ctx, ChatCommands.gc, true);
}
