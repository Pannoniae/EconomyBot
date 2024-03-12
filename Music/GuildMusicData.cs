using System.Runtime.InteropServices;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Entities.Filters;
using DisCatSharp.Lavalink.Enums;
using DisCatSharp.Lavalink.Enums.Filters;
using DisCatSharp.Lavalink.EventArgs;
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
    public LavalinkGuildPlayer? Player { get; private set; }

    public LavalinkSession Node { get; }

    // TODO implement a *proper* music weighting system

    public static string rootPath;

    public static readonly Dictionary<string, Artist> artistMappings = new() {
        { "_fats", new Artist("Fats Waller", "Fats Waller", 1.5) },
        { "_fatslive", new Artist("Fats Waller Live", "Fats Waller/Fats Waller Live", 1.5) },
        { "ella mae morse", new Artist("Ella Mae Morse", "Ella Mae Morse", 1.2) },
        { "slim gaillard", new Artist("Slim Gaillard", "Slim Gaillard", 1.0) },
        { "louis jordan", new Artist("Louis Jordan", "Louis Jordan", 1.0) },
        { "caravan palace", new Artist("Caravan Palace", "Caravan Palace", 1.0, 2) },
        { "tape five", new Artist("Tape Five", "Tape Five", 1.0) },
        { "caro emerald", new Artist("Caro Emerald", "Caro Emerald", 1.0) },
        { "chuck berry", new Artist("Chuck Berry", "Chuck Berry", 1.0, 0.25) }, // most of this is trash
        { "jamie berry", new Artist("Jamie Berry", "Jamie Berry", 0.8) },
        { "sim gretina", new Artist("Sim Gretina", "Sim Gretina", 0.8, 0) }, // too much earrape
        { "freshly squeezed", new Artist("Freshly Squeezed Music", "Freshly Squeezed Music", 0.8, 0.25, 0.5, 0.3) },
        { "puppini sisters", new Artist("Puppini Sisters", "Puppini Sisters", 1.0) },
        { "11 acorn lane", new Artist("11 Acorn Lane", "11 Acorn Lane", 1.0) },
        { "electric swing circus", new Artist("Electric Swing Circus", "Electric Swing Circus", 1.0) },
        { "the speakeasies swing band", new Artist("The Speakeasies Swing Band", "The Speakeasies Swing Band", 1.0) },
        { "donald lambert", new Artist("Donald Lambert", "Donald Lambert", 1.5) },
        // 1960 Newport Jazz Festival, full recording
        { "newport", new Artist("Newport Jazz Festival", "Newport Jazz Festival", 1.5) },
        { "hot sardines", new Artist("The Hot Sardines", "The Hot Sardines", 1.0) },
        { "louis armstrong", new Artist("Louis Armstrong", "Louis Armstrong", 1.0, 0.25) },
        { "ella fitzgerald", new Artist("Ella Fitzgerald", "Ella Fitzgerald", 1.0, 0.25) },
    };

    public static readonly Dictionary<string, double> artistWeights = new();

    /// <summary>
    /// Gets the actual volume to set.
    /// </summary>
    public int effectiveVolume =>
        (int)(volume * artistVolume);

    public double artistVolume => artistMappings.GetValueOrDefault(queue.NowPlaying?.artist ?? "missing")?.volume ?? 1;

    /// <summary>
    /// Creates a new instance of playback data.
    /// </summary>
    /// <param name="guild">Guild to track data for.</param>
    /// <param name="lavalink">Lavalink service.</param>
    /// <param name="node">The Lavalink node this guild is connected to.</param>
    public GuildMusicData(DiscordGuild guild, LavalinkExtension lavalink, LavalinkSession node) {
        // setup paths by OS
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
            rootPath = "/snd/music";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
            rootPath = "E:/music";
        }
        else {
            throw new NotSupportedException("OS not supported, specify paths for the music.");
        }

        Node = node;
        Guild = guild;
        Lavalink = lavalink;
        queue = new MusicQueue(this);

        foreach (var artist in artistMappings) {
            // get the count of files at the directory
            int fCount = 0;
            try {
                fCount = Directory
                    .GetFiles(getPath(artist.Value.path), "*",
                        new EnumerationOptions
                            { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive })
                    .Length;
            }
            catch (DirectoryNotFoundException e) {
                fCount = 0;
            }

            artistWeights[artist.Key] = fCount * artist.Value.weight;
        }

        webhookCache = new WebhookCache(Guild);

        logger.info("Initialised artist weights.");
    }

    public static string getPath(string path) {
        return Path.Combine(rootPath, path);
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

    /// <summary>summary
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
            await Player.SeekAsync(Player.TrackPosition + target);
    }

    /// <summary>
    /// Creates a player for this guild.
    /// </summary>
    /// <returns></returns>
    public async Task CreatePlayerAsync(DiscordChannel channel) {
        if (Player != null && Player.IsConnected) {
            return;
        }

        Player = await Node.ConnectAsync(channel, false);

        await SetVolumeAsync(volume);

        if (!eq) {
            enableEQ();
        }

        Player.TrackEnded += (con, e) => queue.Player_PlaybackFinished(con, e);
        Player.TrackStarted += (sender, e) => queue.Player_PlaybackStarted(sender, e);
        Player.TrackException += Lavalink_TrackExceptionThrown;
    }

    private async Task Lavalink_TrackExceptionThrown(LavalinkGuildPlayer con, LavalinkTrackExceptionEventArgs e) {
        if (e.Guild is null) {
            return;
        }

        await CommandChannel.SendMessageAsync(
            $"{DiscordEmoji.FromName(Program.client, ":pinkpill:")} A problem occured while playing {Formatter.Sanitize(e.Track.Info.Title).Bold()} by {Formatter.Sanitize(e.Track.Info.Author).Bold()}:\n{e.Exception}");
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
        return queue.NowPlaying == default ? TimeSpan.Zero : Player.TrackPosition;
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

        var trackLoad = await Node.LoadTracksAsync(result.Name + " " + track.Name);
        var tracks = trackLoad.Result;
        if (trackLoad.LoadType == LavalinkLoadResultType.Error || trackLoad.LoadType == LavalinkLoadResultType.Track && (LavalinkTrack)tracks == null) {
            logger.error("Error loading random track");
        }

        queue.Enqueue((LavalinkTrack)tracks, artist);
    }

    public async Task<IEnumerable<LavalinkTrackLoadingResult>> getJazz(string searchTerm) {
        return artistMappings.Where(artist => Path.Exists(getPath(artist.Value.path))).SelectMany(
                artist => Directory.GetFiles(getPath(artist.Value.path), searchTerm,
                    new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }))
            .Select(file => getTracksAsync(Node, file).Result);
    }

    public static async Task<LavalinkTrackLoadingResult> getTracksAsync(LavalinkSession client, string file) {
        var tracks = await client.LoadTracksAsync(file);

        //var result = tracks.Result as LavalinkTrack;
        /*// This is a typical example of bad cargo-cult programming. I am leaving it in because it doesn't matter,
        // but this is how programs get slow.
        // The naive programmer would think that filtering with where is cheap.
        // The naive programmer doesn't think about the fact that it traverses the entire array or list.
        // Instead of converting it to a proper imperative loop, writing a helper for concatenating the two conditions,
        // or using a better language, we just copypaste the loop and hope for the best.
        if (result.Info.Title == "Unknown title") {
            track.GetType().GetProperty("Title")!.SetValue(track, Path.GetFileNameWithoutExtension(file.Name));
        }

        if (result.Info.Author == "Unknown artist") {
            // Not to mention that we are literally reflecting the Track object because the stupid authors thought
            // their autodetection was infallible thus they haven't provided a way to properly set the track's name which will be displayed.
            // The end-user is probably not very delighted at seeing "unknown author" or "unknown title" so we make a best-effort guess here.
            track.GetType().GetProperty("Author")!.SetValue(track, file.Directory!.Name);
        }*/

        return tracks;
    }

    public void enableEQ() {
        eq = true;
        logger.info("Enabled EQ");
        Player.UpdateAsync(action => action.Filters = new LavalinkFilters {
                Equalizers = new List<LavalinkEqualizer> {
                    new((LavalinkFilterBand)0, 0.2f),
                    new((LavalinkFilterBand)1, 0.2f),
                    new((LavalinkFilterBand)2, 0.2f),
                    new((LavalinkFilterBand)3, 0.2f),
                    new((LavalinkFilterBand)4, 0.15f),
                    new((LavalinkFilterBand)5, 0.12f),
                    new((LavalinkFilterBand)6, 0.10f),
                    new((LavalinkFilterBand)7, 0.05f),
                    new((LavalinkFilterBand)8, 0.00f),
                    new((LavalinkFilterBand)9, 0.00f),
                    new((LavalinkFilterBand)10, -0.02f),
                    new((LavalinkFilterBand)11, -0.02f),
                    new((LavalinkFilterBand)12, -0.03f),
                    new((LavalinkFilterBand)13, -0.04f),
                    new((LavalinkFilterBand)14, -0.05f)
                }
            }
        );
    }

    public void disableEQ() {
        eq = false;
        logger.info("Disabled EQ");
        Player.UpdateAsync(action => action.Filters = null!);
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
/// <param name="name">The artist's name.</param>
/// <param name="path">The path of the music files for the artist.</param>
/// <param name="volume">The volume modifier for the artist.</param>
/// <param name="weight">The rarity multiplier for the artist in the random selection.</param>
/// <param name="historyRepeatPenalty">If the history already contains the artist, the chance of selecting the artist again will be multiplied by the value.</param>
/// <param name="doubleRepeatPenalty">If the previous track is the same artist, the chance of selecting the artist again will be multiplied by the value.</param>
public record Artist(
    string name,
    string path,
    double volume,
    double weight = 1.0,
    double historyRepeatPenalty = 1.0,
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