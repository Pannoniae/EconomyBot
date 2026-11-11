using DisCatSharp.ApplicationCommands;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;

namespace EconomyBot;

/// Slash versions of the music commands. The commands themselves live in <see cref="MusicCommands"/>; anything added
/// here needs its twin in <see cref="MusicModule"/>.
///
/// Differences from the text side, all forced by how slash commands work: no overloads (so optional parameters
/// instead), no "!!" prefix (so a front option), no aliases.
public class MusicModuleSlash : ApplicationCommandsModule {
    private const string FRONT = "Play it next instead of adding it to the end of the queue.";

    private static Task music(InteractionContext ctx, Func<MusicCommands, Task> body, bool voiceChecks = true,
        bool front = false) =>
        Run.slash(ctx, c => {
            c.front = front;
            return MusicCommands.run(c, body, voiceChecks);
        });

    /// For the commands taking a time: slash commands have no TimeSpan, so it comes in as text.
    private static async Task timed(InteractionContext ctx, string text, Func<MusicCommands, TimeSpan, Task> body) {
        var parsed = await new CustomTimeSpanConverter().ConvertAsync(text.Trim(), null);
        if (!parsed.HasValue) {
            await Run.slash(ctx, c => c.respond("I can't read that as a time. Try something like `1m30s` or `2:15`."));
            return;
        }

        await music(ctx, m => body(m, parsed.Value));
    }

    [SlashCommand("eq", "Toggles the EQ.")]
    public Task eq(InteractionContext ctx) => music(ctx, m => m.eq());

    [SlashCommand("reset", "Resets the voice state.")]
    public Task reset(InteractionContext ctx) => music(ctx, m => m.resetPlayer());

    [SlashCommand("join", "Joins the voice channel.")]
    public Task join(InteractionContext ctx) => music(ctx, m => m.join(), false);

    [SlashCommand("jazz", "Plays some jazz. :3")]
    public Task jazz(InteractionContext ctx,
        [Option("search", "A specific track to look for, or \"all\". Leave empty for the usual.")]
        string search = "",
        [Option("front", FRONT)] bool front = false) =>
        music(ctx, m => string.IsNullOrWhiteSpace(search) ? m.jazz() : m.jazz(search), front: front);

    [SlashCommand("live", "Live music! :3")]
    public Task live(InteractionContext ctx) => music(ctx, m => m.live());

    [SlashCommand("analyse", "Analyses the frequency of artists.")]
    public Task analyse(InteractionContext ctx) => music(ctx, m => m.analyse());

    [SlashCommand("rl", "Reloads music data.")]
    public Task reload(InteractionContext ctx) => music(ctx, m => m.reload());

    [SlashCommand("stopjazz", "Stops jazz.")]
    public Task stopJazz(InteractionContext ctx) => music(ctx, m => m.stopJazz());

    [SlashCommand("play", "Plays a URL, or searches for the given keywords.")]
    public Task play(InteractionContext ctx,
        [Option("query", "A link, or what to search for.")]
        string query,
        [Option("front", FRONT)] bool front = false) =>
        music(ctx,
            m => Uri.TryCreate(query.Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
                ? m.play(uri)
                : m.play(query), front: front);

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

    [SlashCommand("artist", "Plays tracks from an artist.")]
    public Task artist(InteractionContext ctx, [Option("artist", "Who to play.")] string artist) =>
        music(ctx, m => m.artist(artist));

    [SlashCommand("stopartist", "Stops playing tracks from an artist.")]
    public Task stopArtist(InteractionContext ctx) => music(ctx, m => m.stopArtist());

    [SlashCommand("stop", "Stops playback and quits the voice channel.")]
    public Task stop(InteractionContext ctx) => music(ctx, m => m.stop());

    [SlashCommand("repeat", "Toggles repeat of the queue.")]
    public Task repeat(InteractionContext ctx) => music(ctx, m => m.repeat());

    [SlashCommand("earrape", "Toggles annoying users.")]
    public Task earrape(InteractionContext ctx) => music(ctx, m => m.earrape());

    [SlashCommand("clear", "Clears the queue.")]
    public Task clear(InteractionContext ctx) => music(ctx, m => m.clear());

    [SlashCommand("pause", "Pauses playback.")]
    public Task pause(InteractionContext ctx) => music(ctx, m => m.pause());

    [SlashCommand("resume", "Resumes playback.")]
    public Task resume(InteractionContext ctx) => music(ctx, m => m.resume());

    [SlashCommand("shuffle", "Shuffles the queue.")]
    public Task shuffle(InteractionContext ctx) => music(ctx, m => m.shuffle());

    [SlashCommand("skip", "Skips the current track.")]
    public Task skip(InteractionContext ctx,
        [Option("tracks", "How many tracks to skip."), MinimumValue(1), MaximumValue(50)]
        long tracks = 1) => music(ctx, m => m.skip((int)tracks));

    [SlashCommand("seek", "Seeks to the specified time in the current track.")]
    public Task seek(InteractionContext ctx,
        [Option("position", "Which time point to seek to, like 1m30s or 2:15.")]
        string position) => timed(ctx, position, (m, t) => m.seek(t));

    [SlashCommand("forward", "Forwards the track by the specified amount of time.")]
    public Task forward(InteractionContext ctx,
        [Option("offset", "By how much to forward, like 30s.")]
        string offset) => timed(ctx, offset, (m, t) => m.forward(t));

    [SlashCommand("rewind", "Rewinds the track by the specified amount of time.")]
    public Task rewind(InteractionContext ctx,
        [Option("offset", "By how much to rewind, like 30s.")]
        string offset) => timed(ctx, offset, (m, t) => m.rewind(t));

    [SlashCommand("volume", "Shows the playback volume, or sets it.")]
    public Task volume(InteractionContext ctx,
        [Option("volume", "Volume to set, 100 is normal. Leave empty to see the current one."), MinimumValue(0),
         MaximumValue(1000)]
        long volume = -1) => music(ctx, m => volume < 0 ? m.getVolume() : m.setVolume((int)volume));

    [SlashCommand("restart", "Restarts the playback of the current track.")]
    public Task restart(InteractionContext ctx) => music(ctx, m => m.restart());

    [SlashCommand("remove", "Removes a track from the playback queue.")]
    public Task remove(InteractionContext ctx,
        [Option("index", "Which track to remove (its number in the queue)."), MinimumValue(1)]
        long index) => music(ctx, m => m.remove((int)index));

    [SlashCommand("queue", "Displays the current playback queue.")]
    public Task queue(InteractionContext ctx) => music(ctx, m => m.queue(), false);

    [SlashCommand("nowplaying", "Displays information about the currently played track.")]
    public Task nowPlaying(InteractionContext ctx) => music(ctx, m => m.nowPlaying());

    [SlashCommand("playerinfo", "Displays information about the current player.")]
    public Task playerInfo(InteractionContext ctx) => music(ctx, m => m.playerInfo());
}
