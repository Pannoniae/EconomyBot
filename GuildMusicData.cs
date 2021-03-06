using System.Collections.ObjectModel;
using System.Globalization;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using Emzi0767;
using Newtonsoft.Json;

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
    /// Gets the guild ID for this dataset.
    /// </summary>
    public string Identifier { get; }

    /// <summary>
    /// Gets whether the queue for this guild is shuffled.
    /// </summary>
    public bool isShuffled { get; private set; }

    /// <summary>
    /// Gets whether a track is currently playing.
    /// </summary>
    public bool isPlaying { get; private set; }

    /// <summary>
    /// Gets the playback volume for this guild.
    /// </summary>
    public int volume { get; private set; } = 100;

    /// <summary>
    /// Gets the current music queue.
    /// </summary>
    public IReadOnlyCollection<LavalinkTrack> Queue { get; }

    /// <summary>
    /// Gets the currently playing item.
    /// </summary>
    public LavalinkTrack NowPlaying { get; private set; }

    /// <summary>
    /// Is random jazz on?
    /// </summary>
    public bool isJazz { get; set; }

    /// <summary>
    /// Gets the channel in which the music is played.
    /// </summary>
    public DiscordChannel Channel => Player?.Channel;

    /// <summary>
    /// Gets or sets the channel in which commands are executed.
    /// </summary>
    public DiscordChannel CommandChannel { get; set; }

    private List<LavalinkTrack> QueueInternal { get; }
    private SemaphoreSlim QueueInternalLock { get; }
    private string QueueSerialized { get; set; }
    private DiscordGuild Guild { get; }
    private SecureRandom RNG { get; }
    public LavalinkExtension Lavalink { get; }
    public LavalinkGuildConnection Player { get; set; }

    /// <summary>
    /// Creates a new instance of playback data.
    /// </summary>
    /// <param name="guild">Guild to track data for.</param>
    /// <param name="rng">Cryptographically-secure random number generator implementation.</param>
    /// <param name="lavalink">Lavalink service.</param>
    /// <param name="redis">Redis service.</param>
    public GuildMusicData(DiscordGuild guild, SecureRandom rng, LavalinkExtension lavalink) {
        Guild = guild;
        RNG = rng;
        Lavalink = lavalink;
        Identifier = Guild.Id.ToString(CultureInfo.InvariantCulture);
        QueueInternalLock = new SemaphoreSlim(1, 1);
        QueueInternal = new List<LavalinkTrack>();
        Queue = new ReadOnlyCollection<LavalinkTrack>(QueueInternal);
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
        await Player.StopAsync();
    }

    /// <summary>
    /// Pauses the playback.
    /// </summary>
    public async Task PauseAsync() {
        if (Player == null || !Player.IsConnected)
            return;

        isPlaying = false;
        await Player.PauseAsync();
    }

    /// <summary>
    /// Resumes the playback.
    /// </summary>
    public async Task ResumeAsync() {
        if (Player == null || !Player.IsConnected)
            return;

        isPlaying = true;
        await Player.ResumeAsync();
    }

    /// <summary>
    /// Sets playback volume.
    /// </summary>
    public async Task SetVolumeAsync(int volume) {
        if (Player == null || !Player.IsConnected)
            return;

        await Player.SetVolumeAsync(volume);
        this.volume = volume;
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
            QueueInternal.Insert(0, NowPlaying);
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
    /// Shuffles the playback queue.
    /// </summary>
    public void Shuffle() {
        if (isShuffled)
            return;

        isShuffled = true;
        Reshuffle();
    }

    /// <summary>
    /// Reshuffles the playback queue.
    /// </summary>
    public void Reshuffle() {
        lock (QueueInternal) {
            QueueInternal.Sort(new Shuffler<LavalinkTrack>(RNG));
        }
    }

    /// <summary>
    /// Causes the queue to no longer be shuffled.
    /// </summary>
    public void StopShuffle() {
        isShuffled = false;
    }

    /// <summary>
    /// Enqueues a music track for playback.
    /// </summary>
    /// <param name="item">Music track to enqueue.</param>
    public void Enqueue(LavalinkTrack item) {
        lock (QueueInternal) {
            //if (this.RepeatMode == RepeatMode.All && QueueInternal.Count == 1) {
            //    QueueInternal.Insert(0, item);
            //}
            if (QueueInternal.Count == 1) {
                QueueInternal.Insert(0, item);
            }
            else if (!isShuffled || !QueueInternal.Any()) {
                QueueInternal.Add(item);
            }
            else if (isShuffled) {
                var index = RNG.Next(0, QueueInternal.Count);
                QueueInternal.Insert(index, item);
            }
        }
    }

    /// <summary>
    /// Dequeues next music item for playback.
    /// </summary>
    /// <returns>Dequeued item, or null if dequeueing fails.</returns>
    public LavalinkTrack? Dequeue() {
        lock (QueueInternal) {
            if (QueueInternal.Count == 0)
                return null;

            var item = QueueInternal[0];
            QueueInternal.RemoveAt(0);
            return item;

            //if (this.RepeatMode == RepeatMode.None) {
            //    var item = QueueInternal[0];
            //    QueueInternal.RemoveAt(0);
            //    return item;
            //}

            //if (this.RepeatMode == RepeatMode.Single) {
            //    var item = QueueInternal[0];
            //    return item;
            //}

            //if (this.RepeatMode == RepeatMode.All) {
            //    var item = QueueInternal[0];
            //    QueueInternal.RemoveAt(0);
            //    QueueInternal.Add(item);
            //    return item;
            //}
        }

        return null;
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
            return item;
        }
    }

    /// <summary>
    /// Creates a player for this guild.
    /// </summary>
    /// <returns></returns>
    public async Task CreatePlayerAsync(DiscordChannel channel) {
        if (Player != null && Player.IsConnected)
            return;

        var node = Lavalink.ConnectedNodes.Values.First();
        Player = await node.ConnectAsync(channel);

        if (volume != 100)
            await Player.SetVolumeAsync(volume);
        Player.PlaybackFinished += Player_PlaybackFinished;
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
        isPlaying = false;
        if (isJazz) {
            await AddToJazz();
        }

        await PlayHandlerAsync();
    }


    private async Task PlayHandlerAsync() {
        var itemN = Dequeue();
        if (itemN == null) {
            NowPlaying = default;
            return;
        }

        var item = itemN;
        NowPlaying = item;
        isPlaying = true;
        await Player.PlayAsync(item);
    }

    private async Task AddToJazz() {
        string path = "D:\\music\\fats";
        var rand = new Random();
        var files = Directory.GetFiles(path, "*");
        var randomFile = new FileInfo(files[rand.Next(files.Length)]);
        var tracks_ = await Lavalink.ConnectedNodes.Values.First().Rest
            .GetTracksAsync(randomFile);
        foreach (var track in tracks_.Tracks) {
            Enqueue(track);
            await Console.Out.WriteLineAsync($"Enqueued {track.Title} at {track.Uri}");
        }
    }

    public async Task StartJazz() {
        isJazz = true;
        string path = "D:\\music\\fats";
        var rand = new Random();
        var files = Directory.GetFiles(path, "*");
        var randomFiles = new FileInfo[6];
        for (int i = 0; i < 6; i++) {
            randomFiles[i] = new FileInfo(files[rand.Next(files.Length)]);
            var tracks_ = await Lavalink.ConnectedNodes.Values.First().Rest
                .GetTracksAsync(randomFiles[i]);
            foreach (var track in tracks_.Tracks) {
                Enqueue(track);
                await Console.Out.WriteLineAsync($"{DiscordEmoji.FromName(Lavalink.Client, ":cube:")} Enqueued {track.Title} at {track.Uri}");
            }
        }
    }
    
    public async Task<List<LavalinkLoadResult>> StartJazz(string searchTerm) {
        isJazz = true;
        string path = "D:\\music\\fats";
        var rand = new Random();
        var files = Directory.GetFiles(path, searchTerm); // max 9 lol
        var randomFiles = new List<FileInfo>();
        foreach (var file in files) {
            randomFiles.Add(new FileInfo(file));
        }

        var tracks = new List<LavalinkLoadResult>();
        foreach (var file in randomFiles) {
            tracks.Add(await Lavalink.ConnectedNodes.Values.First().Rest
                .GetTracksAsync(file));

        }

        return tracks;
        //foreach (var track in tracks_.Tracks) {
        //    Enqueue(track);
        //    await Console.Out.WriteLineAsync($"Enqueued {track.Title} at {track.Uri}");
        //}
    }

    public void StopJazz() {
        isJazz = false;
    }
}

public class Shuffler<T> : IComparer<T> {
    public SecureRandom RNG { get; }

    /// <summary>
    /// Creates a new shuffler.
    /// </summary>
    /// <param name="rng">Cryptographically-secure random number generator.</param>
    public Shuffler(SecureRandom rng) {
        RNG = rng;
    }

    /// <summary>
    /// Returns a random order for supplied items.
    /// </summary>
    /// <param name="x">First item.</param>
    /// <param name="y">Second item.</param>
    /// <returns>Random order for the items.</returns>
    public int Compare(T? x, T? y) {
        var val1 = RNG.Next();
        var val2 = RNG.Next();

        if (val1 > val2)
            return 1;
        if (val1 < val2)
            return -1;
        return 0;
    }
}