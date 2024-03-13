using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using DisCatSharp.Lavalink.EventArgs;
using EconomyBot.Logging;

namespace EconomyBot;

public class MusicQueue(GuildMusicData guildMusic) {
    private static readonly Logger logger = Logger.getClassLogger("MusicQueue");

    /// <summary>
    /// Gets the currently playing item.
    /// </summary>
    public Track? NowPlaying { get; private set; }

    /// <summary>
    /// It's like <see cref="NowPlaying"/> but doesn't get cleared when playback stops. Used for implementing repeat.
    /// </summary>
    public Track? repeatHolder { get; private set; }

    /// <summary>
    /// Gets the current manual music queue.
    /// </summary>
    public List<Track> Queue { get; } = [];

    public bool repeatQueue { get; set; } = false;

    public bool earrapeMode { get; set; } = false;

    /// <summary>
    /// Gets the current auto-played music queue.
    /// </summary>
    public List<Track> autoQueue { get; } = [];

    /// <summary>
    /// Playback history. Used for analytics.... I mean stopping the awful songs from playing.
    /// Yes I know this should be a deque or a linked list shut up
    /// </summary>
    public List<Track> history { get; } = [];

    /// <summary>
    /// The things being played right now. "_fats" is special-cased to the great collection.
    /// </summary>
    public List<string> artistQueue { get; } = [];

    /// <summary>
    /// Stops the playback.
    /// </summary>
    public async Task StopAsync() {
        if (guildMusic.Player == null || !guildMusic.Player.IsConnected)
            return;

        NowPlaying = default;
        await guildMusic.Player.StopAsync();
    }

    /// <summary>
    /// Begins playback.
    /// </summary>
    public async Task PlayAsync() {
        if (guildMusic.Player == null || !guildMusic.Player.IsConnected)
            return;

        if (NowPlaying == default)
            await guildMusic.queue.PlayHandlerAsync();
    }

    /// <summary>
    /// Restarts current track.
    /// </summary>
    public async Task RestartAsync() {
        if (guildMusic.Player == null || !guildMusic.Player.IsConnected)
            return;

        if (NowPlaying == default)
            return;

        insert(0, NowPlaying);
        await guildMusic.Player.StopAsync();
    }

    /// <summary>
    /// Play the given track and automatically decide which queue it goes in.
    /// </summary>
    /// <param name="track">The track to be played.</param>
    private void play(Track track) {
        // autoplaylist
        if (track.artist != null) {
            autoQueue.Add(track);
            history.Add(track);
        }
        // manually added
        else {
            Queue.Add(track);
        }
    }

    /// <summary>
    /// Play the given track and automatically decide which queue it goes in.
    /// </summary>
    /// <param name="track">The track to be played.</param>
    private void insert(int idx, Track track) {
        // autoplaylist
        if (track.artist != null) {
            autoQueue.Insert(0, track);
            history.Insert(0, track);
        }
        // manually added
        else {
            Queue.Insert(0, track);
        }
    }

    /// <summary>
    /// Empties the playback queue.
    /// </summary>
    /// <returns>Number of cleared items.</returns>
    public int EmptyQueue() {
        var itemCount = Queue.Count;
        var itemCount2 = autoQueue.Count;
        Queue.Clear();
        autoQueue.Clear();
        history.Clear();
        return itemCount + itemCount2;
    }

    /// <summary>
    /// Enqueues a music track for playback.
    /// </summary>
    /// <param name="item">Music track to enqueue.</param>
    public void Enqueue(LavalinkTrack item, string? artist = null) {
        play(new Track(item, artist));
    }

    /// <summary>
    /// Dequeues next music item for playback.
    /// </summary>
    /// <returns>Dequeued item, or null if dequeueing fails.</returns>
    public Track? Dequeue() {
        Track item;
        // there is nothing manually playing
        if (Queue.Count == 0) {
            // there is nothing playing in the autoplaylist either, return null
            if (autoQueue.Count == 0) {
                return null;
            }

            item = autoQueue[0];
            autoQueue.RemoveAt(0);
            return item;
        }
        // there are manual tracks, play them first
        else {
            item = Queue[0];
            Queue.RemoveAt(0);
            repeatHolder = item;
            return item;
        }
    }

    /// <summary>
    /// Removes a track from the queue.
    /// </summary>
    /// <param name="index">Index of the track to remove.</param>
    public LavalinkTrack? Remove(int index) {
        var combinedQueue = getCombinedQueue();
        if (index < 0 || index >= combinedQueue.Count) {
            return null;
        }

        var item = combinedQueue[index];
        var queue = (item.artist == null) switch {
            true => Queue,
            false => autoQueue
        };
        queue.Remove(item);
        if (item.artist != null) {
            history.Remove(item);
        }

        return item.track;
    }

    /// <summary>
    /// Get the combined queue of the bot. Removing/modifying this collection does nothing. So you can't do that.
    /// Modify the individual queues instead and call this method again.
    /// </summary>
    public List<Track> getCombinedQueue() {
        var combinedQueue = new List<Track>();
        combinedQueue.AddRange(Queue);
        combinedQueue.AddRange(autoQueue);
        return combinedQueue;
    }

    public async Task PlayHandlerAsync() {
        var nextTrack = Dequeue();
        if (nextTrack == default) {
            NowPlaying = default;
            return;
        }

        NowPlaying = nextTrack;
        if (earrapeMode) {
            var length = nextTrack.track.Info.Length;
            await guildMusic.Player.PlayPartialAsync(nextTrack.track, TimeSpan.Zero, length - TimeSpan.FromSeconds(20));
        }
        else {
            await guildMusic.Player.PlayAsync(nextTrack.track);
        }
    }

    public void addToJazz(Track track) {
        Enqueue(track.track, track.artist);
        logger.info($"Enqueued {track.track.Info.Title} at {track.track.Info.Uri}");
    }

    public void addAllToQueue() {
        foreach (var key in GuildMusicData.artistMappings.Keys) {
            artistQueue.Add(key);
        }
    }

    public void addToQueue(string music) {
        artistQueue.Add(music);
    }

    public void clearQueue() {
        artistQueue.Clear();
    }

    public async Task growQueue() {
        // return the online artist with the max. frequency of all played since we don't know the number of total songs
        var nextSong = await selectNextPlayedSong();
        addToJazz(nextSong);
    }

    private async Task<Track> selectNextPlayedSong() {
        // beginning
        beginning:
        var max = GuildMusicData.artistWeights.Values.Max();
        var artistName = artistQueue.randomElementByWeight(e => {
            var weight = GuildMusicData.artistWeights.GetValueOrDefault(e, max);
            GuildMusicData.artistMappings.TryGetValue(e, out var artist);
            if (history.Select(i => i.artist).Contains(e)) {
                weight *= artist!.historyRepeatPenalty;
            }

            if (autoQueue.FirstOrDefault()?.artist == e) {
                weight *= artist!.doubleRepeatPenalty;
            }

            return weight;
        });
        var artist = GuildMusicData.artistMappings[artistName];
        // not found
        var path = GuildMusicData.getPath(artist.path);
        if (!Path.Exists(path)) {
            goto beginning;
        }

        var nextSong = await selectNextSong(artist, artistName);

        var historyHasRemix =
            history.Any(h => h.track.Info.Title.Contains("remix", StringComparison.CurrentCultureIgnoreCase));
        // remix filter
        if (nextSong.track.Info.Title.Contains("remix", StringComparison.CurrentCultureIgnoreCase) && historyHasRemix) {
            // 90% reject
            if (Random.Shared.NextDouble() < 0.9) {
                logger.info("Found duplicate remix, re-rolling...");
                goto beginning;
            }
        }

        return nextSong;
    }

    private async Task<Track> selectNextSong(Artist artist, string artistName) {
        var path = GuildMusicData.getPath(artist.path);
        var rand = new Random();
        var files = Directory.GetFiles(path, "*",
            new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive });
        beginning:
        var randomFile = files[rand.Next(files.Length)];
        var tracks_ = await GuildMusicData.getTracksAsync(guildMusic.Node, randomFile);
        if (tracks_ == null) {
            // retry if not found
            goto beginning;
        }
        return new Track(tracks_, artistName);
    }

    public async Task seedQueue() {
        // don't overqueue
        while (autoQueue.Count < 6) {
            await growQueue();
        }
    }

    public async Task Player_PlaybackFinished(LavalinkGuildPlayer con, LavalinkTrackEndedEventArgs e) {
        // requeue if there are items in the queue
        if (repeatQueue && Queue.Count != 0 && repeatHolder != null) {
            Queue.Add(repeatHolder);
        }

        await Task.Delay(500);
        if (artistQueue.Count != 0 && autoQueue.Count < 6) {
            await seedQueue();
        }

        if (history.Count > 20) {
            history.RemoveAt(0);
        }

        await PlayHandlerAsync();
    }

    public async Task Player_PlaybackStarted(LavalinkGuildPlayer sender, LavalinkTrackStartedEventArgs e) {
        await sender.SetVolumeAsync(guildMusic.effectiveVolume);
    }
}