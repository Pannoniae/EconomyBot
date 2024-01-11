using System.Collections.Concurrent;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;

namespace EconomyBot;

/// <summary>
/// Provides a persistent way of tracking music in various guilds.
/// </summary>
public sealed class MusicService {
    private LavalinkExtension Lavalink;
    private ConcurrentDictionary<ulong, GuildMusicData> MusicData;
    private readonly DiscordClient client;

    private LavalinkNodeConnection node { get; }

    /// <summary>
    /// Creates a new instance of this music service.
    /// </summary>
    public MusicService(LavalinkExtension lavalink, LavalinkNodeConnection theNode) {
        Lavalink = lavalink;
        MusicData = new ConcurrentDictionary<ulong, GuildMusicData>();
        client = lavalink.Client;
        node = theNode;

        node.TrackException += Lavalink_TrackExceptionThrown;

        async Task playbackStarted(LavalinkGuildConnection con, TrackStartEventArgs e) {
            await Console.Out.WriteLineAsync($"len/nodes: {lavalink.ConnectedNodes.Count}");
        }

        node.PlaybackStarted += playbackStarted;
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
    public Task<LavalinkLoadResult> GetTracksAsync(Uri uri)
        => node.Rest.GetTracksAsync(uri);

    public Task<LavalinkLoadResult> GetTracksAsync(string search)
        => node.Rest.GetTracksAsync(search);

    private async Task Lavalink_TrackExceptionThrown(LavalinkGuildConnection con, TrackExceptionEventArgs e) {
        if (e.Player?.Guild is null)
            return;

        if (!MusicData.TryGetValue(e.Player.Guild.Id, out var gmd))
            return;

        await gmd.CommandChannel.SendMessageAsync(
            $"{DiscordEmoji.FromName(client, ":pinkpill:")} A problem occured while playing {Formatter.Bold(Formatter.Sanitize(e.Track.Title))} by {Formatter.Bold(Formatter.Sanitize(e.Track.Author))}:\n{e.Error}");
    }
}