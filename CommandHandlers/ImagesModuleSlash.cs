using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;

namespace EconomyBot;

/// Slash versions of the image commands. The commands themselves live in <see cref="ImagesCommands"/>; anything added
/// here needs its twin in <see cref="ImagesModule"/>.
public class ImagesModuleSlash : ApplicationCommandsModule {
    [SlashCommand("xkcd", "Gets an XKCD.")]
    public Task xkcd(InteractionContext ctx,
        [Option("number", "Which one. Leave empty for a random one."), MinimumValue(1)]
        long number = -1) => Run.slash(ctx, c => ImagesCommands.xkcd(c, number < 1 ? null : (int)number));
}
