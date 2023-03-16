using System.Collections.ObjectModel;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
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
    /// <summary>
    /// Is EQ enabled?
    /// </summary>
    private bool eq;

    /// <summary>
    /// Gets the playback volume for this guild.
    /// </summary>
    public int volume { get; private set; } = 100;

    /// <summary>
    /// Gets the current music queue.
    /// </summary>
    public IReadOnlyCollection<Track> Queue { get; }

    /// <summary>
    /// Gets the currently playing item.
    /// </summary>
    public LavalinkTrack? NowPlaying { get; private set; }

    /// <summary>
    /// Gets the currently playing item.
    /// </summary>
    public string? NowPlayingArtist { get; private set; }

    /// <summary>
    /// The things being played right now. "_fats" is special-cased to the great collection.
    /// </summary>
    public List<string> artistQueue { get; }


    /// <summary>
    /// Gets or sets the channel in which commands are executed.
    /// </summary>
    public DiscordChannel CommandChannel { get; set; }

    private List<Track> QueueInternal { get; }
    private SemaphoreSlim QueueInternalLock { get; }
    private DiscordGuild Guild { get; }
    public LavalinkExtension Lavalink { get; }
    public LavalinkGuildConnection Player { get; set; }

    public LavalinkNodeConnection Node { get; }

    private static readonly Dictionary<string, Artist> artistMappings = new() {
        { "_fats", new Artist("G:\\music\\fats", 1.2) },
        { "ella", new Artist("G:\\music\\ella", 0.8) },
        { "slim", new Artist("G:\\music\\slim", 0.8) },
        { "jordan", new Artist("G:\\music\\jordan", 0.6) },
        { "caravan palace", new Artist("G:\\music\\caravan palace", 0.6) },
        { "tape5", new Artist("G:\\music\\tape5", 0.6) }
    };

    public static readonly Dictionary<string, double> artistWeights = new();

    /// <summary>
    /// Gets the actual volume to set.
    /// </summary>
    public int effectiveVolume =>
        (int)(volume * (artistMappings.GetValueOrDefault(NowPlayingArtist ?? "missing")?.volume ?? 1));

    public double artistVolume => artistMappings.GetValueOrDefault(NowPlayingArtist ?? "missing")?.volume ?? 1;

    /// <summary>
    /// Creates a new instance of playback data.
    /// </summary>
    /// <param name="guild">Guild to track data for.</param>
    /// <param name="rng">Cryptographically-secure random number generator implementation.</param> 
    /// <param name="lavalink">Lavalink service.</param>
    /// <param name="redis">Redis service.</param>
    /// <param name="node">The Lavalink node this guild is connected to.</param>
    public GuildMusicData(DiscordGuild guild, LavalinkExtension lavalink, LavalinkNodeConnection node) {
        Node = node;
        Guild = guild;
        Lavalink = lavalink;
        QueueInternalLock = new SemaphoreSlim(1, 1);
        QueueInternal = new List<Track>();
        Queue = new ReadOnlyCollection<Track>(QueueInternal);
        artistQueue = new List<string>();

        foreach (var artist in artistMappings) {
            // get the count of files at the directory
            int fCount = Directory
                .GetFiles(artist.Value.path, "*", new EnumerationOptions { RecurseSubdirectories = true })
                .Length;
            artistWeights[artist.Key] = fCount;
        }

        Console.Out.WriteLine("Initialised artist weights.");
    }

    /// <summary>
    /// Begins playback.
    /// </summary>
    public async Task PlayAsync() {
        if (Player == null || !Player.IsConnected)
            return;

        if (NowPlaying?.TrackString == null)
            await PlayHandlerAsync();
    }

    /// <summary>
    /// Stops the playback.
    /// </summary>
    public async Task StopAsync() {
        if (Player == null || !Player.IsConnected)
            return;

        NowPlaying = default;
        NowPlayingArtist = default;
        await Player.StopAsync();
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
    /// Restarts current track.
    /// </summary>
    public async Task RestartAsync() {
        if (Player == null || !Player.IsConnected)
            return;

        if (NowPlaying.TrackString == null)
            return;

        await QueueInternalLock.WaitAsync();
        try {
            QueueInternal.Insert(0, new Track(NowPlaying, NowPlayingArtist));
            await Player.StopAsync();
        }
        finally {
            QueueInternalLock.Release();
        }
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
    /// Empties the playback queue.
    /// </summary>
    /// <returns>Number of cleared items.</returns>
    public int EmptyQueue() {
        lock (QueueInternal) {
            var itemCount = QueueInternal.Count;
            QueueInternal.Clear();
            return itemCount;
        }
    }

    /// <summary>
    /// Enqueues a music track for playback.
    /// </summary>
    /// <param name="item">Music track to enqueue.</param>
    public void Enqueue(LavalinkTrack item, string? artist = null) {
        lock (QueueInternal) {
            if (QueueInternal.Count == 1) {
                QueueInternal.Insert(0, new Track(item, artist));
            }
            else {
                QueueInternal.Add(new Track(item, artist));
            }
        }
    }

    /// <summary>
    /// Dequeues next music item for playback.
    /// </summary>
    /// <returns>Dequeued item, or null if dequeueing fails.</returns>
    public Track? Dequeue() {
        lock (QueueInternal) {
            if (QueueInternal.Count == 0)
                return null;

            var item = QueueInternal[0];
            QueueInternal.RemoveAt(0);
            return item;
        }
    }

    /// <summary>
    /// Removes a track from the queue.
    /// </summary>
    /// <param name="index">Index of the track to remove.</param>
    public LavalinkTrack? Remove(int index) {
        lock (QueueInternal) {
            if (index < 0 || index >= QueueInternal.Count)
                return null;

            var item = QueueInternal[index];
            QueueInternal.RemoveAt(index);
            return item.track;
        }
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

        Player.PlaybackFinished += Player_PlaybackFinished;
        Player.PlaybackStarted += Player_PlaybackStarted;
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
        if (NowPlaying.TrackString == null)
            return TimeSpan.Zero;

        return Player.CurrentState.PlaybackPosition;
    }

    private async Task Player_PlaybackFinished(LavalinkGuildConnection con, TrackFinishEventArgs e) {
        await Task.Delay(500);
        if (artistQueue.Any() && Queue.Count < 6) {
            await growQueue();
        }

        await PlayHandlerAsync();
    }

    private async Task Player_PlaybackStarted(LavalinkGuildConnection sender, TrackStartEventArgs e) {
        if (NowPlayingArtist != null) {
            await Player.SetVolumeAsync(effectiveVolume);
        }
    }


    private async Task PlayHandlerAsync() {
        var itemN = Dequeue();
        if (itemN == null) {
            NowPlaying = default;
            NowPlayingArtist = default;
            return;
        }

        var item = itemN;
        NowPlaying = item.track;
        NowPlayingArtist = item.artist;
        await Player.PlayAsync(item.track);
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
            await Console.Out.WriteLineAsync("Error loading random track");
        }

        Enqueue(tracks.First(), artist);
    }

    private async Task addToJazz(string artist, string path) {
        var rand = new Random();
        var files = Directory.GetFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true });
        var randomFile = new FileInfo(files[rand.Next(files.Length)]);
        var tracks_ = await Lavalink.ConnectedNodes.Values.First().Rest
            .GetTracksAsync(randomFile);
        foreach (var track in tracks_.Tracks) {
            //Console.Out.WriteLine(tracks_.Tracks.Count());
            Enqueue(track, artist);
            await Console.Out.WriteLineAsync($"Enqueued {track.Title} at {track.Uri}");
        }
    }

    public void addAllToQueue() {
        foreach (var key in artistMappings.Keys) {
            artistQueue.Add(key);
        }
    }

    public void addToQueue(string music) {
        artistQueue.Add(music);
    }

    public void clearQueue() {
        artistQueue.Clear();
    }

    private async Task growQueue() {
        var max = artistWeights.Values.Max();
        // return the online artist with the max. frequency of all played since we don't know the number of total songs
        var randomElement =
            artistQueue.randomElementByWeight(e => artistWeights.ContainsKey(e) ? artistWeights[e] : max);
        if (artistMappings.ContainsKey(randomElement)) {
            await addToJazz(randomElement, artistMappings[randomElement].path);
        }

        else {
            await AddToRandom(randomElement);
        }
    }

    public async Task seedQueue() {
        // don't overqueue
        for (var i = 0; i < 6; i++) {
            while (Queue.Count < 6) {
                await growQueue();
            }
        }
    }

    public async Task<IEnumerable<LavalinkLoadResult>> StartJazz(string searchTerm) {
        return artistMappings.SelectMany(
                artist => Directory.GetFiles(artist.Value.path, searchTerm,
                    new EnumerationOptions { RecurseSubdirectories = true }))
            .Select(file => new FileInfo(file))
            .Select(file => Node.Rest.GetTracksAsync(file).Result);
    }

    public void enableEQ() {
        eq = true;
        Console.Out.WriteLine("Enabled EQ");
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
        Console.Out.WriteLine("Disabled EQ");
        Player.ResetEqualizerAsync();
    }
}

public record Artist(string path, double volume);

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