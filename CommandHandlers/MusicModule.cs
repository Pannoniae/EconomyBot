using System.Globalization;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.EventHandling;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using EconomyBot.CommandHandlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Formatter = DSharpPlus.Formatter;

namespace EconomyBot;

[ModuleLifespan(ModuleLifespan.Singleton)]
public class MusicModule : BaseCommandModule {
    private MusicService Music { get; set; }
    private YouTubeSearchProvider YouTube { get; }

    public GuildMusicData GuildMusic { get; set; }

    public MusicCommon common;

    public MusicModule(YouTubeSearchProvider yt) {
        Music = Program.musicService;
        YouTube = yt;
    }

    public override async Task BeforeExecutionAsync(CommandContext ctx) {
        Music = Program.musicService;
        if (ctx.Command.Name == "join") {
            GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
            GuildMusic.CommandChannel = ctx.Channel;
            return;
        }

        var vs = ctx.Member.VoiceState;
        var chn = vs?.Channel;
        if (chn == null) {
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} You need to be in a voice channel.");
            throw new IdiotException("user error");
        }

        var mbr = ctx.Guild.CurrentMember?.VoiceState?.Channel;
        if (mbr != null && chn != mbr) {
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} You need to be in the same voice channel.");
            throw new IdiotException("user error");
        }

        GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
        GuildMusic.CommandChannel = ctx.Channel;

        await base.BeforeExecutionAsync(ctx);
    }

    [Command("eq"), Description("Enable EQ.")]
    public async Task eq(CommandContext ctx) {
        GuildMusic.enableEQ();
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Enabled EQ.");
    }

    [Command("deq"), Description("Disable EQ.")]
    public async Task deq(CommandContext ctx) {
        GuildMusic.disableEQ();
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Disabled EQ.");
    }

    [Command("reset"), Description("Reset the voice state.")]
    public async Task ResetAsync(CommandContext ctx) {
        await GuildMusic.DestroyPlayerAsync();
        int rmd = GuildMusic.EmptyQueue();
        GuildMusic.clearQueue();
    }

    [Command("join"), Description("Joins the voice channel."), Priority(1)]
    public async Task JoinAsync(CommandContext ctx) {
        // yeet the bot in 
        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Joined the channel.");
    }

    [Command("join"), Description("Joins the voice channel."), Priority(0)]
    public async Task JoinAsync(CommandContext ctx, DiscordMember member) {
        // yeet the bot in
        var vs = member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Joined the channel.");
    }

    [Command("jazz"), Description("Plays some jazz. :3"), Aliases("j"), Priority(1)]
    public async Task PlayJazzAsync(CommandContext ctx) {
        // yeet the bot in
        GuildMusic.addToQueue("_fats");
        await GuildMusic.seedQueue();
        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Started playing jazz.");
    }

    [Command("analyse"), Description("Analyse the frequency of artists."), Aliases("an")]
    public async Task AnalyseAsync(CommandContext ctx) {
        var weights = new List<string>();
        var weightsp = new List<string>();
        var sum = GuildMusicData.artistWeights.Values.Sum();
        foreach (var weight in GuildMusicData.artistWeights) {
            weights.Add($"{weight.Key}: {weight.Value}");
            weightsp.Add($"{weight.Key}: {(weight.Value / sum) * 100:#.##}%");
        }

        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Weights:\n{string.Join("\n", weights)}");
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Weights (percent):\n{string.Join("\n", weightsp)}");
    }

    [Command("stopjazz"), Description("Stops jazz.")]
    public async Task StopJazzAsync(CommandContext ctx) {
        GuildMusic.clearQueue();
        int rmd = GuildMusic.EmptyQueue();
        await GuildMusic.StopAsync();
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Stopped jazz.");
    }

    [Command("play"), Description("Plays supplied URL or searches for specified keywords."), Aliases("p"), Priority(1)]
    public async Task PlayAsync(CommandContext ctx,
        [Description("URL to play from.")] Uri uri) {
        var trackLoad = await Music.GetTracksAsync(uri);
        var tracks = trackLoad.Tracks;
        if (trackLoad.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks.Any()) {
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link.");
            return;
        }
        else if (trackLoad.LoadResultType == LavalinkLoadResultType.PlaylistLoaded &&
                 trackLoad.PlaylistInfo.SelectedTrack > 0) {
            var index = trackLoad.PlaylistInfo.SelectedTrack;
            tracks = tracks.Skip(index).Concat(tracks.Take(index));
        }

        var trackCount = tracks.Count();
        foreach (var track in tracks) {
            GuildMusic.Enqueue(track);
        }

        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();

        if (trackCount > 1)
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount:#,##0} tracks to playback queue.");
        else {
            var track = tracks.First();
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue.");
        }
    }

    [Command("jazz"), Priority(0), Aliases("pj")]
    public async Task PlayJazzAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) {
        if (term == "all") {
            GuildMusic.addAllToQueue();
            await GuildMusic.seedQueue();
            var track_ = GuildMusic.Queue.First();

            var vs_ = ctx.Member.VoiceState;
            var chn_ = vs_.Channel;
            await GuildMusic.CreatePlayerAsync(chn_);
            await GuildMusic.PlayAsync();
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Started playing {GuildMusic.artistQueue.Count} cats.");
            return;
        }

        var interactivity = ctx.Client.GetInteractivity();

        var results = await GuildMusic.StartJazz("*" + term + "*");
        if (!results.Any()) {
            await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Nothing was found.");
            return;
        }

        if (results.Count() == 1) {
            // only one result
            var el_ = results.First();
            var tracks_ = el_.Tracks;
            if (el_.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks_.Any()) {
                await ctx.RespondAsync(
                    $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link.");
                return;
            }

            var trackCount_ = tracks_.Count();
            foreach (var track in tracks_)
                GuildMusic.Enqueue(track);

            var vs_ = ctx.Member.VoiceState;
            var chn_ = vs_.Channel;
            await GuildMusic.CreatePlayerAsync(chn_);
            await GuildMusic.PlayAsync();

            if (trackCount_ > 1) {
                await ctx.RespondAsync(
                    $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount_:#,##0} tracks to playback queue.");
            }
            else {
                var track = tracks_.First();
                await ctx.RespondAsync(
                    $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue.");
            }

            return;
        }

        var pageCount = results.Count() / 10 + 1;
        if (results.Count() % 10 == 0) pageCount--;

        var content = results.Select((x, i) => (x, i))
            .GroupBy(e => e.i / 10)
            .Select(xg => new Page(
                $"{string.Join("\n", xg.Select(xa => $"`{xa.i + 1}` {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(xa.x.Tracks.First().Title)))} by {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(xa.x.Tracks.First().Author)))}"))}\n\nPage {xg.Key + 1}/{pageCount}"));

        var ems = new PaginationEmojis {
            SkipLeft = null,
            SkipRight = null,
            Stop = DiscordEmoji.FromUnicode("⏹"),
            Left = DiscordEmoji.FromUnicode("◀"),
            Right = DiscordEmoji.FromUnicode("▶")
        };

        _ = interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, content, ems,
            PaginationBehaviour.Ignore,
            PaginationDeletion.KeepEmojis, TimeSpan.FromMinutes(2));
        
        var msgC =
            $"Type a number 1-{results.Count()} to queue a track. To cancel, type cancel or {MusicCommon.Numbers.Last()}.";
        var msg = await ctx.RespondAsync(msgC);

        var res = await interactivity.WaitForMessageAsync(x => x.Author == ctx.User, TimeSpan.FromMinutes(2));
        if (res.TimedOut || res.Result == null) {
            await msg.ModifyAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No choice was made.");
            return;
        }

        var resInd = res.Result.Content.Trim();
        if (!int.TryParse(resInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elInd)) {
            if (resInd.ToLowerInvariant() == "cancel") {
                elInd = -1;
            }
            else {
                if (elInd < 0 || elInd > results.Count()) {
                    await msg.ModifyAsync(
                        $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made.");
                    return;
                }
            }
        }

        if (elInd == -1) {
            await msg.ModifyAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Choice cancelled.");
            return;
        }

        var el = results.ElementAt(elInd - 1);
        var tracks = el.Tracks;


        if (el.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks.Any()) {
            await msg.ModifyAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link.");
            return;
        }

        var trackCount = tracks.Count();
        foreach (var track in tracks)
            GuildMusic.Enqueue(track);

        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();

        if (trackCount > 1) {
            await msg.ModifyAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount:#,##0} tracks to playback queue.");
        }
        else {
            var track = tracks.First();
            await msg.ModifyAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue.");
        }
    }

    [Command("play"), Priority(0)]
    public async Task PlayAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) {
        var interactivity = ctx.Client.GetInteractivity();

        var results = await YouTube.SearchAsync(term);
        if (!results.Any()) {
            await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Nothing was found.");
            return;
        }

        var msgC = string.Join("\n",
            results.Select((x, i) =>
                $"{MusicCommon.NumberMappings[i + 1]} {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(x.Title)))} by {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(x.Author)))}"));
        msgC =
            $"{msgC}\n\nType a number 1-{results.Count()} to queue a track. To cancel, type cancel or {MusicCommon.Numbers.Last()}.";
        var msg = await ctx.RespondAsync(msgC);

        var res = await interactivity.WaitForMessageAsync(x => x.Author == ctx.User, TimeSpan.FromSeconds(30));
        if (res.TimedOut || res.Result == null) {
            await msg.ModifyAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No choice was made.");
            return;
        }

        var resInd = res.Result.Content.Trim();
        if (!int.TryParse(resInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elInd)) {
            if (resInd.ToLowerInvariant() == "cancel") {
                elInd = -1;
            }
        }
        else if (elInd < 1) {
            await msg.ModifyAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made.");
            return;
        }

        if (!MusicCommon.NumberMappings.ContainsKey(elInd)) {
            await msg.ModifyAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made.");
            return;
        }
        if (elInd == -1) {
            await msg.ModifyAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Choice cancelled.");
            return;
        }

        var el = results.ElementAt(elInd - 1);
        var url = new Uri($"https://youtu.be/{el.Id}");

        var trackLoad = await Music.GetTracksAsync(url);
        var tracks = trackLoad.Tracks;
        if (trackLoad.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks.Any()) {
            await msg.ModifyAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link.");
            return;
        }

        var trackCount = tracks.Count();
        foreach (var track in tracks)
            GuildMusic.Enqueue(track);

        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();

        if (trackCount > 1) {
            await msg.ModifyAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount:#,##0} tracks to playback queue.");
        }
        else {
            var track = tracks.First();
            await msg.ModifyAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue.");
        }
    }

    [Command("artist"), Description("Plays tracks from an artist."), Aliases("a")]
    public async Task ArtistAsync(CommandContext ctx, [RemainingText] string artist) {
        GuildMusic.addToQueue(artist);

        await GuildMusic.seedQueue();
        var track_ = GuildMusic.Queue.First();

        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Started playing {artist}.");
    }

    [Command("stopartist"), Description("Stops playing tracks from an artist."), Aliases("sa")]
    public async Task StopArtistAsync(CommandContext ctx) {
        GuildMusic.clearQueue();

        int rmd = GuildMusic.EmptyQueue();
        await GuildMusic.StopAsync();
        await GuildMusic.DestroyPlayerAsync();

        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Removed {rmd:#,##0} tracks from the queue.");
    }

    [Command("stop"), Description("Stops playback and quits the voice channel.")]
    public async Task StopAsync(CommandContext ctx) {
        int rmd = GuildMusic.EmptyQueue();
        await GuildMusic.StopAsync();
        GuildMusic.clearQueue();
        await GuildMusic.DestroyPlayerAsync();

        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Removed {rmd:#,##0} tracks from the queue.");
    }

    [Command("clear"), Description("Clears the queue.")]
    public async Task ClearAsync(CommandContext ctx) {
        int rmd = GuildMusic.EmptyQueue();
        GuildMusic.clearQueue();

        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Removed {rmd:#,##0} tracks from the queue uwu");
    }

    [Command("pause"), Description("Pauses playback.")]
    public async Task PauseAsync(CommandContext ctx) {
        await GuildMusic.PauseAsync();
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Playback paused. Use {Formatter.InlineCode($"{ctx.Prefix}resume")} to resume playback.");
    }

    [Command("resume"), Description("Resumes playback."), Aliases("unpause")]
    public async Task ResumeAsync(CommandContext ctx) {
        await GuildMusic.ResumeAsync();
        await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Playback resumed.");
    }

    [Command("skip"), Description("Skips current track."), Aliases("next")]
    public async Task SkipAsync(CommandContext ctx) {
        var track = GuildMusic.NowPlaying;
        await GuildMusic.StopAsync();
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} skipped.");
    }

    [Command("skip"), Description("Skips current track.")]
    public async Task SkipAsync(CommandContext ctx, int num) {
        for (int i = 0; i < num; i++) {
            var track = GuildMusic.NowPlaying;
            await GuildMusic.StopAsync();
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} skipped.");
            await Task.Delay(500); // wait for the next one
        }
    }

    [Command("seek"), Description("Seeks to specified time in current track.")]
    public async Task SeekAsync(CommandContext ctx, [Description("Which time point to seek to.")] TimeSpan position) {
        await GuildMusic.SeekAsync(position, false);
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Seeking to {position.ToDurationString()}...");
    }

    [Command("forward"), Description("Forwards the track by specified amount of time.")]
    public async Task ForwardAsync(CommandContext ctx, [Description("By how much to forward.")] TimeSpan offset) {
        await GuildMusic.SeekAsync(offset, true);
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Seeking forward by {offset.ToDurationString()}...");
    }

    [Command("rewind"), Description("Rewinds the track by specified amount of time.")]
    public async Task RewindAsync(CommandContext ctx, [Description("By how much to rewind.")] TimeSpan offset) {
        await GuildMusic.SeekAsync(-offset, true);
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Seeking backward by {offset.ToDurationString()}...");
    }

    [Command("volume"), Description("Sets playback volume."), Aliases("v")]
    public async Task SetVolumeAsync(CommandContext ctx,
        [Description("Volume to set. Can be 0-150. Default 100.")]
        int volume) {
        if (volume is < 0 or > 1000) {
            await ctx.RespondAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Volume must be greater than 0, and less than or equal to 1000.");
            return;
        }

        await GuildMusic.SetVolumeAsync(volume);
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Volume set to {GuildMusic.effectiveVolume}%.");
    }

    [Command("volume"), Description("Gets playback volume.")]
    public async Task GetVolumeAsync(CommandContext ctx) {
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Volume is {GuildMusic.volume} * {GuildMusic.artistVolume} = {GuildMusic.effectiveVolume}%.");
    }

    [Command("restart"), Description("Restarts the playback of the current track.")]
    public async Task RestartAsync(CommandContext ctx) {
        var track = GuildMusic.NowPlaying;
        await GuildMusic.RestartAsync();
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} restarted.");
    }

    [Command("remove"), Description("Removes a track from playback queue."), Aliases("del", "rm")]
    public async Task RemoveAsync(CommandContext ctx, [Description("Which track to remove.")] int index) {
        var itemN = GuildMusic.Remove(index - 1);
        if (itemN == null) {
            await ctx.RespondAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No such track.");
            return;
        }

        var track = itemN;
        await ctx.RespondAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} removed.");
    }

    [Command("queue"), Description("Displays current playback queue."), Aliases("q")]
    public async Task QueueAsync(CommandContext ctx) {
        var interactivity = ctx.Client.GetInteractivity();

        var pageCount = GuildMusic.Queue.Count / 10 + 1;
        if (GuildMusic.Queue.Count % 10 == 0) pageCount--;
        var pages = GuildMusic.Queue.Select(x => x.track.ToTrackString())
            .Select((s, i) => (s, i))
            .GroupBy(x => x.i / 10)
            .Select(xg =>
                new Page(
                    $"Now playing: {GuildMusic.NowPlaying.ToTrackString()}\n\n{string.Join("\n", xg.Select(xa => $"`{xa.i + 1:00}` {xa.s}"))}\n\nPage {xg.Key + 1}/{pageCount}"))
            .ToArray();

        var trk = GuildMusic.NowPlaying;
        if (!pages.Any()) {
            if (trk?.TrackString == null)
                await ctx.RespondAsync("Queue is empty!");
            else
                await ctx.RespondAsync($"Now playing: {GuildMusic.NowPlaying.ToTrackString()}");

            return;
        }

        var ems = new PaginationEmojis {
            SkipLeft = null,
            SkipRight = null,
            Stop = DiscordEmoji.FromUnicode("⏹"),
            Left = DiscordEmoji.FromUnicode("◀"),
            Right = DiscordEmoji.FromUnicode("▶")
        };
        await interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages, ems, PaginationBehaviour.Ignore,
            PaginationDeletion.KeepEmojis, TimeSpan.FromMinutes(2));
    }

    [Command("nowplaying"), Description("Displays information about currently-played track."), Aliases("np")]
    public async Task NowPlayingAsync(CommandContext ctx) {
        var track = GuildMusic.NowPlaying;
        if (GuildMusic.NowPlaying?.TrackString == null) {
            await ctx.RespondAsync("Not playing.");
        }
        else {
            await ctx.RespondAsync(
                $"Now playing: {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{GuildMusic.NowPlaying.Length.ToDurationString()}].");
        }
    }

    [Command("playerinfo"), Description("Displays information about current player."), Aliases("pinfo", "pinf"), Hidden]
    public async Task PlayerInfoAsync(CommandContext ctx) {
        await ctx.RespondAsync(
            $"Queue length: {GuildMusic.Queue.Count}\nVolume: {GuildMusic.volume}%");
    }
}

[Serializable]
// when the user is an idiot
public class IdiotException : Exception {
    public IdiotException() {
    }

    public IdiotException(string message) : base(message) {
    }

    protected IdiotException(SerializationInfo info, StreamingContext ctx) : base(info, ctx) {
    }
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
    public static string ToTrackString(this LavalinkTrack x) {
        return
            $"{Formatter.Bold(Formatter.Sanitize(x.Title))} by {Formatter.Bold(Formatter.Sanitize(x.Author ?? "No Author"))} [{x.Length.ToDurationString()}]";
    }
}

/// <summary>
/// Provides ability to search YouTube in a streamlined manner.
/// </summary>
public sealed class YouTubeSearchProvider {
    private string ApiKey { get; }
    private HttpClient Http { get; }

    /// <summary>
    /// Creates a new YouTube search provider service instance.
    /// </summary>
    public YouTubeSearchProvider() {
        ApiKey = Constants.apikey;
        Http = new HttpClient {
            BaseAddress = new Uri("https://www.googleapis.com/youtube/v3/search")
        };
        Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Companion-Cube");
    }

    /// <summary>
    /// Performs a YouTube search and returns the results.
    /// </summary>
    /// <param name="term">What to search for.</param>
    /// <returns>A collection of search results.</returns>
    public async Task<IEnumerable<YouTubeSearchResult>> SearchAsync(string term) {
        var uri = new Uri(
            $"https://www.googleapis.com/youtube/v3/search?part=snippet&maxResults=5&type=video&fields=items(id(videoId),snippet(title,channelTitle))&key={ApiKey}&q={WebUtility.UrlEncode(term)}");

        var json = "{}";
        using (var req = await Http.GetAsync(uri))
        await using (var res = await req.Content.ReadAsStreamAsync())
        using (var sr = new StreamReader(res, Encoding.UTF8))
            json = await sr.ReadToEndAsync();

        var jsonData = JObject.Parse(json);
        var data = jsonData["items"].ToObject<IEnumerable<YouTubeApiResponseItem>>();

        return data.Select(x => new YouTubeSearchResult(x.Snippet.Title, x.Snippet.Author, x.Id.VideoId));
    }
}

/// <summary>
/// Represents a YouTube search result.
/// </summary>
public struct YouTubeSearchResult {
    /// <summary>
    /// Gets the title of this item.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the name of the item's author.
    /// </summary>
    public string Author { get; }

    /// <summary>
    /// Gets the item's ID.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Creates a new YouTube search result with specified parameters.
    /// </summary>
    /// <param name="title">Title of the item.</param>
    /// <param name="author">Item's author.</param>
    /// <param name="id">Item's ID.</param>
    public YouTubeSearchResult(string title, string author, string id) {
        Title = title;
        Author = author;
        Id = id;
    }
}

internal struct YouTubeApiResponseItem {
    [JsonProperty("id")] public ResponseId Id { get; private set; }

    [JsonProperty("snippet")] public ResponseSnippet Snippet { get; private set; }


    public struct ResponseId {
        [JsonProperty("videoId")] public string VideoId { get; private set; }
    }

    public struct ResponseSnippet {
        [JsonProperty("title")] public string Title { get; private set; }

        [JsonProperty("channelTitle")] public string Author { get; private set; }
    }
}