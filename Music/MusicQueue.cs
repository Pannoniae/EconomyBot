using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
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
    public List<Track> Queue { get; } = new();

    public bool repeatQueue { get; set; } = false;

    public bool earrapeMode { get; set; } = false;

    /// <summary>
    /// Gets the current auto-played music queue.
    /// </summary>
    public List<Track> autoQueue { get; } = new();

    /// <summary>
    /// The things being played right now. "_fats" is special-cased to the great collection.
    /// </summary>
    public List<string> artistQueue { get; } = new();

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
        return itemCount + itemCount2;
    }

    /// <summary>
    /// Enqueues a music track for playback.
    /// </summary>
    /// <param name="item">Music track to enqueue.</param>
    public void  Enqueue(LavalinkTrack item, string? artist = null) {
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
        if (item.artist != null) {
            autoQueue.Remove(item);
            return item.track;
        }
        else {
            Queue.Remove(item);
            return item.track;
        }
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
        var itemN = Dequeue();
        if (itemN == default) {
            NowPlaying = default;
            return;
        }

        NowPlaying = itemN;
        if (earrapeMode) {
            var length = itemN.track.Length;
            await guildMusic.Player.PlayPartialAsync(itemN.track, TimeSpan.Zero, length - TimeSpan.FromSeconds(20));
        }
        else {
            await guildMusic.Player.PlayAsync(itemN.track);
        }
    }

    public async Task addToJazz(string artist, string path) {
        var rand = new Random();
        var files = Directory.GetFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true });
        var randomFile = new FileInfo(files[rand.Next(files.Length)]);
        var tracks_ = await GuildMusicData.getTracksAsync(guildMusic.Node.Rest, randomFile);
        foreach (var track in tracks_.Tracks) {
            Enqueue(track, artist);
            logger.info($"Enqueued {track.Title} at {track.Uri}");
        }
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
        var randomElement =
            selectNextSong();
        if (GuildMusicData.artistMappings.TryGetValue(randomElement, out var artist)) {
            await addToJazz(randomElement, artist.path);
        }

        else {
            await guildMusic.AddToRandom(randomElement);
        }
    }

    private string selectNextSong() {
        var max = GuildMusicData.artistWeights.Values.Max();
        return artistQueue.randomElementByWeight(e => {
            var weight = GuildMusicData.artistWeights.TryGetValue(e, out var element) ? element : max;
            GuildMusicData.artistMappings.TryGetValue(e, out var artist);
            if (autoQueue.Select(i => i.artist).Contains(e)) {
                weight *= artist!.repeatPenalty;
            }

            if (autoQueue.FirstOrDefault()?.artist == e) {
                weight *= artist!.doubleRepeatPenalty;
            }

            return weight;
        });
    }

    public async Task seedQueue() {
        // don't overqueue
        while (autoQueue.Count < 6) {
            await growQueue();
        }
    }

    public async Task Player_PlaybackFinished(LavalinkGuildConnection con, TrackFinishEventArgs e) {
        // requeue if there are items in the queue
        if (repeatQueue && Queue.Count != 0 && repeatHolder != null) {
            Queue.Add(repeatHolder);
        }
        await Task.Delay(500);
        if (artistQueue.Count != 0 && autoQueue.Count < 6) {
            await seedQueue();
        }

        await PlayHandlerAsync();
    }

    public async Task Player_PlaybackStarted(LavalinkGuildConnection sender, TrackStartEventArgs e) {
        await guildMusic.Player.SetVolumeAsync(guildMusic.effectiveVolume);
    }
}