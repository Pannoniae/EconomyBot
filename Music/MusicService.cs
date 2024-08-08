using System.Collections.Concurrent;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.EventArgs;
using Soulseek;
using File = Soulseek.File;

namespace EconomyBot;

/// <summary>
/// Provides a persistent way of tracking music in various guilds.
/// </summary>
public sealed class MusicService {
    private LavalinkExtension Lavalink;
    private ConcurrentDictionary<ulong, GuildMusicData> MusicData;
    private readonly DiscordClient client;


    public SoulseekClient slsk;

    private LavalinkSession node { get; }

    /// <summary>
    /// Creates a new instance of this music service.
    /// </summary>
    public MusicService(LavalinkExtension lavalink, LavalinkSession theNode) {
        Lavalink = lavalink;
        MusicData = new ConcurrentDictionary<ulong, GuildMusicData>();
        client = lavalink.Client;
        node = theNode;

        slsk = new SoulseekClient();
        slsk.ConnectAsync("jazzbot", "jazzbot").GetAwaiter().GetResult();
        slsk.ExcludedSearchPhrasesReceived += (sender, args) => {
            Console.WriteLine("Excluded search phrases: ");
            foreach (var phrase in args) {
                Console.WriteLine(phrase);
            }
        };

        node.StatsReceived += playbackStarted;

        async Task playbackStarted(LavalinkSession sender, LavalinkStatsReceivedEventArgs e) {
            await Console.Out.WriteLineAsync($"len/nodes: {e.Statistics.Players}");
        }
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

        gmd = MusicData.AddOrUpdate(guild.Id, new GuildMusicData(guild, Lavalink, node),
            (k, v) => v);

        gmd.setupWebhooks();

        return gmd;
    }

    /// <summary>
    /// Loads tracks from specified URL.
    /// </summary>
    /// <param name="uri">URL to load tracks from.</param>
    /// <returns>Loaded tracks.</returns>
    public Task<LavalinkTrackLoadingResult> GetTracksAsync(Uri uri)
        => node.LoadTracksAsync(uri.ToString());

    public Task<LavalinkTrackLoadingResult> GetTracksAsync(string search)
        => node.LoadTracksAsync(search);
}

public record SLSKResult(SearchResponse response, File file) {
}