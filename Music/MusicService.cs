using System.Collections.Concurrent;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Lavalink;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using Soulseek;
using Spectre.Console;
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
        
        slsk?.Dispose();

        slsk = new SoulseekClient();
        slsk.ConnectAsync("jazzbot", "jazzbot").GetAwaiter().GetResult();
        slsk.ExcludedSearchPhrasesReceived += (sender, args) => {
            AnsiConsole.WriteLine("Excluded search phrases: ");
            foreach (var phrase in args) {
                AnsiConsole.WriteLine(phrase);
            }
        };

        node.LavalinkSocketErrored += async (sender, args) => {
            await Console.Out.WriteLineAsync($"Lavalink socket errored: {args.Exception.Message}");
            
            _ = Task.Run(async () => {
                try {
                    await Program.saveAllPlaybackStates();
                    await Program.lavalinkReconnect();
                }
                catch (Exception ex) {
                    await Console.Out.WriteLineAsync($"Error handling Lavalink socket error: {ex.Message}");
                }
            });
        };
        node.WebsocketClosed += async (sender, args) => {
            await Console.Out.WriteLineAsync($"Lavalink websocket closed: (by Discord: {args.ByRemote}), code: {args.CloseCode}\nmessage: {args.CloseMessage}");
            
            _ = Task.Run(async () => {
                try {
                    await Program.saveAllPlaybackStates();
                    await Program.lavalinkReconnect();
                }
                catch (Exception ex) {
                    await Console.Out.WriteLineAsync($"Error handling Lavalink socket error: {ex.Message}");
                }
            });
        };
        node.StatsReceived += async (sender, args) => {
            await Console.Out.WriteLineAsync($"len/nodes: {args.Statistics.Players}");
        };
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
            if (aSpeed == bSpeed) {
                return 0;
            }
            return bSpeed - aSpeed;
        });

        return results.Take(50).ToList();
    }

    public async Task<List<SLSKResult>> getSLSKbyUser(string searchTerm, string user) {
        var options = new SearchOptions(searchTimeout: 10000, responseFilter: arg => userFilter(arg, user));
        var result = await slsk.SearchAsync(new SearchQuery(searchTerm), options: options);

        var results = new List<SLSKResult>();
        // for each response, aggregate tracks with the result it belongs to
        foreach (var r in result.Responses) {
                foreach (var f in r.Files) {
                    results.Add(new SLSKResult(r, f));
            }
        }

        return results.Take(50).ToList();
    }

    private static bool noLocked(SearchResponse arg) {
        return arg.HasFreeUploadSlot && arg.LockedFileCount == 0;
    }

    private static bool userFilter(SearchResponse arg, string user) {
        return ActualFuzz.partialFuzz(user, arg.Username) > 75;
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
    /// Gets existing music data for all active guilds.
    /// </summary>
    /// <returns>Collection of existing guild music data.</returns>
    public IEnumerable<GuildMusicData> GetExistingGuildData() {
        return MusicData.Values;
    }

    /// <summary>
    /// Gets or creates a dataset for specified guild.
    /// </summary>
    /// <param name="guild">Guild to get or create dataset for.</param>
    /// <returns>Resulting dataset.</returns>
    public Task<GuildMusicData> GetOrCreateDataAsync(DiscordGuild guild) {
        if (MusicData.TryGetValue(guild.Id, out var gmd)) {
            return Task.FromResult(gmd);
        }

        gmd = MusicData.AddOrUpdate(guild.Id, new GuildMusicData(guild, Lavalink, node),
            (k, v) => v);

        gmd.setupWebhooks();

        return Task.FromResult(gmd);
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

    public Task<LavalinkTrackLoadingResult> SearchTracksAsync(string search)
        => node.LoadTracksAsync(LavalinkSearchType.Youtube, search);
}

public record SLSKResult(SearchResponse response, File file) {
}