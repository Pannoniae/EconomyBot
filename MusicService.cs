using System.Collections.Concurrent;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Lavalink;
using DSharpPlus.Lavalink.EventArgs;
using Emzi0767;

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
/// Provides a persistent way of tracking music in various guilds.
/// </summary>
public sealed class MusicService {
    private LavalinkExtension Lavalink { get; }
    private ConcurrentDictionary<ulong, GuildMusicData> MusicData { get; }
    private DiscordClient Discord { get; }
    
    private LavalinkNodeConnection node { get; }

    /// <summary>
    /// Creates a new instance of this music service.
    /// </summary>
    public MusicService(LavalinkExtension lavalink, LavalinkNodeConnection theNode) {
        Lavalink = lavalink;
        MusicData = new ConcurrentDictionary<ulong, GuildMusicData>();
        Discord = lavalink.Client; 
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
        MusicData.TryGetValue(guild.Id, out var _);
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
        if (e.Player?.Guild == null)
            return;

        if (!MusicData.TryGetValue(e.Player.Guild.Id, out var gmd)) 
            return;

        await gmd.CommandChannel.SendMessageAsync(
            $"{DiscordEmoji.FromName(Discord, ":pinkpill:")} A problem occured while playing {Formatter.Bold(Formatter.Sanitize(e.Track.Title))} by {Formatter.Bold(Formatter.Sanitize(e.Track.Author))}:\n{e.Error}");
    }
}