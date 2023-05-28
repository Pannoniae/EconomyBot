using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;

namespace EconomyBot;

public class MusicQueue {
    public MusicQueue(GuildMusicData guildMusic) {
        Queue = new List<Track>();
        autoQueue = new List<Track>();
        artistQueue = new List<string>();
        gmd = guildMusic;
    }

    public GuildMusicData gmd;

    /// <summary>
    /// Gets the currently playing item.
    /// </summary>
    public LavalinkTrack? NowPlaying { get; private set; }

    /// <summary>
    /// Gets the currently playing item.
    /// </summary>
    public string? NowPlayingArtist { get; private set; }

    /// <summary>
    /// Gets the current manual music queue.
    /// </summary>
    public List<Track> Queue { get; }

    /// <summary>
    /// Gets the current auto-played music queue.
    /// </summary>
    public List<Track> autoQueue { get; }

    /// <summary>
    /// The things being played right now. "_fats" is special-cased to the great collection.
    /// </summary>
    public List<string> artistQueue { get; }

    /// <summary>
    /// Stops the playback.
    /// </summary>
    public async Task StopAsync(GuildMusicData guildMusicData) {
        if (guildMusicData.Player == null || !guildMusicData.Player.IsConnected)
            return;

        NowPlaying = default;
        NowPlayingArtist = default;
        await guildMusicData.Player.StopAsync();
    }

    /// <summary>
    /// Begins playback.
    /// </summary>
    public async Task PlayAsync() {
        if (gmd.Player == null || !gmd.Player.IsConnected)
            return;

        if (NowPlaying?.TrackString == null)
            await gmd.queue.PlayHandlerAsync();
    }

    /// <summary>
    /// Restarts current track.
    /// </summary>
    public async Task RestartAsync() {
        if (gmd.Player == null || !gmd.Player.IsConnected)
            return;

        if (NowPlaying.TrackString == null)
            return;

        insert(0, new Track(NowPlaying, NowPlayingArtist));
        await gmd.Player.StopAsync();
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
    /// Get the combined queue of the bot. WARNING: removing/modifying this collection does nothing. Don't do that.
    /// Modify the individual queues instead.
    /// </summary>
    public List<Track> getCombinedQueue() {
        var combinedQueue = new List<Track>();
        combinedQueue.AddRange(Queue);
        combinedQueue.AddRange(autoQueue);
        return combinedQueue;
    }

    public async Task PlayHandlerAsync() {
        var itemN = Dequeue();
        if (itemN == null) {
            NowPlaying = default;
            NowPlayingArtist = default;
            return;
        }

        var item = itemN;
        NowPlaying = item.track;
        NowPlayingArtist = item.artist;
        await gmd.Player.PlayAsync(item.track);
    }

    public async Task addToJazz(string artist, string path) {
        var rand = new Random();
        var files = Directory.GetFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true });
        var randomFile = new FileInfo(files[rand.Next(files.Length)]);
        var tracks_ = await gmd.Lavalink.GetIdealNodeConnection().Rest
            .GetTracksAsync(randomFile);
        foreach (var track in tracks_.Tracks) {
            Enqueue(track, artist);
            await Console.Out.WriteLineAsync($"Enqueued {track.Title} at {track.Uri}");
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
        var max = GuildMusicData.artistWeights.Values.Max();
        // return the online artist with the max. frequency of all played since we don't know the number of total songs
        var randomElement =
            artistQueue.randomElementByWeight(e => GuildMusicData.artistWeights.TryGetValue(e, out var element) ? element : max);
        if (GuildMusicData.artistMappings.TryGetValue(randomElement, out var artist)) {
            await addToJazz(randomElement, artist.path);
        }

        else {
            await gmd.AddToRandom(randomElement);
        }
    }

    public async Task seedQueue() {
        // don't overqueue
        while (autoQueue.Count < 6) {
            await growQueue();
        }
    }

    public async Task Player_PlaybackFinished(LavalinkGuildConnection con, TrackFinishEventArgs e) {
        await Task.Delay(500);
        if (artistQueue.Any() && autoQueue.Count < 6) {
            await seedQueue();
        }

        await PlayHandlerAsync();
    }

    public async Task Player_PlaybackStarted(LavalinkGuildConnection sender, TrackStartEventArgs e) {
        if (NowPlayingArtist != null) {
            await gmd.Player.SetVolumeAsync(gmd.effectiveVolume);
        }
    }
}