using System.Reflection;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using NLog;
using SpotifyAPI.Web;

namespace EconomyBot;
// This file is a part of Music Turret project.
// 
// Copyright (C) 2018-2021 Emzi0767
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

/// <summary>
/// Represents data for the music playback in a discord guild.
/// </summary>
public sealed class GuildMusicData {
    private WebhookCache webhookCache;
    
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Is EQ enabled?
    /// </summary>
    private bool eq;

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
    public LavalinkExtension Lavalink { get; }
    public LavalinkGuildConnection Player { get; set; }

    public LavalinkNodeConnection Node { get; }

    // TODO implement a *proper* music weighting system

    public static readonly Dictionary<string, Artist> artistMappings = new() {
        { "_fats", new Artist("G:\\music\\Fats Waller", 1.5) },
        { "ella mae morse", new Artist("G:\\music\\Ella Mae Morse", 1.2) },
        { "slim gaillard", new Artist("G:\\music\\Slim Gaillard", 1.0) },
        { "louis jordan", new Artist("G:\\music\\Louis Jordan", 1.0) },
        { "caravan palace", new Artist("G:\\music\\Caravan Palace", 1.0, 2) },
        { "tape five", new Artist("G:\\music\\Tape Five", 1.0) },
        { "caro emerald", new Artist("G:\\music\\Caro Emerald", 1.0) },
        { "chuck berry", new Artist("G:\\music\\Chuck Berry", 1.0, 0.5) }, // most of this is trash
        { "jamie berry", new Artist("G:\\music\\Jamie Berry", 0.8) },
        { "sim gretina", new Artist("G:\\music\\Sim Gretina", 0.8, 0) }, // too much earrape
        { "freshly squeezed", new Artist("G:\\music\\Freshly Squeezed Music", 0.8, 0.5) },
        { "puppini sisters", new Artist("G:\\music\\Puppini Sisters", 1.0) },
        { "11 acorn lane", new Artist("G:\\music\\11 Acorn Lane", 1.0) },
        { "electric swing circus", new Artist("G:\\music\\Electric Swing Circus", 1.0) },
        { "the speakeasies swing band", new Artist("G:\\music\\The Speakeasies Swing Band", 1.0) },
    };

    public static readonly Dictionary<string, double> artistWeights = new();

    /// <summary>
    /// Gets the actual volume to set.
    /// </summary>
    public int effectiveVolume =>
        (int)(volume * (artistMappings.GetValueOrDefault(queue.NowPlayingArtist ?? "missing")?.volume ?? 1));

    public double artistVolume => artistMappings.GetValueOrDefault(queue.NowPlayingArtist ?? "missing")?.volume ?? 1;

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
        queue = new(this);

        foreach (var artist in artistMappings) {
            // get the count of files at the directory
            int fCount = Directory
                .GetFiles(artist.Value.path, "*", new EnumerationOptions { RecurseSubdirectories = true })
                .Length;
            artistWeights[artist.Key] = fCount * artist.Value.weight;
        }

        webhookCache = new WebhookCache(Guild);

        logger.Info("Initialised artist weights.");
    }

    public async Task setupWebhooks() {
        webhookCache.setup();
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
    public async Task SetVolumeAsync(int volume) {
        if (Player == null || !Player.IsConnected)
            return;

        this.volume = volume;
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
        if (queue.NowPlaying?.TrackString == null)
            return TimeSpan.Zero;

        return Player.CurrentState.PlaybackPosition;
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
            logger.Error("Error loading random track");
        }

        queue.Enqueue(tracks.First(), artist);
    }

    public async Task<IEnumerable<LavalinkLoadResult>> StartJazz(string searchTerm) {
        return artistMappings.SelectMany(
                artist => Directory.GetFiles(artist.Value.path, searchTerm,
                    new EnumerationOptions { RecurseSubdirectories = true }))
            .Select(file => new FileInfo(file))
            .Select(file => getTracksAsync(Node.Rest, file).Result);
    }
    
    public async Task<LavalinkLoadResult> getTracksAsync(LavalinkRestClient client, FileInfo file) {
        var tracks = await client.GetTracksAsync(file);
        foreach (var track in tracks.Tracks.Where(track => track.Title == "Unknown title")) {
            track.GetType().GetProperty("Title")!.SetValue(track, file.Name);
        }

        return tracks;
    }

    public void enableEQ() {
        eq = true;
        logger.Info("Enabled EQ");
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
        logger.Info("Disabled EQ");
        Player.ResetEqualizerAsync();
    }
}

public record Artist(string path, double volume, double weight = 1.0);

public record Track(LavalinkTrack track, string? artist);

public static class IEnumerableExtensions {
    public static T randomElementByWeight<T>(this IEnumerable<T> sequence, Func<T, double> weightSelector) {
        double totalWeight = sequence.Sum(weightSelector);
        // The weight we are after...
        double itemWeightIndex = new Random().NextDouble() * totalWeight;
        double currentWeightIndex = 0;

        foreach (var item in from weightedItem in sequence
                 select new { Value = weightedItem, Weight = weightSelector(weightedItem) }) {
            currentWeightIndex += item.Weight;

            // If we've hit or passed the weight we are after for this item then it's the one we want....
            if (currentWeightIndex >= itemWeightIndex)
                return item.Value;
        }

        return default;
    }
}