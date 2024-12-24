using System.Runtime.InteropServices;
using EconomyBot.Logging;
using Lavalink4NET;
using Lavalink4NET.Filters;
using Lavalink4NET.NetCord;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.Commands;
using Spectre.Console;
using SpotifyAPI.Web;
using Directory = System.IO.Directory;

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
    public TextGuildChannel CommandChannel { get; set; }

    private Guild Guild { get; }
    private AudioService Lavalink { get; }
    public LavalinkPlayer? Player { get; private set; }


    // TODO implement a *proper* music weighting system

    public static string rootPath;

    public static Dictionary<string, Artist> artistMappings = new() {
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
        { "wynonie harris", new Artist("Wynonie Harris", "Wynonie Harris", 1.0) },
        { "willie the lion smith", new Artist("Willie 'The Lion' Smith", "Willie 'The Lion' Smith", 1.5) },
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
    public GuildMusicData(Guild guild, AudioService lavalink) {
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

        Guild = guild;
        Lavalink = lavalink;
        queue = new MusicQueue(this);

        reload();

        webhookCache = new WebhookCache(Guild);
    }

    public static void reload() {
        // load artist data from SDL file if available
        var parser = new SDLParser();
        try {
            artistMappings = parser.parse("artists.sdl");
        }
        catch (Exception e) {
            logger.warn("Couldn't load artist data, using defaults.");
            AnsiConsole.WriteLine(e.ToString());
        }
        var x = artistMappings.Count;
        logger.info($"Loaded {x} artists from file.");

        foreach (var artist in artistMappings) {
            // get the count of files at the directory
            int fCount;
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

        logger.info("Initialised artist weights.");
    }

    private void loadArtistDataIntoMapping(Env env) {
        foreach (var artist in env.artists) {
            artistMappings[artist.Key] = artist.Value;
        }
    }

    public static string getPath(string path) {
        return Path.Combine(rootPath, path);
    }

    public void setupWebhooks() {
        _ = webhookCache.setup();
    }

    public async Task setupForChannel(IGuildChannel channel) {
        await webhookCache.setupForChannel(channel);
    }

    public async Task<Webhook> getWebhook(IGuildChannel channel) {
        await webhookCache.setupForChannel(channel);
        return webhookCache.getWebhook(channel);
    }

    /// <summary>
    /// Pauses the playback.
    /// </summary>
    public async Task PauseAsync() {
        if (Player == null || !Player.ConnectionState.IsConnected)
            return;

        await Player.PauseAsync();
    }

    /// <summary>summary
    /// Resumes the playback.
    /// </summary>
    public async Task ResumeAsync() {
        if (Player == null || !Player.ConnectionState.IsConnected)
            return;

        await Player.ResumeAsync();
    }

    /// <summary>
    /// Sets playback volume.
    /// </summary>
    public async Task SetVolumeAsync(int vol) {
        if (Player == null || !Player.ConnectionState.IsConnected)
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
        if (Player == null || !Player.ConnectionState.IsConnected)
            return;

        if (!relative)
            await Player.SeekAsync(target);
        else
            await Player.SeekAsync(Player.Position.Value.Position + target);
    }

    /// <summary>
    /// Creates a player for this guild.
    /// </summary>
    /// <returns></returns>
    public async Task CreatePlayerAsync(CommandContext ctx, IVoiceGuildChannel channel) {
        if (Player != null && Player.ConnectionState.IsConnected) {
            return;
        }

        var retrieveOptions = new PlayerRetrieveOptions(ChannelBehavior: PlayerChannelBehavior.Join);

        var result = await Lavalink.Players
            .RetrieveAsync(ctx, playerFactory: PlayerFactory.Queued, retrieveOptions);

        var player = result.Player;

        await SetVolumeAsync(volume);

        if (!eq) {
            enableEQ();
        }
    }

    private static string GetErrorMessage(PlayerRetrieveStatus retrieveStatus) => retrieveStatus switch {
        PlayerRetrieveStatus.UserNotInVoiceChannel => "You are not connected to a voice channel.",
        PlayerRetrieveStatus.BotNotConnected => "The bot is currently not connected.",
        _ => "Unknown error.",
    };



    /// <summary>
    /// Destroys a player for this guild.
    /// </summary>
    /// <returns></returns>
    public async Task DestroyPlayerAsync() {
        if (Player == null)
            return;

        if (Player.ConnectionState.IsConnected)
            await Player.DisconnectAsync();

        Player = null;
    }

    /// <summary>
    /// Gets the current position in the track.
    /// </summary>
    /// <returns>Position in the track.</returns>
    public TimeSpan GetCurrentPosition() {
        return queue.NowPlaying == null ? TimeSpan.Zero : Player.Position.Value.Position;
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

        var trackLoad = await Lavalink.Tracks.LoadTracksAsync(result.Name + " " + track.Name, TrackSearchMode.YouTube);
        var track_ = trackLoad.Track;
        if (trackLoad.IsFailed || trackLoad.Exception != null || trackLoad.Track == null) {
            logger.error($"Error loading random track: {trackLoad.Exception}");
        }

        queue.Enqueue(track_, artist);
    }

    public async Task<IEnumerable<LavalinkTrack?>> getJazz(string searchTerm) {
        return artistMappings.Where(
                artist => Path.Exists(getPath(artist.Value.path)))
            .SelectMany(
                artist => Directory.GetFiles(getPath(artist.Value.path), searchTerm,
                    new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }))
            .Where(extensionFilter)
            .Select(file => getTrackAsync(Lavalink, file).Result ?? null);
    }

    /// <summary>
    /// We don't load useless m3u's and png's so lavalink won't spam the log with exceptions.
    /// </summary>
    public static bool extensionFilter(string s) {
        return s.EndsWith(".mp3") ||
               s.EndsWith(".flac") ||
               s.EndsWith(".wav") ||
               s.EndsWith(".ogg") ||
               s.EndsWith(".m4a") ||
               s.EndsWith(".opus") ||
               s.EndsWith(".webm") ||
               s.EndsWith(".aac") ||
               s.EndsWith(".wma") ||
               s.EndsWith(".aiff") ||
               s.EndsWith(".alac");
    }

    public static async Task<LavalinkTrack?> getTrackAsync(AudioService client, string file) {
        var tracks = await client.Tracks.LoadTracksAsync(file, TrackSearchMode.None);

        if (tracks.Track == null) {
            return null;
        }

        // if not error, fix the titles up

        LavalinkTrack result = tracks.Track ?? throw new InvalidOperationException();
        if (result.Title == "Unknown title") {
            result.GetType().GetProperty("Title")!.SetValue(result, Path.GetFileNameWithoutExtension(file));
        }

        if (result.Author == "Unknown artist") {
            // Not to mention that we are literally reflecting the Track object because the stupid authors thought
            // their autodetection was infallible thus they haven't provided a way to properly set the track's name which will be displayed.
            // The end-user is probably not very delighted at seeing "unknown author" or "unknown title" so we make a best-effort guess here.
            result.GetType().GetProperty("Author")!.SetValue(result, new FileInfo(file).Directory!.Name);
        }

        return result;
    }

    public void enableEQ() {
        eq = true;
        logger.info("Enabled EQ");
        Player.Filters.SetFilter(new EqualizerFilterOptions(new Equalizer {
            Band0 = 0.2f,
            Band1 = 0.2f,
            Band2 = 0.2f,
            Band3 = 0.2f,
            Band4 = 0.15f,
            Band5 = 0.12f,
            Band6 = 0.10f,
            Band7 = 0.05f,
            Band8 = 0.00f,
            Band9 = 0.00f,
            Band10 = -0.02f,
            Band11 = -0.02f,
            Band12 = -0.03f,
            Band13 = -0.04f,
            Band14 = -0.05f
        }));
        Player.Filters.CommitAsync().GetAwaiter().GetResult();
    }

    public void disableEQ() {
        eq = false;
        logger.info("Disabled EQ");
        Player.Filters.SetFilter(new EqualizerFilterOptions(Equalizer.Default));
        Player.Filters.CommitAsync().GetAwaiter().GetResult();
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