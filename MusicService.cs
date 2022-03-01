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
    private SecureRandom RNG { get; }
    private ConcurrentDictionary<ulong, GuildMusicData> MusicData { get; }
    private DiscordClient Discord { get; }

    /// <summary>
    /// Creates a new instance of this music service.
    /// </summary>
    /// <param name="rng">Cryptographically-secure random number generator implementaion.</param>
    public MusicService(SecureRandom rng, LavalinkExtension lavalink) {
        Lavalink = lavalink;
        RNG = rng;
        MusicData = new ConcurrentDictionary<ulong, GuildMusicData>();
        Discord = lavalink.Client;
        var node = lavalink.ConnectedNodes.Values.First();

        node.TrackException += Lavalink_TrackExceptionThrown;
    }

    /// <summary>
    /// Saves data for specified guild.
    /// </summary>
    /// <param name="guild">Guild to save data for.</param>
    /// <returns></returns>
    public Task SaveDataForAsync(DiscordGuild guild) {
        if (MusicData.TryGetValue(guild.Id, out var gmd)) {
            
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets or creates a dataset for specified guild.
    /// </summary>
    /// <param name="guild">Guild to get or create dataset for.</param>
    /// <returns>Resulting dataset.</returns>
    public async Task<GuildMusicData> GetOrCreateDataAsync(DiscordGuild guild) {
        if (MusicData.TryGetValue(guild.Id, out var gmd))
            return gmd;

        gmd = MusicData.AddOrUpdate(guild.Id, new GuildMusicData(guild, RNG, Lavalink),
            (k, v) => v);

        return gmd;
    }

    /// <summary>
    /// Loads tracks from specified URL.
    /// </summary>
    /// <param name="uri">URL to load tracks from.</param>
    /// <returns>Loaded tracks.</returns>
    public Task<LavalinkLoadResult> GetTracksAsync(Uri uri)
        => Lavalink.ConnectedNodes.Values.First().Rest.GetTracksAsync(uri);

    /// <summary>
    /// Shuffles the supplied track list.
    /// </summary>
    /// <param name="tracks">Collection of tracks to shuffle.</param>
    /// <returns>Shuffled track collection.</returns>
    public IEnumerable<LavalinkTrack> Shuffle(IEnumerable<LavalinkTrack> tracks)
        => tracks.OrderBy(x => RNG.Next());

    private async Task Lavalink_TrackExceptionThrown(LavalinkGuildConnection con, TrackExceptionEventArgs e) {
        if (e.Player?.Guild == null)
            return;

        if (!MusicData.TryGetValue(e.Player.Guild.Id, out var gmd))
            return;

        await gmd.CommandChannel.SendMessageAsync(
            $"{DiscordEmoji.FromName(Discord, ":msfrown:")} A problem occured while playing {Formatter.Bold(Formatter.Sanitize(e.Track.Title))} by {Formatter.Bold(Formatter.Sanitize(e.Track.Author))}:\n{e.Error}");
    }
}