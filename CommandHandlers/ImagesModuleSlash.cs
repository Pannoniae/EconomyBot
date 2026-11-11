using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;

namespace EconomyBot;

/// Slash versions of the image commands. The commands themselves live in <see cref="ImagesCommands"/>; anything added
/// here needs its twin in <see cref="ImagesModule"/>.
public class ImagesModuleSlash : ApplicationCommandsModule {
    [SlashCommand("muv", "Ghost's anime thing.")]
    public Task muvluv(InteractionContext ctx) => Run.slash(ctx, ImagesCommands.muvluv);

    [SlashCommand("img", "Fetch a wholesome image.")]
    public Task img(InteractionContext ctx,
        [Option("reddit", "Get it from reddit instead.")]
        bool reddit = false) => Run.slash(ctx, c => ImagesCommands.img(c, reddit));

    [SlashCommand("b", "I love you Msozod :3")]
    public Task b(InteractionContext ctx) => Run.slash(ctx, ImagesCommands.b);

    [SlashCommand("xkcd", "Gets an XKCD.")]
    public Task xkcd(InteractionContext ctx,
        [Option("number", "Which one. Leave empty for a random one."), MinimumValue(1)]
        long number = -1) => Run.slash(ctx, c => ImagesCommands.xkcd(c, number < 1 ? null : (int)number));
}
