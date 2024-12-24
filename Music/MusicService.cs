using System.Collections.Concurrent;
using Lavalink4NET;
using Lavalink4NET.Events;
using Lavalink4NET.Events.Players;
using NetCord.Gateway;
using Soulseek;
using Spectre.Console;
using File = Soulseek.File;

namespace EconomyBot;

/// <summary>
/// Provides a persistent way of tracking music in various guilds.
/// </summary>
public sealed class MusicService {
    private AudioService Lavalink;
    private ConcurrentDictionary<ulong, GuildMusicData> MusicData;
    private readonly GatewayClient client;


    public SoulseekClient slsk;

    /// <summary>
    /// Creates a new instance of this music service.
    /// </summary>
    public MusicService(AudioService lavalink) {
        Lavalink = lavalink;
        MusicData = new ConcurrentDictionary<ulong, GuildMusicData>();
        client = Program.client;

        slsk = new SoulseekClient();
        slsk.ConnectAsync("jazzbot", "jazzbot").GetAwaiter().GetResult();
        slsk.ExcludedSearchPhrasesReceived += (sender, args) => {
            AnsiConsole.WriteLine("Excluded search phrases: ");
            foreach (var phrase in args) {
                AnsiConsole.WriteLine(phrase);
            }
        };


        Lavalink.StatisticsUpdated += playbackStarted;
        Lavalink.TrackEnded += (sender, e) => {
            // get queue for track
            var queue = MusicData[e.Player.GuildId].queue;
            return queue.Player_PlaybackFinished(sender, e);
        };
        Lavalink.TrackStarted += (sender, e) => {
            // get queue for track
            var queue = MusicData[e.Player.GuildId].queue;
            return queue.Player_PlaybackStarted(sender, e);
        };
        Lavalink.TrackException += Lavalink_TrackExceptionThrown;

        async Task playbackStarted(object o, StatisticsUpdatedEventArgs e) {
            AnsiConsole.WriteLine($"len/nodes: {e.Statistics.ConnectedPlayers}");
        }
    }

    private async Task Lavalink_TrackExceptionThrown(object sender, TrackExceptionEventArgs eventArgs) {
        if (e.Guild is null) {
            return;
        }

        await CommandChannel.SendMessageAsync(
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
    public void SaveDataForAsync(Guild guild) {
        MusicData.TryGetValue(guild.Id, out _);
    }

    /// <summary>
    /// Gets or creates a dataset for specified guild.
    /// </summary>
    /// <param name="guild">Guild to get or create dataset for.</param>
    /// <returns>Resulting dataset.</returns>
    public async Task<GuildMusicData> GetOrCreateDataAsync(Guild guild) {
        if (MusicData.TryGetValue(guild.Id, out var gmd))
            return gmd;

        gmd = MusicData.AddOrUpdate(guild.Id, new GuildMusicData(guild, Lavalink),
            (k, v) => v);

        gmd.setupWebhooks();

        return gmd;
    }
}

public record SLSKResult(SearchResponse response, File file) {
}