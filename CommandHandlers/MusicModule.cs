using System.Globalization;
using System.Net;
using System.Text;
using DisCatSharp;
using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Attributes;
using DisCatSharp.Entities;
using DisCatSharp.Interactivity;
using DisCatSharp.Interactivity.Entities;
using DisCatSharp.Interactivity.Enums;
using DisCatSharp.Interactivity.Extensions;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using EconomyBot.Logging;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Soulseek;
using Spectre.Console;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace EconomyBot;

/// Text versions of the music commands. The commands themselves live in <see cref="MusicCommands"/>; anything added
/// here needs its twin in <see cref="MusicModuleSlash"/>.
public class MusicModule : BaseCommandModule {
    private static Task music(CommandContext ctx, Func<MusicCommands, Task> body, bool voiceChecks = true) =>
        Run.prefix(ctx, c => MusicCommands.run(c, body, voiceChecks));

    [Command("eq"), Description("Enable EQ.")]
    public Task eq(CommandContext ctx) => music(ctx, m => m.eq());

    [Command("reset"), Description("Reset the voice state.")]
    public Task ResetAsync(CommandContext ctx) => music(ctx, m => m.resetPlayer());

    [Command("join"), Description("Joins the voice channel."), Priority(1)]
    public Task JoinAsync(CommandContext ctx) => music(ctx, m => m.join(), false);

    [Command("join"), Description("Joins the voice channel."), Priority(0)]
    public Task JoinAsync(CommandContext ctx, DiscordMember member) => music(ctx, m => m.join(), false);

    [Command("jazz"), Description("Plays some jazz. :3"), Aliases("j"), Priority(1)]
    public Task PlayJazzAsync(CommandContext ctx) => music(ctx, m => m.jazz());

    [Command("live"), Description("Live music! :3"), Aliases("l"), Priority(1)]
    public Task PlayLiveAsync(CommandContext ctx) => music(ctx, m => m.live());

    [Command("analyse"), Description("Analyse the frequency of artists."), Aliases("an")]
    public Task AnalyseAsync(CommandContext ctx) => music(ctx, m => m.analyse());

    [Command("rl"), Description("Reloads music data.")]
    public Task ReloadAsync(CommandContext ctx) => music(ctx, m => m.reload());

    [Command("stopjazz"), Description("Stops jazz.")]
    public Task StopJazzAsync(CommandContext ctx) => music(ctx, m => m.stopJazz());

    [Command("play"), Description("Plays supplied URL or searches for specified keywords."), Aliases("p"), Priority(2)]
    public Task PlayAsync(CommandContext ctx,
        [Description("URL to play from.")] Uri uri) => music(ctx, m => m.play(uri));

    [Command("jazz"), Priority(0), Aliases("pj")]
    public Task PlayJazzAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) => music(ctx, m => m.jazz(term));

    [Command("soulseek"), Priority(0), Aliases("slsk")]
    public Task PlaySLSKAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) => music(ctx, m => m.soulseek(term));

    [Command("soulseekuser"), Priority(0), Aliases("su")]
    public Task PlaySLSKUserAsync(CommandContext ctx,
        [Description("The user to search.")] string user,
        [RemainingText, Description("Terms to search for.")]
        string term) => music(ctx, m => m.soulseekUser(user, term));

    [Command("play"), Priority(1)]
    public Task PlayAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) => music(ctx, m => m.play(term));

    [Command("artist"), Description("Plays tracks from an matchedArtist."), Aliases("a")]
    public Task ArtistAsync(CommandContext ctx, [RemainingText] string artist) => music(ctx, m => m.artist(artist));

    [Command("stopartist"), Description("Stops playing tracks from an matchedArtist."), Aliases("sa")]
    public Task StopArtistAsync(CommandContext ctx) => music(ctx, m => m.stopArtist());

    [Command("stop"), Description("Stops playback and quits the voice channel.")]
    public Task StopAsync(CommandContext ctx) => music(ctx, m => m.stop());

    [Command("repeat"), Description("Toggles repeat of the queue.")]
    public Task RepeatAsync(CommandContext ctx) => music(ctx, m => m.repeat());

    [Command("earrape"), Description("Toggles annoying users."), Aliases("er")]
    public Task EarrapeAsync(CommandContext ctx) => music(ctx, m => m.earrape());

    [Command("clear"), Description("Clears the queue.")]
    public Task ClearAsync(CommandContext ctx) => music(ctx, m => m.clear());

    [Command("pause"), Description("Pauses playback.")]
    public Task PauseAsync(CommandContext ctx) => music(ctx, m => m.pause());

    [Command("resume"), Description("Resumes playback."), Aliases("unpause")]
    public Task ResumeAsync(CommandContext ctx) => music(ctx, m => m.resume());

    [Command("shuffle"), Description("Shuffles the queue.")]
    public Task ShuffleAsync(CommandContext ctx) => music(ctx, m => m.shuffle());

    [Command("skip"), Description("Skips current track."), Aliases("next")]
    public Task SkipAsync(CommandContext ctx) => music(ctx, m => m.skip(1));

    [Command("skip"), Description("Skips current track.")]
    public Task SkipAsync(CommandContext ctx, int num) => music(ctx, m => m.skip(num));

    [Command("seek"), Description("Seeks to specified time in current track.")]
    public Task SeekAsync(CommandContext ctx, [Description("Which time point to seek to.")] TimeSpan position) =>
        music(ctx, m => m.seek(position));

    [Command("forward"), Description("Forwards the track by specified amount of time.")]
    public Task ForwardAsync(CommandContext ctx, [Description("By how much to forward.")] TimeSpan offset) =>
        music(ctx, m => m.forward(offset));

    [Command("rewind"), Description("Rewinds the track by specified amount of time.")]
    public Task RewindAsync(CommandContext ctx, [Description("By how much to rewind.")] TimeSpan offset) =>
        music(ctx, m => m.rewind(offset));

    [Command("volume"), Description("Sets playback volume."), Aliases("v")]
    public Task SetVolumeAsync(CommandContext ctx,
        [Description("Volume to set. Can be 0-150. Default 100.")]
        int volume) => music(ctx, m => m.setVolume(volume));

    [Command("volume"), Description("Gets playback volume.")]
    public Task GetVolumeAsync(CommandContext ctx) => music(ctx, m => m.getVolume());

    [Command("restart"), Description("Restarts the playback of the current track.")]
    public Task RestartAsync(CommandContext ctx) => music(ctx, m => m.restart());

    [Command("remove"), Description("Removes a track from playback queue."), Aliases("del", "rm")]
    public Task RemoveAsync(CommandContext ctx, [Description("Which track to remove.")] int index) =>
        music(ctx, m => m.remove(index));

    [Command("queue"), Description("Displays current playback queue."), Aliases("q")]
    public Task QueueAsync(CommandContext ctx) => music(ctx, m => m.queue(), false);

    [Command("nowplaying"), Description("Displays information about currently-played track."), Aliases("np")]
    public Task NowPlayingAsync(CommandContext ctx) => music(ctx, m => m.nowPlaying());

    [Command("playerinfo"), Description("Displays information about current player."), Aliases("pinfo", "pinf"), Hidden]
    public Task PlayerInfoAsync(CommandContext ctx) => music(ctx, m => m.playerInfo());
}

public static class Extensions {
    /// <summary>
    /// Converts given <see cref="TimeSpan"/> to a duration string.
    /// </summary>
    /// <param name="ts">Time span to convert.</param>
    /// <returns>Duration string.</returns>
    public static string ToDurationString(this TimeSpan ts) {
        return ts.ToString(ts.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");
    }

    /// <summary>
    /// Converts given <see cref="LavalinkTrack"/> to a track string.
    /// </summary>
    /// <param name="x">Music item to convert.</param>
    /// <returns>Track string.</returns>
    public static string ToTrackString(this LavalinkTrack? x) {
        return x != null ? $"{(x.Info.Title ?? "No title").Sanitize().Bold().URLDecode()} by {(x.Info.Author ?? "No Author").Sanitize().Bold().URLDecode()} [{x.Info.Length.ToDurationString()}]" : "";
    }

    public static string URLDecode(this string title) {
        return WebUtility.HtmlDecode(WebUtility.UrlDecode(title));
    }

    public static string ToLimitedTrackString(this LavalinkTrack? x) {
        return x != null ? $"{(x.Info.Title ?? "No title").Sanitize().Bold().URLDecode()} by {(x.Info.Author ?? "No Author").Sanitize().Bold().URLDecode()}" : "";
    }

    public static string ReversePath(this string s) {
        return string.Join("\\", Enumerable.Reverse(s.Split('\\')));
    }

    public static string musicLength(this TimeSpan timeSpan) {
        return $"{(int)timeSpan.TotalMinutes}:{timeSpan.Seconds:D2}";
    }

    public static string getBitrateString(this Soulseek.File f) {
        // get all attributes, join them

        // if variable bitrate, return that
        if (f.Attributes.Any(a => a.Type == FileAttributeType.BitRate)) {
            return $"{f.BitRate!.Value}kbps";
        }

        // if samplerate+depth (FLAC), do that like 16/44.1khz
        if (f.Attributes.Any(a => a.Type == FileAttributeType.SampleRate) &&
            f.Attributes.Any(a => a.Type == FileAttributeType.BitDepth)) {
            var sr = (f.SampleRate!.Value / 1000f).ToString("N1");
            var bd = f.BitDepth;
            return $"{bd}/{sr}kHz";
        }
        return "???";
    }
}
