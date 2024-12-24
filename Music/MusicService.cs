using System.Collections.Concurrent;
using DisCatSharp;
using DisCatSharp.Entities;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players;
using Lavalink4NET.Rest.Entities.Tracks;
using Soulseek;
using File = Soulseek.File;

namespace EconomyBot;

/// <summary>
/// Provides a persistent way of tracking music in various guilds.
/// </summary>
public sealed class MusicService {
    private IAudioService Lavalink;
    private ConcurrentDictionary<ulong, GuildMusicData> MusicData;
    private readonly DiscordClient client;


    public SoulseekClient slsk;

    /// <summary>
    /// Creates a new instance of this music service.
    /// </summary>
    public MusicService(IAudioService lavalink) {
        Lavalink = lavalink;
        MusicData = new ConcurrentDictionary<ulong, GuildMusicData>();
        client = Program.client;

        slsk = new SoulseekClient();
        slsk.ConnectAsync("jazzbot", "jazzbot").GetAwaiter().GetResult();
        slsk.ExcludedSearchPhrasesReceived += (sender, args) => {
            Spectre.Console.AnsiConsole.WriteLine("Excluded search phrases: ");
            foreach (var phrase in args) {
                Spectre.Console.AnsiConsole.WriteLine(phrase);
            }
        };

        Lavalink.StatisticsUpdated += playbackStarted;

        Lavalink.TrackEnded += async (con, e) => (await GetOrCreateDataAsync(client.Guilds[e.Player.GuildId])).queue.Player_PlaybackFinished(con, e);
        Lavalink.TrackStarted += async(sender, e) => (await GetOrCreateDataAsync(client.Guilds[e.Player.GuildId])).queue.Player_PlaybackStarted(sender, e);
        Lavalink.TrackException += Lavalink_TrackExceptionThrown;

        async Task playbackStarted(object o, StatisticsUpdatedEventArgs e) {
            await Console.Out.WriteLineAsync($"len/nodes: {e.Statistics.ConnectedPlayers}");
        }
    }

    private async Task Lavalink_TrackExceptionThrown(object sender, TrackExceptionEventArgs e) {
        if (e.Player.State is PlayerState.Destroyed) {
            return;
        }

        await (await GetOrCreateDataAsync(client.Guilds[e.Player.GuildId])).CommandChannel.SendMessageAsync(
            $"{Program.cube} A problem occured while playing {e.Track.ToLimitedTrackString()}:\n{e.Exception}");
    }


    public async Task<List<SLSKResult>> getSLSK(string searchTerm) {
        var options = new SearchOptions(searchTimeout: 6000, responseFilter: noLocked);
        var result = await slsk.SearchAsync(new SearchQuery(searchTerm), options: options);

        var results = new List<SLSKResult>();
        // for each response, aggregate tracks with the result it belongs to
        foreach (var r in result.Responses) {
            foreach (var f in r.Files) {
                results.Add(new SLSKResult(r, f));
            }
        }
        // time to sort by speed! higher speed goes first
        results.Sort((a, b) => {
            var aSpeed = a.response.UploadSpeed;
            var bSpeed = b.response.UploadSpeed;
            if (aSpeed == bSpeed) return 0;
            return bSpeed - aSpeed;
        });

        return results.Take(50).ToList();
    }

    private static bool noLocked(SearchResponse arg) {
        return arg.HasFreeUploadSlot && arg.LockedFileCount == 0;
    }

    /// <summary>
    /// Saves data for specified guild.
    /// </summary>
    /// <param name="guild">Guild to save data for.</param>
    /// <returns></returns>
    public void SaveDataForAsync(DiscordGuild guild) {
        MusicData.TryGetValue(guild.Id, out _);
    }

    /// <summary>
    /// Gets or creates a dataset for specified guild.
    /// </summary>
    /// <param name="guild">Guild to get or create dataset for.</param>
    /// <returns>Resulting dataset.</returns>
    public async Task<GuildMusicData> GetOrCreateDataAsync(DiscordGuild guild) {
        if (MusicData.TryGetValue(guild.Id, out var gmd))
            return gmd;

        gmd = MusicData.AddOrUpdate(guild.Id, new GuildMusicData(guild, Lavalink),
            (k, v) => v);

        gmd.setupWebhooks();

        return gmd;
    }

    /// <summary>
    /// Loads tracks from specified URL.
    /// </summary>
    /// <param name="uri">URL to load tracks from.</param>
    /// <returns>Loaded tracks.</returns>
    public Task<TrackLoadResult> GetTracksAsync(Uri uri)
        => Lavalink.Tracks.LoadTracksAsync(uri.ToString(), TrackSearchMode.YouTube).AsTask();

    public Task<TrackLoadResult> GetTracksAsync(string search)
        => Lavalink.Tracks.LoadTracksAsync(search, TrackSearchMode.YouTube).AsTask();
}

public record SLSKResult(SearchResponse response, File file) {
}