using System.Numerics;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using EconomyBot.Logging;
using SpotifyAPI.Web;

namespace EconomyBot;

/// <summary>
/// Represents data for the music playback in a discord guild.
/// </summary>
public sealed class GuildMusicData {
    private WebhookCache webhookCache;

    private static readonly Logger logger = Logger.getClassLogger("GuildMusicData");

    /// <summary>
    /// Is EQ enabled?
    /// </summary>
    public bool eq;

    /// <summary>
    /// Gets the playback volume for this guild.
    /// </summary>
    public int volume { get; private set; } = 100;

    public MusicQueue queue;

    /// <summary>
    /// Gets or sets the channel in which commands are executed.
    /// </summary>
    public DiscordChannel CommandChannel { get; set; }

    private DiscordGuild Guild { get; }
    private LavalinkExtension Lavalink { get; }
    public LavalinkGuildConnection? Player { get; private set; }

    public LavalinkNodeConnection Node { get; }

    // TODO implement a *proper* music weighting system

    public static readonly Dictionary<string, Artist> artistMappings = new() {
        { "_fats", new Artist(@"E:\music\Fats Waller", 1.5) },
        { "ella mae morse", new Artist(@"E:\music\Ella Mae Morse", 1.2) },
        { "slim gaillard", new Artist(@"E:\music\Slim Gaillard", 1.0) },
        { "louis jordan", new Artist(@"E:\music\Louis Jordan", 1.0) },
        { "caravan palace", new Artist(@"E:\music\Caravan Palace", 1.0, 2) },
        { "tape five", new Artist(@"E:\music\Tape Five", 1.0) },
        { "caro emerald", new Artist(@"E:\music\Caro Emerald", 1.0) },
        { "chuck berry", new Artist(@"E:\music\Chuck Berry", 1.0, 0.25) }, // most of this is trash
        { "jamie berry", new Artist(@"E:\music\Jamie Berry", 0.8) },
        { "sim gretina", new Artist(@"E:\music\Sim Gretina", 0.8, 0) }, // too much earrape
        { "freshly squeezed", new Artist(@"E:\music\Freshly Squeezed Music", 0.8, 0.5, 0.5, 0.3) },
        { "puppini sisters", new Artist(@"E:\music\Puppini Sisters", 1.0) },
        { "11 acorn lane", new Artist(@"E:\music\11 Acorn Lane", 1.0) },
        { "electric swing circus", new Artist(@"E:\music\Electric Swing Circus", 1.0) },
        { "the speakeasies swing band", new Artist(@"E:\music\The Speakeasies Swing Band", 1.0) },
        { "donald lambert", new Artist(@"E:\music\Donald Lambert", 1.5) },
        { "newport", new Artist(@"E:\music\Newport Jazz Festival", 1.5) }, // 1960 Newport Jazz Festival, full recording
        { "hot sardines", new Artist(@"E:\music\The Hot Sardines", 1.0) }
    };

    public static readonly Dictionary<string, double> artistWeights = new();

    /// <summary>
    /// Gets the actual volume to set.
    /// </summary>
    public int effectiveVolume =>
        (int)(volume * (artistMappings.GetValueOrDefault(queue.NowPlaying?.artist ?? "missing")?.volume ?? 1));

    public double artistVolume => artistMappings.GetValueOrDefault(queue.NowPlaying?.artist ?? "missing")?.volume ?? 1;

    /// <summary>
    /// Creates a new instance of playback data.
    /// </summary>
    /// <param name="guild">Guild to track data for.</param>
    /// <param name="lavalink">Lavalink service.</param>
    /// <param name="node">The Lavalink node this guild is connected to.</param>
    public GuildMusicData(DiscordGuild guild, LavalinkExtension lavalink, LavalinkNodeConnection node) {
        Node = node;
        Guild = guild;
        Lavalink = lavalink;
        queue = new MusicQueue(this);

        foreach (var artist in artistMappings) {
            // get the count of files at the directory
            int fCount = Directory
                .GetFiles(artist.Value.path, "*", new EnumerationOptions { RecurseSubdirectories = true })
                .Length;
            artistWeights[artist.Key] = fCount * artist.Value.weight;
        }

        webhookCache = new WebhookCache(Guild);

        logger.info("Initialised artist weights.");
    }

    public void setupWebhooks() {
        _ = webhookCache.setup();
    }

    public async Task setupForChannel(DiscordChannel channel) {
        await webhookCache.setupForChannel(channel);
    }

    public async Task<DiscordWebhook> getWebhook(DiscordChannel channel) {
        await webhookCache.setupForChannel(channel);
        return webhookCache.getWebhook(channel);
    }

    /// <summary>
    /// Pauses the playback.
    /// </summary>
    public async Task PauseAsync() {
        if (Player == null || !Player.IsConnected)
            return;

        await Player.PauseAsync();
    }

    /// <summary>
    /// Resumes the playback.
    /// </summary>
    public async Task ResumeAsync() {
        if (Player == null || !Player.IsConnected)
            return;

        await Player.ResumeAsync();
    }

    /// <summary>
    /// Sets playback volume.
    /// </summary>
    public async Task SetVolumeAsync(int vol) {
        if (Player == null || !Player.IsConnected)
            return;

        volume = vol;
        await Player.SetVolumeAsync(effectiveVolume);
    }

    /// <summary>
    /// Seeks the currently-playing track.
    /// </summary>
    /// <param name="target">Where or how much to seek by.</param>
    /// <param name="relative">Whether the seek is relative.</param>
    public async Task SeekAsync(TimeSpan target, bool relative) {
        if (Player == null || !Player.IsConnected)
            return;

        if (!relative)
            await Player.SeekAsync(target);
        else
            await Player.SeekAsync(Player.CurrentState.PlaybackPosition + target);
    }

    /// <summary>
    /// Creates a player for this guild.
    /// </summary>
    /// <returns></returns>
    public async Task CreatePlayerAsync(DiscordChannel channel) {
        if (Player != null && Player.IsConnected) {
            return;
        }

        Player = await Node.ConnectAsync(channel);

        await SetVolumeAsync(volume);

        if (!eq) {
            enableEQ();
        }

        Player.PlaybackFinished += (con, e) => queue.Player_PlaybackFinished(con, e);
        Player.PlaybackStarted += (sender, e) => queue.Player_PlaybackStarted(sender, e);
    }

    /// <summary>
    /// Destroys a player for this guild.
    /// </summary>
    /// <returns></returns>
    public async Task DestroyPlayerAsync() {
        if (Player == null)
            return;

        if (Player.IsConnected)
            await Player.DisconnectAsync();

        Player = null;
    }

    /// <summary>
    /// Gets the current position in the track.
    /// </summary>
    /// <returns>Position in the track.</returns>
    public TimeSpan GetCurrentPosition() {
        return queue.NowPlaying == default ? TimeSpan.Zero : Player.CurrentState.PlaybackPosition;
    }


    public async Task AddToRandom(string artist) {
        var config = SpotifyClientConfig
            .CreateDefault()
            .WithAuthenticator(new ClientCredentialsAuthenticator(Constants.spotifytoken, Constants.spotifytoken2));
        var spotify = new SpotifyClient(config);

        var results = (await spotify.Search.Item(new SearchRequest(SearchRequest.Types.Artist, $"artist:\"{artist}\"") {
            Limit = 2
        })).Artists.Items;
        FullArtist result;
        if (results.Any()) {
            result = results[0];
        }
        else {
            return;
        }

        var tracksList = (await spotify.Search.Item(
            new SearchRequest(SearchRequest.Types.Track, $"artist:\"{result.Name}\"") {
                Limit = 1,
                Offset = new Random().Next(1000)
            })).Tracks;
        FullTrack track;
        if (tracksList.Items.Any()) {
            track = tracksList.Items[0];
        }
        else {
            var secondRequest = (await spotify.Search.Item(
                new SearchRequest(SearchRequest.Types.Track, $"artist:\"{result.Name}\"") {
                    Limit = 1,
                    Offset = new Random().Next(tracksList.Total.Value)
                })).Tracks;
            track = secondRequest.Items[0];
        }

        var trackLoad = await Node.Rest.GetTracksAsync(result.Name + " " + track.Name);
        var tracks = trackLoad.Tracks;
        if (trackLoad.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks.Any()) {
            logger.error("Error loading random track");
        }

        queue.Enqueue(tracks.First(), artist);
    }

    public async Task<IEnumerable<LavalinkLoadResult>> getJazz(string searchTerm) {
        return artistMappings.SelectMany(
                artist => Directory.GetFiles(artist.Value.path, searchTerm,
                    new EnumerationOptions { RecurseSubdirectories = true }))
            .Select(file => new FileInfo(file))
            .Select(file => getTracksAsync(Node.Rest, file).Result);
    }

    public static async Task<LavalinkLoadResult> getTracksAsync(LavalinkRestClient client, FileInfo file) {
        var tracks = await client.GetTracksAsync(file);

        // This is a typical example of bad cargo-cult programming. I am leaving it in because it doesn't matter,
        // but this is how programs get slow.
        // The naive programmer would think that filtering with where is cheap.
        // The naive programmer doesn't think about the fact that it traverses the entire array or list.
        // Instead of converting it to a proper imperative loop, writing a helper for concatenating the two conditions,
        // or using a better language, we just copypaste the loop and hope for the best.
        foreach (var track in tracks.Tracks.Where(track => track.Title == "Unknown title")) {
            track.GetType().GetProperty("Title")!.SetValue(track, Path.GetFileNameWithoutExtension(file.Name));
        }

        foreach (var track in tracks.Tracks.Where(track => track.Author == "Unknown artist")) {
            // Not to mention that we are literally reflecting the Track object because the stupid authors thought
            // their autodetection was infallible thus they haven't provided a way to properly set the track's name which will be displayed.
            // The end-user is probably not very delighted at seeing "unknown author" or "unknown title" so we make a best-effort guess here.
            track.GetType().GetProperty("Author")!.SetValue(track, file.Directory!.Name);
        }

        return tracks;
    }

    public void enableEQ() {
        eq = true;
        logger.info("Enabled EQ");
        Player.AdjustEqualizerAsync(
            new LavalinkBandAdjustment(0, 0.2f),
            new LavalinkBandAdjustment(1, 0.2f),
            new LavalinkBandAdjustment(2, 0.2f),
            new LavalinkBandAdjustment(3, 0.2f),
            new LavalinkBandAdjustment(4, 0.15f),
            new LavalinkBandAdjustment(5, 0.12f),
            new LavalinkBandAdjustment(6, 0.10f),
            new LavalinkBandAdjustment(7, 0.05f),
            new LavalinkBandAdjustment(8, 0.00f),
            new LavalinkBandAdjustment(9, 0.00f),
            new LavalinkBandAdjustment(10, -0.02f),
            new LavalinkBandAdjustment(11, -0.02f),
            new LavalinkBandAdjustment(12, -0.03f),
            new LavalinkBandAdjustment(13, -0.04f),
            new LavalinkBandAdjustment(14, -0.05f));
    }

    public void disableEQ() {
        eq = false;
        logger.info("Disabled EQ");
        Player.ResetEqualizerAsync();
    }

    public void toggleEQ() {
        if (eq) {
            disableEQ();
        }
        else {
            enableEQ();
        }
    }
}

/// <summary>
/// Stores artist information which is used for song selection.
/// </summary>
/// <param name="path">The path of the music files for the artist.</param>
/// <param name="volume">The volume modifier for the artist.</param>
/// <param name="weight">The rarity multiplier for the artist in the random selection.</param>
/// <param name="repeatPenalty">If the queue already contains the artist, the chance of selecting the artist again will be multiplied by the value.</param>
/// <param name="doubleRepeatPenalty">If the previous track is the same artist, the chance of selecting the artist again will be multiplied by the value.</param>
public record Artist(string path,
    double volume,
    double weight = 1.0,
    double repeatPenalty = 1.0,
    double doubleRepeatPenalty = 1.0);

public record Track(LavalinkTrack track, string? artist);

public static class IEnumerableExtensions {
    private static Random rand = new();

    public static T randomElementByWeight<T>(this IEnumerable<T> sequence, Func<T, double> weightSelector) {
        var elements = sequence.ToList();
        double totalWeight = elements.Sum(weightSelector);
        // The weight we are after...
        double itemWeightIndex = rand.NextDouble() * totalWeight;
        double currentWeightIndex = 0;

        foreach (var item in elements) {
            var weight = weightSelector(item);
            currentWeightIndex += weight;

            // If we've hit or passed the weight we are after for this item then it's the one we want....
            if (currentWeightIndex >= itemWeightIndex)
                return item;
        }

        return default!;
    }
}