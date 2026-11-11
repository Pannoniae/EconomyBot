using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Attributes;

namespace EconomyBot;

/// Text versions of the image commands. The commands themselves live in <see cref="ImagesCommands"/>; anything added
/// here needs its twin in <see cref="ImagesModuleSlash"/>.
public class ImagesModule : BaseCommandModule {
    [Command("muv"), Description("Ghost's anime thing.")]
    public async Task muvluv(CommandContext ctx, string luv) {
        if (luv == "luv") {
            await Run.prefix(ctx, ImagesCommands.muvluv);
        }
    }

    [Command("img"), Description("Fetch a wholesome image.")]
    public Task img(CommandContext ctx, string reddit = "no") =>
        Run.prefix(ctx, c => ImagesCommands.img(c, reddit == "reddit"));

    [Command("b"), Description("I love you Msozod :3")]
    public Task b(CommandContext ctx) => Run.prefix(ctx, ImagesCommands.b);

    [Command("xkcd"), Description("Gets a random XKCD.")]
    public Task xkcd(CommandContext ctx) => Run.prefix(ctx, c => ImagesCommands.xkcd(c, null));

    [Command("xkcd"), Description("Gets a specific XKCD.")]
    public Task xkcd(CommandContext ctx, int number) => Run.prefix(ctx, c => ImagesCommands.xkcd(c, number));
}
