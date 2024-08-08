using System.Globalization;
using System.Net;
using System.Text;
using DisCatSharp;
using DisCatSharp.CommandsNext;
using DisCatSharp.CommandsNext.Attributes;
using DisCatSharp.Entities;
using DisCatSharp.Interactivity;
using DisCatSharp.Interactivity.Enums;
using DisCatSharp.Interactivity.Extensions;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Soulseek;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace EconomyBot;

// TODO implement a CheckBaseAttribute to stop commands from erroring when base prereqs aren't met

[ModuleLifespan(ModuleLifespan.Singleton)]
public class MusicModule(YouTubeSearchProvider yt) : BaseCommandModule {
    private MusicService Music { get; set; } = Program.musicService;
    private YouTubeSearchProvider YouTube { get; } = yt;

    public GuildMusicData GuildMusic { get; set; }

    private readonly MusicCommon common = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static DiscordChannel? getChannel(CommandContext ctx) {
        return ctx.Member?.VoiceState?.Channel;
    }

    private async Task startPlayer(CommandContext ctx) {
        var chn = getChannel(ctx);
        await GuildMusic.CreatePlayerAsync(chn);
    }

    private async Task reset() {
        GuildMusic.queue.clearQueue();
        GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
    }

    public override async Task BeforeExecutionAsync(CommandContext ctx) {
        if (!Program.lavalinkInit) {
            await common.respond(ctx,
                "Lavalink not initialised, can't play music right now. (Check output for details)");
            throw new Exception("Actually, this is not your fault.:)");
        }

        Music = Program.musicService;

        if (ctx.Member.Roles.Any(r => r.Name.Contains("No music", StringComparison.OrdinalIgnoreCase))) {
            throw new Exception("Leave me alone...");
        }

        if (ctx.Command.Name == "join") {
            GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
            GuildMusic.CommandChannel = ctx.Channel;
            return;
        }

        var chn = getChannel(ctx);
        if (chn is null && ctx.Command.Name != "queue") {
            await common.respond(ctx, "You need to be in a voice channel.");
            throw new IdiotException("user error");
        }

        var mbr = ctx.Guild.CurrentMember?.VoiceState?.Channel;
        if (mbr is not null && chn != mbr && ctx.Command.Name != "queue") {
            await common.respond(ctx, "You need to be in the same voice channel.");
            throw new IdiotException("user error");
        }

        GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
        GuildMusic.CommandChannel = ctx.Channel;

        await base.BeforeExecutionAsync(ctx);
    }

    [Command("eq"), Description("Enable EQ.")]
    public async Task eq(CommandContext ctx) {
        GuildMusic.toggleEQ();
        if (GuildMusic.eq) {
            await common.respond(ctx, "Enabled EQ.");
        }
        else {
            await common.respond(ctx, "Disabled EQ.");
        }
    }

    [Command("reset"), Description("Reset the voice state.")]
    public async Task ResetAsync(CommandContext ctx) {
        await reset();
        await GuildMusic.DestroyPlayerAsync();
    }

    [Command("join"), Description("Joins the voice channel."), Priority(1)]
    public async Task JoinAsync(CommandContext ctx) {
        // yeet the bot in 
        await startPlayer(ctx);
        await common.respond(ctx, "Joined the channel.");
    }

    [Command("join"), Description("Joins the voice channel."), Priority(0)]
    public async Task JoinAsync(CommandContext ctx, DiscordMember member) {
        // yeet the bot in
        await startPlayer(ctx);
        await common.respond(ctx, "Joined the channel.");
    }

    [Command("jazz"), Description("Plays some jazz. :3"), Aliases("j"), Priority(1)]
    public async Task PlayJazzAsync(CommandContext ctx) {
        // yeet the bot in
        GuildMusic.queue.addToQueue("_fats");
        await GuildMusic.queue.seedQueue();
        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();
        await common.respond(ctx, "Started playing jazz.");
    }

    [Command("live"), Description("Live music! :3"), Aliases("l"), Priority(1)]
    public async Task PlayLiveAsync(CommandContext ctx) {
        // yeet the bot in
        GuildMusic.queue.addToQueue("_fatslive");
        await GuildMusic.queue.seedQueue();
        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();
        await common.respond(ctx, "Started playing jazz.");
    }

    [Command("analyse"), Description("Analyse the frequency of artists."), Aliases("an")]
    public async Task AnalyseAsync(CommandContext ctx) {
        var sum = GuildMusicData.artistWeights.Values.Sum();

        var weights = GuildMusicData.artistWeights.Select(w => $"{w.Key}: {w.Value}");
        var weightsp = GuildMusicData.artistWeights.Select(
            w => $"{w.Key}: {w.Value / sum * 100:#.##}%");

        await common.respond(ctx, $"Weights:\n{string.Join("\n", weights)}");
        await common.respond(ctx, $"Weights (percent):\n{string.Join("\n", weightsp)}");
    }

    [Command("stopjazz"), Description("Stops jazz.")]
    public async Task StopJazzAsync(CommandContext ctx) {
        await reset();
        await common.respond(ctx, "Stopped jazz.");
    }

    [Command("play"), Description("Plays supplied URL or searches for specified keywords."), Aliases("p"), Priority(1)]
    public async Task PlayAsync(CommandContext ctx,
        [Description("URL to play from.")] Uri uri) {
        var trackLoad = await Music.GetTracksAsync(uri);
        var result = trackLoad.Result;
        List<LavalinkTrack> tracks = [];
        if (trackLoad.LoadType == LavalinkLoadResultType.Error) {
            await common.respond(ctx, "No tracks were found at specified link.");
            return;
        }

        if (trackLoad.LoadType == LavalinkLoadResultType.Playlist) {
            var playlist = (LavalinkPlaylist)result;
            if (playlist.Info.SelectedTrack > 0) {
                var index = playlist.Info.SelectedTrack;
                tracks = tracks.Skip(index).Concat(tracks.Take(index)).ToList();
            }
        }

        if (trackLoad.LoadType == LavalinkLoadResultType.Search) {
            var search = (List<LavalinkTrack>)result;
            tracks = search;
        }

        if (trackLoad.LoadType == LavalinkLoadResultType.Track) {
            var search = (LavalinkTrack)result;
            tracks = [search];
        }

        var trackCount = tracks.Count;
        foreach (var track in tracks) {
            GuildMusic.queue.Enqueue(track);
        }

        var chn = getChannel(ctx);
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.queue.PlayAsync();

        if (trackCount > 1)
            await common.respond(ctx, $"Added {trackCount:#,##} tracks to playback queue.");
        else {
            var track = tracks.First();
            await common.respond(ctx,
                $"Added {track.Info.Title.Sanitize().Bold()} by {track.Info.Author.Sanitize().Bold()} to the playback queue.");
        }
    }

    [Command("jazz"), Priority(0), Aliases("pj")]
    public async Task PlayJazzAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) {
        if (term == "all") {
            GuildMusic.queue.addAllToQueue();
            await GuildMusic.queue.seedQueue();

            await startPlayer(ctx);
            await GuildMusic.queue.PlayAsync();
            await common.respond(ctx, $"Started playing {GuildMusic.queue.artistQueue.Count} cats.");
            return;
        }

        var interactivity = ctx.Client.GetInteractivity();

        var results = (await GuildMusic.getJazz("*" + term + "*")).ToList();
        if (!results.Any()) {
            await common.respond(ctx, "Nothing was found.");
            return;
        }

        LavalinkTrack? track;
        object? track_;
        if (results.Count == 1) {
            // only one result
            var el_ = results.First();
            track_ = el_;
            if (track_ == null) {
                await common.respond(ctx, "No tracks were found at specified link.");
                return;
            }

            track = (LavalinkTrack)track_;


            GuildMusic.queue.Enqueue(track);


            await startPlayer(ctx);
            await GuildMusic.queue.PlayAsync();

            /*if (trackCount_ > 1) {
                await common.respond(ctx, $"Added {trackCount_:#,##0} tracks to playback queue.");
            }
            else {
                var track = tracks_.First();*/
            await common.respond(ctx,
                $"Added {track.Info.Title.Sanitize().Bold()} by {track.Info.Author.Sanitize().Bold()} to the playback queue.");
            return;
        }

        var pageCount = results.Count / 10 + 1;
        if (results.Count() % 10 == 0) {
            pageCount--;
        }

        var content = results.Select((x, i) => (x, i))
            .GroupBy(e => e.i / 10)
            .Select(xg => new Page(
                $"{string.Join("\n", xg.Select(xa => $"`{xa.i + 1}` {WebUtility.HtmlDecode(xa.x.Info.Title).Sanitize().Bold()} by {WebUtility.HtmlDecode(xa.x.Info.Author).Sanitize().Bold()}"))}\n\nPage {xg.Key + 1}/{pageCount}"));

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
            $"Type a number 1-{results.Count()} to queue a track. To cancel, type cancel or {MusicCommon.NumberMappingsReverse.Last()}.";

        var msg = await ctx.RespondAsync(msgC);

        var res = await interactivity.WaitForMessageAsync(x => x.Author == ctx.User && x.Channel == ctx.Channel,
            TimeSpan.FromMinutes(2));
        if (res.TimedOut || res.Result == null) {
            await msg.ModifyAsync($"{Program.cube} No choice was made.");
            return;
        }

        var resInd = res.Result.Content.Trim();
        if (!int.TryParse(resInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elInd)) {
            if (resInd.ToLowerInvariant() == "cancel") {
                elInd = -1;
            }
            else {
                return;
            }
        }

        else if (elInd < 0 || elInd > results.Count) {
            await common.modify(ctx, msg, "Invalid choice was made.");
            return;
        }

        if (elInd == -1) {
            await common.modify(ctx, msg, "Choice cancelled.");
            return;
        }

        var el = results.ElementAt(elInd - 1);
        track_ = el;


        if (track_ == null) {
            await common.modify(ctx, msg, "No tracks were found at specified link.");
            return;
        }

        track = el;

        GuildMusic.queue.Enqueue(track);
        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();

        /*if (trackCount > 1) {
            await common.modify(ctx, msg, $"Added {trackCount:#,##0} tracks to playback queue.");
        }
        else {*/
        await common.modify(ctx, msg,
            $"Added {track.Info.Title.Sanitize().Bold()} by {track.Info.Author.Sanitize().Bold()} to the playback queue.");
    }


    /// <summary>
    /// Note to Mr. or Ms. Library Author.
    /// I won't make my bot GPL just because you want me to. I'm not using your library to make money.
    /// I am not creating a "derivative work" using your library because the bot is perfectly functional without this functionality.
    /// This is "mere aggregation" in GPL-speak.
    /// P.S. fuck copyright as a concept
    /// </summary>
    [Command("soulseek"), Priority(0), Aliases("slsk")]
    public async Task PlaySLSKAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) {

        var interactivity = ctx.Client.GetInteractivity();
        if (string.IsNullOrWhiteSpace(term)) {
            await common.respond(ctx, "No query was entered :(");
            return;
        }

        var results = await Music.getSLSK(term);
        if (results.Count == 0) {
            await common.respond(ctx, "Nothing was found.");
            return;
        }

        var pageCount = results.Count / 10 + 1;
        if (results.Count % 10 == 0) {
            pageCount--;
        }

        var content = results.Select((x, i) => (x, i))
            .GroupBy(e => e.i / 10)
            .Select(xg => new Page(
                $"{string.Join("\n",
                    xg.Select(xa => $"**{xa.i + 1}.** {WebUtility.HtmlDecode(xa.x.file.Filename.ReversePath()).InlineCode()}" +
                                    $" {TimeSpan.FromSeconds(xa.x.file.Length.GetValueOrDefault()).musicLength().Bold()} ({xa.x.file.getBitrateString()})"))
                }\n\nPage {xg.Key + 1}/{pageCount}"));

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
            $"Type a number 1-{results.Count} to queue a track. To cancel, type cancel or {MusicCommon.NumberMappingsReverse.Last()}.";

        var msg = await ctx.RespondAsync(msgC);

        var res = await interactivity.WaitForMessageAsync(x => x.Author == ctx.User && x.Channel == ctx.Channel,
            TimeSpan.FromMinutes(2));
        if (res.TimedOut || res.Result == null) {
            await msg.ModifyAsync($"{Program.cube} No choice was made.");
            return;
        }

        var resInd = res.Result.Content.Trim();
        if (!int.TryParse(resInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elInd)) {
            if (resInd.ToLowerInvariant() == "cancel") {
                elInd = -1;
            }
            else {
                await common.modify(ctx, msg, "Invalid choice was made.");
                return;
            }
        }

        else if (elInd < 0 || elInd > results.Count) {
            await common.modify(ctx, msg, "Invalid choice was made.");
            return;
        }

        if (elInd == -1) {
            await common.modify(ctx, msg, "Choice cancelled.");
            return;
        }

        var chosen = results.ElementAt(elInd - 1);


        // actually download it from soulseek
        var slsk = Music.slsk;
        var tempFolder = "/snd/music/temp";

        // basically, the "filename" goes like this:
        // for example: @@xrknr\Music\Dream Theater\2002 - six degrees of inner turbulence (flac)\(09) [Dream Theater] IV. The Test That Stumped Them All.flac
        // we want the LAST part of the path as the actual filename to download to.
        var actualFilename = chosen.file.Filename.Split('\\').Last();

        await common.modify(ctx, msg, $"Downloading {actualFilename}...");

        // we hash the filename so we don't reDL the same file
        var hash = chosen.file.Filename.GetHashCode().ToString("x8");
        var localPath = Path.Join(tempFolder, hash, actualFilename);
        // if hash exists, play from that
        // if not, create folder
        LavalinkTrack? lltrack;
        // if the hash directory exists + the file exists
        if (Directory.Exists(Path.Join(tempFolder, hash)) &&
            File.Exists(Path.Join(tempFolder, hash, actualFilename))) {
            lltrack = await GuildMusicData.getTrackAsync(GuildMusic.Node, localPath);
        }
        else {
            // create the folder
            Directory.CreateDirectory(Path.Join(tempFolder, hash));
            try {
                var dl = await slsk.DownloadAsync(chosen.response.Username, chosen.file.Filename, localPath);
            }
            catch (TimeoutException e) {
                Console.WriteLine(e);
                await common.modify(ctx, msg, "Download timed out...");
                return;
            }
            lltrack = await GuildMusicData.getTrackAsync(GuildMusic.Node, localPath);
        }

        GuildMusic.queue.Enqueue(lltrack);
        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();

        /*if (trackCount > 1) {
            await common.modify(ctx, msg, $"Added {trackCount:#,##0} tracks to playback queue.");
        }
        else {*/
        await common.modify(ctx, msg,
            $"Added {lltrack.Info.Title.Sanitize().Bold()} by {lltrack.Info.Author.Sanitize().Bold()} to the playback queue.");
    }

    [Command("play"), Priority(0)]
    public async Task PlayAsync(CommandContext ctx,
        [RemainingText, Description("Terms to search for.")]
        string term) {
        var interactivity = ctx.Client.GetInteractivity();

        var results = (await YouTube.SearchAsync(term)).ToList();
        if (!results.Any()) {
            await common.respond(ctx, "Nothing was found.");
            return;
        }

        var msgC = string.Join("\n",
            results.Select((x, i) =>
                $"{MusicCommon.NumberMappings[i + 1]} {WebUtility.HtmlDecode(x.Title).Sanitize().Bold()} by {WebUtility.HtmlDecode(x.Author).Sanitize().Bold()}"));
        msgC =
            $"{msgC}\n\nType a number 1-{results.Count} to queue a track. To cancel, type cancel or {MusicCommon.NumberMappingsReverse.Last()}.";
        var msg = await ctx.RespondAsync(msgC);

        var res = await interactivity.WaitForMessageAsync(x => x.Author == ctx.User, TimeSpan.FromSeconds(30));
        if (res.TimedOut || res.Result == null) {
            await common.modify(ctx, msg, "No choice was made.");
            return;
        }

        var resInd = res.Result.Content.Trim();
        if (!int.TryParse(resInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elInd)) {
            if (resInd.ToLowerInvariant() == "cancel") {
                elInd = -1;
            }
            else {
                return;
            }
        }
        else if (elInd < 1) {
            await common.modify(ctx, msg, "Invalid choice was made.");
            return;
        }

        if (!MusicCommon.NumberMappings.ContainsKey(elInd)) {
            await common.modify(ctx, msg, "Invalid choice was made.");
            return;
        }

        if (elInd == -1) {
            await common.modify(ctx, msg, "Choice cancelled.");
            return;
        }

        var el = results.ElementAt(elInd - 1);
        var url = new Uri($"https://youtu.be/{el.Id}");

        var trackLoad = await Music.GetTracksAsync(url);
        var result = trackLoad.Result;
        List<LavalinkTrack> tracks = [];
        if (trackLoad.LoadType == LavalinkLoadResultType.Error) {
            await common.respond(ctx, "No tracks were found at specified link.");
            return;
        }

        if (trackLoad.LoadType == LavalinkLoadResultType.Playlist) {
            var playlist = (LavalinkPlaylist)result;
            if (playlist.Info.SelectedTrack > 0) {
                var index = playlist.Info.SelectedTrack;
                tracks = tracks.Skip(index).Concat(tracks.Take(index)).ToList();
            }
        }

        if (trackLoad.LoadType == LavalinkLoadResultType.Search) {
            var search = (List<LavalinkTrack>)result;
            tracks = search;
        }

        if (trackLoad.LoadType == LavalinkLoadResultType.Track) {
            var search = (LavalinkTrack)result;
            tracks = [search];
        }

        var trackCount = tracks.Count;
        foreach (var track in tracks) {
            GuildMusic.queue.Enqueue(track);
        }

        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();

        if (trackCount > 1) {
            await common.modify(ctx, msg, $"Added {trackCount:#,##0} tracks to playback queue.");
        }
        else {
            var track = tracks.First();
            await common.modify(ctx, msg,
                $"Added {track.Info.Title.Sanitize().Bold()} by {track.Info.Author.Sanitize().Bold()} to the playback queue.");
        }
    }

    [Command("artist"), Description("Plays tracks from an matchedArtist."), Aliases("a")]
    public async Task ArtistAsync(CommandContext ctx, [RemainingText] string artist) {
        string matchedArtist =
            GuildMusicData.artistMappings.Keys.MaxBy(values => ActualFuzz.partialFuzz(artist, values))!;
        GuildMusic.queue.addToQueue(matchedArtist);

        await GuildMusic.queue.seedQueue();

        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();
        await common.respond(ctx, $"Started playing {matchedArtist}.");
    }

    [Command("stopartist"), Description("Stops playing tracks from an matchedArtist."), Aliases("sa")]
    public async Task StopArtistAsync(CommandContext ctx) {
        GuildMusic.queue.clearQueue();

        int rmd = GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        await GuildMusic.DestroyPlayerAsync();

        await common.respond(ctx, $"Removed {rmd:#,##0} tracks from the queue.");
    }

    [Command("stop"), Description("Stops playback and quits the voice channel.")]
    public async Task StopAsync(CommandContext ctx) {
        int rmd = GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        GuildMusic.queue.clearQueue();
        await GuildMusic.DestroyPlayerAsync();

        await common.respond(ctx, $"Removed {rmd:#,##0} tracks from the queue.");
    }

    [Command("repeat"), Description("Toggles repeat of the queue.")]
    public async Task RepeatAsync(CommandContext ctx) {
        bool repeat = GuildMusic.queue.repeatQueue;
        GuildMusic.queue.repeatQueue = !repeat;
        // seed the queue if it doesn't exist and we are playing a manual song
        if (GuildMusic.queue.NowPlaying != null && GuildMusic.queue.NowPlaying.artist == null &&
            GuildMusic.queue.Queue.Count == 0) {
            GuildMusic.queue.Queue.Add(GuildMusic.queue.NowPlaying);
        }

        if (repeat) {
            await common.respond(ctx, "Disabled repeat.");
        }
        else {
            await common.respond(ctx, "Enabled repeat.");
        }
    }

    [Command("earrape"), Description("Toggles annoying users."), Aliases("er")]
    public async Task EarrapeAsync(CommandContext ctx) {
        bool earrape = GuildMusic.queue.earrapeMode;
        GuildMusic.queue.earrapeMode = !earrape;
        if (earrape) {
            await common.respond(ctx, "Disabled earrape mode.");
        }
        else {
            await common.respond(ctx, "Enabled earrape mode.");
        }
    }

    [Command("clear"), Description("Clears the queue.")]
    public async Task ClearAsync(CommandContext ctx) {
        int rmd = GuildMusic.queue.EmptyQueue();
        GuildMusic.queue.clearQueue();

        await common.respond(ctx, $"Removed {rmd:#,##0} tracks from the queue uwu");
    }

    [Command("pause"), Description("Pauses playback.")]
    public async Task PauseAsync(CommandContext ctx) {
        await GuildMusic.PauseAsync();
        await common.respond(ctx,
            $"Playback paused. Use {Formatter.InlineCode($"{ctx.Prefix}resume")} to resume playback.");
    }

    [Command("resume"), Description("Resumes playback."), Aliases("unpause")]
    public async Task ResumeAsync(CommandContext ctx) {
        await GuildMusic.ResumeAsync();
        await common.respond(ctx, "Playback resumed.");
    }

    [Command("skip"), Description("Skips current track."), Aliases("next")]
    public async Task SkipAsync(CommandContext ctx) {
        // don't allow skipping more at the same time
        try {
            await _semaphore.WaitAsync();
            var track = GuildMusic.queue.NowPlaying.track;
            await GuildMusic.queue.StopAsync();
            await common.respond(ctx,
                $"{track.Info.Title.Sanitize().Bold()} by {track.Info.Author.Sanitize().Bold()} skipped.");
        }
        finally {
            _semaphore.Release();
        }
    }

    [Command("skip"), Description("Skips current track.")]
    public async Task SkipAsync(CommandContext ctx, int num) {
        // don't allow skipping more at the same time
        try {
            await _semaphore.WaitAsync();
            for (int i = 0; i < num; i++) {
                var track = GuildMusic.queue.NowPlaying.track;
                await GuildMusic.queue.StopAsync();
                await common.respond(ctx,
                    $"{track.Info.Title.Sanitize().Bold()} by {track.Info.Author.Sanitize().Bold()} skipped.");
                await Task.Delay(500); // wait for the next one
            }
        }
        finally {
            _semaphore.Release();
        }
    }

    [Command("seek"), Description("Seeks to specified time in current track.")]
    public async Task SeekAsync(CommandContext ctx, [Description("Which time point to seek to.")] TimeSpan position) {
        await GuildMusic.SeekAsync(position, false);
        await common.respond(ctx, $"Seeking to {position.ToDurationString()}...");
    }

    [Command("forward"), Description("Forwards the track by specified amount of time.")]
    public async Task ForwardAsync(CommandContext ctx, [Description("By how much to forward.")] TimeSpan offset) {
        await GuildMusic.SeekAsync(offset, true);
        await common.respond(ctx, $"Seeking forward by {offset.ToDurationString()}...");
    }

    [Command("rewind"), Description("Rewinds the track by specified amount of time.")]
    public async Task RewindAsync(CommandContext ctx, [Description("By how much to rewind.")] TimeSpan offset) {
        await GuildMusic.SeekAsync(-offset, true);
        await common.respond(ctx, $"Seeking backward by {offset.ToDurationString()}...");
    }

    [Command("volume"), Description("Sets playback volume."), Aliases("v")]
    public async Task SetVolumeAsync(CommandContext ctx,
        [Description("Volume to set. Can be 0-150. Default 100.")]
        int volume) {
        if (volume is < 0 or > 1000) {
            await common.respond(ctx, "Volume must be greater than 0, and less than or equal to 1000.");
            return;
        }

        await GuildMusic.SetVolumeAsync(volume);
        await common.respond(ctx, $"Volume set to {GuildMusic.effectiveVolume}%.");
    }

    [Command("volume"), Description("Gets playback volume.")]
    public async Task GetVolumeAsync(CommandContext ctx) {
        await common.respond(ctx,
            $"Volume is {GuildMusic.volume} * {GuildMusic.artistVolume} = {GuildMusic.effectiveVolume}%.");
    }

    [Command("restart"), Description("Restarts the playback of the current track.")]
    public async Task RestartAsync(CommandContext ctx) {
        var track = GuildMusic.queue.NowPlaying.track;
        await GuildMusic.queue.RestartAsync();
        await common.respond(ctx,
            $"{track.Info.Title.Sanitize().Bold()} by {track.Info.Author.Sanitize().Bold()} restarted.");
    }

    [Command("remove"), Description("Removes a track from playback queue."), Aliases("del", "rm")]
    public async Task RemoveAsync(CommandContext ctx, [Description("Which track to remove.")] int index) {
        var itemN = GuildMusic.queue.Remove(index - 1);
        if (itemN == null) {
            await common.respond(ctx, "No such track.");
            return;
        }

        await common.respond(ctx,
            $"{itemN.Info.Title.Sanitize().Bold()} by {itemN.Info.Author.Sanitize().Bold()} removed.");
    }

    [Command("queue"), Description("Displays current playback queue."), Aliases("q")]
    public async Task QueueAsync(CommandContext ctx) {
        var track = GuildMusic.queue.NowPlaying;
        if (track == default && GuildMusic.queue.Queue.Count == 0 && GuildMusic.queue.autoQueue.Count == 0) {
            await common.respond(ctx, "Queue is empty!");
            return;
        }

        var isPlaying = track != default;
        var interactivity = ctx.Client.GetInteractivity();
        var queue = GuildMusic.queue.getCombinedQueue();
        var pageCount = queue.Count / 10 + 1;
        if (queue.Count % 10 == 0) pageCount--;
        if (!isPlaying || queue.Count == 0) {
            await common.respond(ctx, "Queue is empty!");
            return;
        }
        var pages = queue.Select(x => x.track.ToTrackString())
            .Select((s, i) => (s, i))
            .GroupBy(x => x.i / 10)
            .Select(xg =>
                new Page(
                    $"Now playing: {(isPlaying ? $"{track.track.ToLimitedTrackString()} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{track.track.Info.Length.ToDurationString()}]" : "Nothing".Bold())}\n\n" +
                    $"{string.Join("\n", xg.Select(xa => $"`{xa.i + 1:00}` {xa.s}"))}\n\nPage {xg.Key + 1}/{pageCount}"))
            .ToList();

        // queue is empty but we are playing
        if (pages.Count == 0) {
            pages.Add(new Page(
                $"Now playing: {(isPlaying ? $"{track.track.ToLimitedTrackString()} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{track.track.Info.Length.ToDurationString()}]" : "Nothing".Bold())}"));
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
        var track = GuildMusic.queue.NowPlaying;
        if (track == default) {
            await common.respond(ctx, "Not playing.");
        }
        else {
            await common.respond(ctx,
                $"Now playing: {track.track.Info.Title.Sanitize().Bold()} by {track.track.Info.Author.Sanitize().Bold()} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{track.track.Info.Length.ToDurationString()}].");
        }
    }

    [Command("playerinfo"), Description("Displays information about current player."), Aliases("pinfo", "pinf"), Hidden]
    public async Task PlayerInfoAsync(CommandContext ctx) {
        await common.respond(ctx,
            $"Queue length: {GuildMusic.queue.getCombinedQueue().Count}\nVolume: {GuildMusic.volume}%");
    }
}

// when the user is an idiot
public class IdiotException(string message) : Exception(message);

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
        return x != null ? $"{(x.Info.Title ?? "No title").Sanitize().Bold()} by {(x.Info.Author ?? "No Author").Sanitize().Bold()} [{x.Info.Length.ToDurationString()}]" : "";
    }

    public static string ToLimitedTrackString(this LavalinkTrack x) {
        return x != null ? $"{(x.Info.Title ?? "No title").Sanitize().Bold()} by {(x.Info.Author ?? "No Author").Sanitize().Bold()}" : "";
    }

    public static string ReversePath(this string s) {
        return string.Join("\\", s.Split('\\').Reverse());
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
    [JsonProperty("id")]
    public ResponseId Id { get; private set; }

    [JsonProperty("snippet")]
    public ResponseSnippet Snippet { get; private set; }


    public struct ResponseId {
        [JsonProperty("videoId")]
        public string VideoId { get; private set; }
    }

    public struct ResponseSnippet {
        [JsonProperty("title")]
        public string Title { get; private set; }

        [JsonProperty("channelTitle")]
        public string Author { get; private set; }
    }
}