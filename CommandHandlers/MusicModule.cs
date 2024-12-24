using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Text;
using Lavalink4NET;
using Lavalink4NET.Rest.Entities.Tracks;
using Lavalink4NET.Tracks;
using NetCord;
using NetCord.Services;
using NetCord.Services.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Soulseek;
using Spectre.Console;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace EconomyBot;

// TODO implement a CheckBaseAttribute to stop commands from erroring when base prereqs aren't met

public class MusicModule(YouTubeSearchProvider yt, AudioService lavalink) : CommandModule<CommandContext> {
    private MusicService Music { get; set; } = Program.musicService;
    private YouTubeSearchProvider YouTube { get; } = yt;

    public GuildMusicData GuildMusic { get; set; }

    private readonly MusicCommon common = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private static IVoiceGuildChannel? getChannel(CommandContext ctx) {
        ulong? chn = Program.client.Rest.GetGuildUserVoiceStateAsync(ctx.Guild!.Id, ctx.User.Id).GetAwaiter().GetResult()?.ChannelId;
        return chn == null ? null : (IVoiceGuildChannel?)Program.client.Cache.Guilds[ctx.Guild.Id].Channels[chn.Value];
    }

    private async Task startPlayer(CommandContext ctx) {
        var chn = getChannel(ctx);
        await GuildMusic.CreatePlayerAsync(ctx, chn);
    }

    private async Task reset() {
        GuildMusic.queue.clearQueue();
        GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
    }

    public async Task BeforeExecutionAsync(CommandContext ctx) {
        if (!Program.lavalinkInit) {
            await MusicCommon.respond(ctx,
                "Lavalink not initialised, can't play music right now. (Check output for details)");
            throw new Exception("Actually, this is not your fault.:)");
        }

        Music = Program.musicService;
        var cmd = ctx.Message.Content[1..].Split()[0];

        if (((GuildUser)ctx.User).GetRoles(ctx.Guild!).Any(r => r.Name.Contains("No music", StringComparison.OrdinalIgnoreCase))) {
            throw new Exception("Leave me alone...");
        }

        if (cmd == "join") {
            GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
            GuildMusic.CommandChannel = (TextGuildChannel)ctx.Channel!;
            return;
        }

        var chn = getChannel(ctx);
        if (chn is null && cmd != "queue") {
            await MusicCommon.respond(ctx, "You need to be in a voice channel.");
            throw new IdiotException("user error");
        }

        var mbr = Program.client.Cache.Guilds[ctx.Guild!.Id].Channels[
            Program.client.Cache.Guilds[ctx.Guild.Id].GetCurrentUserVoiceStateAsync().GetAwaiter().GetResult().ChannelId.GetValueOrDefault()];
        if (mbr is not null && chn != mbr && cmd != "queue") {
            await MusicCommon.respond(ctx, "You need to be in the same voice channel.");
            throw new IdiotException("user error");
        }

        GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
        GuildMusic.CommandChannel = (TextGuildChannel)ctx.Channel!;
    }

    [Command("eq"), Description("Enable EQ.")]
    public async Task eq(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        GuildMusic.toggleEQ();
        if (GuildMusic.eq) {
            await MusicCommon.respond(ctx, "Enabled EQ.");
        }
        else {
            await MusicCommon.respond(ctx, "Disabled EQ.");
        }
    }

    [Command("reset"), Description("Reset the voice state.")]
    public async Task ResetAsync(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        await reset();
        await GuildMusic.DestroyPlayerAsync();
    }

    [Command("join", Priority = 1), Description("Joins the voice channel.")]
    public async Task JoinAsync(CommandContext ctx) {
        // yeet the bot in
        await startPlayer(ctx);
        await MusicCommon.respond(ctx, "Joined the channel.");
    }

    [Command("join", Priority = 0), Description("Joins the voice channel.")]
    public async Task JoinAsync(CommandContext ctx, GuildUser member) {
        // yeet the bot in
        await startPlayer(ctx);
        await MusicCommon.respond(ctx, "Joined the channel.");
    }

    [Command("jazz", "j", Priority = 1), Description("Plays some jazz. :3")]
    public async Task PlayJazzAsync(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        // yeet the bot in
        GuildMusic.queue.addToQueue("_fats");
        await GuildMusic.queue.seedQueue();
        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();
        await MusicCommon.respond(ctx, "Started playing jazz.");
    }

    [Command("live", "l", Priority = 1), Description("Live music! :3")]
    public async Task PlayLiveAsync(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        // yeet the bot in
        GuildMusic.queue.addToQueue("_fatslive");
        await GuildMusic.queue.seedQueue();
        await startPlayer(ctx);
        await GuildMusic.queue.PlayAsync();
        await MusicCommon.respond(ctx, "Started playing jazz.");
    }

    [Command("analyse", "an"), Description("Analyse the frequency of artists.")]
    public async Task AnalyseAsync(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        var sum = GuildMusicData.artistWeights.Values.Sum();

        var weights = GuildMusicData.artistWeights.Select(w => $"{w.Key}: {w.Value}");
        var weightsp = GuildMusicData.artistWeights.Select(
            w => $"{w.Key}: {w.Value / sum * 100:#.##}%");

        // get number of tracks per artist
        var tracks = new Dictionary<string, int>();
        foreach (string artist in GuildMusicData.artistWeights.Keys) {
            var path = GuildMusicData.getPath(GuildMusicData.artistMappings[artist].path);
            try {
                var files = Directory.GetFiles(path, "*",
                    new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }).Where(GuildMusicData.extensionFilter).ToArray();
                tracks[artist] = files.Length;
            }
            catch (Exception e) {
                tracks[artist] = 0;
            }
        }

        await MusicCommon.respond(ctx, $"Weights:\n{string.Join("\n", weights)}");
        await MusicCommon.respond(ctx, $"Weights (percent):\n{string.Join("\n", weightsp)}");
        await MusicCommon.respond(ctx, $"Tracks:\n{string.Join("\n", tracks.Select(t => $"{t.Key}: {t.Value}"))}");
    }

    [Command("rl"), Description("Reloads music data.")]
    public async Task ReloadAsync(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        GuildMusicData.reload();
        await MusicCommon.respond(ctx, "Reloaded music data.");
    }

    [Command("stopjazz"), Description("Stops jazz.")]
    public async Task StopJazzAsync(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        await reset();
        await MusicCommon.respond(ctx, "Stopped jazz.");
    }

    [Command("play", "p", Priority = 1), Description("Plays supplied URL or searches for specified keywords.")]
    public async Task PlayAsync(CommandContext ctx,
        [Description("URL to play from.")] Uri uri) {
        await BeforeExecutionAsync(ctx);
        var trackLoad = await lavalink.Tracks.LoadTracksAsync(uri.ToString(), TrackSearchMode.YouTube);
        var result = trackLoad;
        var tracks = result.Tracks;
        if (trackLoad.IsFailed) {
            await MusicCommon.respond(ctx, "No tracks were found at specified link.");
            return;
        }

        var trackCount = tracks.Length;
        foreach (var track in tracks) {
            GuildMusic.queue.Enqueue(track);
        }

        var chn = getChannel(ctx);
        await GuildMusic.CreatePlayerAsync(ctx, chn);
        await GuildMusic.queue.PlayAsync();

        if (trackCount > 1)
            await MusicCommon.respond(ctx, $"Added {trackCount:#,##} tracks to playback queue.");
        else {
            var track = tracks.First();
            await MusicCommon.respond(ctx,
                $"Added {track.ToLimitedTrackString()} to the playback queue.");
        }
    }

    [Command("jazz", "pj", Priority = 0)]
    public async Task PlayJazzAsync(CommandContext ctx,
        [CommandParameter(Remainder = true), Description("Terms to search for.")]
        string term) {
        if (term == "all") {
            GuildMusic.queue.addAllToQueue();
            await GuildMusic.queue.seedQueue();

            await startPlayer(ctx);
            await GuildMusic.queue.PlayAsync();
            await MusicCommon.respond(ctx, $"Started playing {GuildMusic.queue.artistQueue.Count} cats.");
            return;
        }

        var interactivity = ctx.Client.GetInteractivity();

        List<LavalinkTrack> results = (await GuildMusic.getJazz("*" + term + "*")).Where(t => t != null).ToList()!;
        if (results.Count == 0) {
            await MusicCommon.respond(ctx, "Nothing was found.");
            return;
        }

        LavalinkTrack? track;
        object? track_;
        if (results.Count == 1) {
            // only one result
            var el_ = results.First();
            track_ = el_;
            if (track_ == null) {
                await MusicCommon.respond(ctx, "No tracks were found at specified link.");
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
            await MusicCommon.respond(ctx,
                $"Added {track.ToLimitedTrackString()} to the playback queue.");
            return;
        }

        var pageCount = results.Count / 10 + 1;
        if (results.Count % 10 == 0) {
            pageCount--;
        }

        var content = results.Select((x, i) => (x, i))
            .GroupBy(e => e.i / 10)
            .Select(xg => new Page(
                $"{string.Join("\n", xg.Select(xa => $"`{xa.i + 1}` {xa.x.ToLimitedTrackString()}"))}\n\nPage {xg.Key + 1}/{pageCount}"));

        var ems = new PaginationEmojis {
            SkipLeft = null,
            SkipRight = null,
            Stop = DiscordEmoji.FromUnicode("⏹"),
            Left = DiscordEmoji.FromUnicode("◀"),
            Right = DiscordEmoji.FromUnicode("▶")
        };

        if (pageCount == 1) {
            await ctx.Channel.SendMessageAsync(content.First().Content);
        }
        else {
            _ = interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, content, ems,
                PaginationBehaviour.Ignore,
                PaginationDeletion.KeepEmojis, TimeSpan.FromMinutes(2));
        }

        var msgC =
            $"Type a number 1-{results.Count} to queue a track. To cancel, type cancel or {MusicCommon.NumberMappingsReverse.Last()}.";

        var msg = await ctx.ReplyAsync(msgC);

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
            await MusicCommon.modify(ctx, msg, "Invalid choice was made.");
            return;
        }

        if (elInd == -1) {
            await MusicCommon.modify(ctx, msg, "Choice cancelled.");
            return;
        }

        var el = results.ElementAt(elInd - 1);
        track_ = el;


        if (track_ == null) {
            await MusicCommon.modify(ctx, msg, "No tracks were found at specified link.");
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
        await MusicCommon.modify(ctx, msg,
            $"Added {track.ToLimitedTrackString()} to the playback queue.");
    }


    /// <summary>
    /// Note to Mr. or Ms. Library Author.
    /// I won't make my bot GPL just because you want me to. I'm not using your library to make money.
    /// I am not creating a "derivative work" using your library because the bot is perfectly functional without this functionality.
    /// This is "mere aggregation" in GPL-speak.
    /// P.S. fuck copyright as a concept
    /// </summary>
    [Command("soulseek", "slsk", Priority = 0)]
    public async Task PlaySLSKAsync(
        [CommandParameter(Remainder = true), Description("Terms to search for.")]
        string term) {
        await BeforeExecutionAsync(Context);

        var interactivity = Context.Client.GetInteractivity();
        if (string.IsNullOrWhiteSpace(term)) {
            await MusicCommon.respond(Context, "No query was entered :(");
            return;
        }

        var results = await Music.getSLSK(term);
        if (results.Count == 0) {
            await MusicCommon.respond(Context, "Nothing was found.");
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

        if (pageCount == 1) {
            await ctx.Channel.SendMessageAsync(content.First().Content);
        }
        else {
            _ = interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, content, ems,
                PaginationBehaviour.Ignore,
                PaginationDeletion.KeepEmojis, TimeSpan.FromMinutes(2));
        }

        var msgC =
            $"Type a number 1-{results.Count} to queue a track. To cancel, type cancel or {MusicCommon.NumberMappingsReverse.Last()}.";

        var msg = await ctx.ReplyAsync(msgC);

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
                await MusicCommon.modify(Context, msg, "Invalid choice was made.");
                return;
            }
        }

        else if (elInd < 0 || elInd > results.Count) {
            await MusicCommon.modify(Context, msg, "Invalid choice was made.");
            return;
        }

        if (elInd == -1) {
            await MusicCommon.modify(Context, msg, "Choice cancelled.");
            return;
        }

        var chosen = results.ElementAt(elInd - 1);


        // actually download it from soulseek
        var slsk = Music.slsk;
        const string tempFolder = "/snd/music/temp";

        // basically, the "filename" goes like this:
        // for example: @@xrknr\Music\Dream Theater\2002 - six degrees of inner turbulence (flac)\(09) [Dream Theater] IV. The Test That Stumped Them All.flac
        // we want the LAST part of the path as the actual filename to download to.
        var actualFilename = chosen.file.Filename.Split('\\').Last();

        await MusicCommon.modify(Context, msg, $"Downloading {actualFilename}...");

        // we hash the filename so we don't reDL the same file
        var hash = chosen.file.Filename.GetHashCode().ToString("x8");
        var localPath = Path.Join(tempFolder, hash, actualFilename);
        // if hash exists, play from that
        // if not, create folder
        LavalinkTrack? lltrack;
        // if the hash directory exists + the file exists
        if (Directory.Exists(Path.Join(tempFolder, hash)) &&
            File.Exists(Path.Join(tempFolder, hash, actualFilename))) {
            lltrack = await GuildMusicData.getTrackAsync(Program.LavalinkNode, localPath);
        }
        else {
            // create the folder
            Directory.CreateDirectory(Path.Join(tempFolder, hash));
            try {
                var dl = await slsk.DownloadAsync(chosen.response.Username, chosen.file.Filename, localPath);
            }
            catch (TimeoutException e) {
                AnsiConsole.WriteLine(e.ToString());
                await MusicCommon.modify(Context, msg, "Download timed out...");
                return;
            }
            lltrack = await GuildMusicData.getTrackAsync(Program.LavalinkNode, localPath);
        }

        GuildMusic.queue.Enqueue(lltrack);
        await startPlayer(Context);
        await GuildMusic.queue.PlayAsync();

        /*if (trackCount > 1) {
            await common.modify(ctx, msg, $"Added {trackCount:#,##0} tracks to playback queue.");
        }
        else {*/
        await MusicCommon.modify(Context, msg,
            $"Added {lltrack.ToLimitedTrackString()} to the playback queue.");
    }

    [Command("play", Priority = 0)]
    public async Task PlayAsync(
        [CommandParameter(Remainder = true), Description("Terms to search for.")]
        string term) {
        var interactivity = Context.Client.GetInteractivity();

        var results = (await YouTube.SearchAsync(term)).ToList();
        if (!results.Any()) {
            await MusicCommon.respond(Context, "Nothing was found.");
            return;
        }

        var msgC = string.Join("\n",
            results.Select((x, i) =>
                $"{MusicCommon.NumberMappings[i + 1]} {WebUtility.HtmlDecode(x.Title).Sanitize().Bold().URLDecode()} by {WebUtility.HtmlDecode(x.Author).Sanitize().Bold().URLDecode()}"));
        msgC =
            $"{msgC}\n\nType a number 1-{results.Count} to queue a track. To cancel, type cancel or {MusicCommon.NumberMappingsReverse.Last()}.";
        var msg = await Context.ReplyAsync(msgC);

        var res = await interactivity.WaitForMessageAsync(x => x.Author == Context.User, TimeSpan.FromSeconds(30));
        if (res.TimedOut || res.Result == null) {
            await MusicCommon.modify(Context, msg, "No choice was made.");
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
            await MusicCommon.modify(Context, msg, "Invalid choice was made.");
            return;
        }

        if (!MusicCommon.NumberMappings.ContainsKey(elInd)) {
            await MusicCommon.modify(Context, msg, "Invalid choice was made.");
            return;
        }

        if (elInd == -1) {
            await MusicCommon.modify(Context, msg, "Choice cancelled.");
            return;
        }

        var el = results.ElementAt(elInd - 1);
        var url = new Uri($"https://youtu.be/{el.Id}");

        var trackLoad = await Music.GetTracksAsync(url);
        var result = trackLoad.Result;
        List<LavalinkTrack> tracks = [];
        switch (trackLoad.LoadType) {
            case LavalinkLoadResultType.Error:
                await MusicCommon.respond(ctx, "No tracks were found at specified link.");
                return;
            case LavalinkLoadResultType.Playlist: {
                var playlist = (LavalinkPlaylist)result;
                if (playlist.Info.SelectedTrack > 0) {
                    var index = playlist.Info.SelectedTrack;
                    tracks = tracks.Skip(index).Concat(tracks.Take(index)).ToList();
                }
                break;
            }
            case LavalinkLoadResultType.Search: {
                var search = (List<LavalinkTrack>)result;
                tracks = search;
                break;
            }
            case LavalinkLoadResultType.Track: {
                var search = (LavalinkTrack)result;
                tracks = [search];
                break;
            }
            case LavalinkLoadResultType.Empty:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var trackCount = tracks.Count;
        foreach (var track in tracks) {
            GuildMusic.queue.Enqueue(track);
        }

        await startPlayer(Context);
        await GuildMusic.queue.PlayAsync();

        if (trackCount > 1) {
            await MusicCommon.modify(Context, msg, $"Added {trackCount:#,##0} tracks to playback queue.");
        }
        else {
            var track = tracks.First();
            await MusicCommon.modify(Context, msg,
                $"Added {track.ToLimitedTrackString()} to the playback queue.");
        }
    }

    [Command("artist", "a"), Description("Plays tracks from an matchedArtist.")]
    public async Task ArtistAsync([CommandParameter(Remainder = true)] string artist) {
        await BeforeExecutionAsync(Context);
        string matchedArtist =
            GuildMusicData.artistMappings.Keys.MaxBy(values => ActualFuzz.partialFuzz(artist, values))!;
        GuildMusic.queue.addToQueue(matchedArtist);

        await GuildMusic.queue.seedQueue();

        await startPlayer(Context);
        await GuildMusic.queue.PlayAsync();
        await MusicCommon.respond(Context, $"Started playing {matchedArtist}.");
    }

    [Command("stopartist", "sa"), Description("Stops playing tracks from an matchedArtist.")]
    public async Task StopArtistAsync() {
        await BeforeExecutionAsync(Context);
        GuildMusic.queue.clearQueue();

        int rmd = GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        await GuildMusic.DestroyPlayerAsync();

        await MusicCommon.respond(CommandContext, $"Removed {rmd:#,##0} tracks from the queue.");
    }

    [Command("stop"), Description("Stops playback and quits the voice channel.")]
    public async Task StopAsync() {
        await BeforeExecutionAsync(Context);
        int rmd = GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        GuildMusic.queue.clearQueue();
        await GuildMusic.DestroyPlayerAsync();

        await MusicCommon.respond(Context, $"Removed {rmd:#,##0} tracks from the queue.");
    }

    [Command("repeat"), Description("Toggles repeat of the queue.")]
    public async Task RepeatAsync() {
        await BeforeExecutionAsync(Context);
        bool repeat = GuildMusic.queue.repeatQueue;
        GuildMusic.queue.repeatQueue = !repeat;
        // seed the queue if it doesn't exist and we are playing a manual song
        if (GuildMusic.queue.NowPlaying != null && GuildMusic.queue.NowPlaying.artist == null &&
            GuildMusic.queue.Queue.Count == 0) {
            GuildMusic.queue.Queue.Add(GuildMusic.queue.NowPlaying);
        }

        if (repeat) {
            await MusicCommon.respond(Context, "Disabled repeat.");
        }
        else {
            await MusicCommon.respond(Context, "Enabled repeat.");
        }
    }

    [Command("earrape", "er"), Description("Toggles annoying users.")]
    public async Task EarrapeAsync() {
        await BeforeExecutionAsync(Context);
        bool earrape = GuildMusic.queue.earrapeMode;
        GuildMusic.queue.earrapeMode = !earrape;
        if (earrape) {
            await MusicCommon.respond(Context, "Disabled earrape mode.");
        }
        else {
            await MusicCommon.respond(Context, "Enabled earrape mode.");
        }
    }

    [Command("clear"), Description("Clears the queue.")]
    public async Task ClearAsync() {
        await BeforeExecutionAsync(Context);
        int rmd = GuildMusic.queue.EmptyQueue();
        GuildMusic.queue.clearQueue();

        await MusicCommon.respond(Context, $"Removed {rmd:#,##0} tracks from the queue uwu");
    }

    [Command("pause"), Description("Pauses playback.")]
    public async Task PauseAsync() {
        await BeforeExecutionAsync(Context);
        await GuildMusic.PauseAsync();
        await MusicCommon.respond(Context,
            $"Playback paused. Use {".resume".InlineCode()} to resume playback.");
    }

    [Command("resume", "unpause"), Description("Resumes playback.")]
    public async Task ResumeAsync() {
        await BeforeExecutionAsync(Context);
        await GuildMusic.ResumeAsync();
        await MusicCommon.respond(Context, "Playback resumed.");
    }

    [Command("skip", "next"), Description("Skips current track.")]
    public async Task SkipAsync() {
        await BeforeExecutionAsync(Context);
        // don't allow skipping more at the same time
        try {
            await _semaphore.WaitAsync();
            var track = GuildMusic.queue.NowPlaying.track;
            await GuildMusic.queue.StopAsync();
            await MusicCommon.respond(Context,
                $"{track.ToLimitedTrackString()} skipped.");
        }
        finally {
            _semaphore.Release();
        }
    }

    [Command("skip"), Description("Skips current track.")]
    public async Task SkipAsync(int num) {
        await BeforeExecutionAsync(Context);
        // don't allow skipping more at the same time
        try {
            await _semaphore.WaitAsync();
            for (int i = 0; i < num; i++) {
                var track = GuildMusic.queue.NowPlaying.track;
                await GuildMusic.queue.StopAsync();
                await MusicCommon.respond(Context,
                    $"{track.ToLimitedTrackString()} skipped.");
                await Task.Delay(500); // wait for the next one
            }
        }
        finally {
            _semaphore.Release();
        }
    }

    [Command("seek"), Description("Seeks to specified time in current track.")]
    public async Task SeekAsync([Description("Which time point to seek to.")] TimeSpan position) {
        await BeforeExecutionAsync(Context);
        await GuildMusic.SeekAsync(position, false);
        await MusicCommon.respond(Context, $"Seeking to {position.ToDurationString()}...");
    }

    [Command("forward"), Description("Forwards the track by specified amount of time.")]
    public async Task ForwardAsync([Description("By how much to forward.")] TimeSpan offset) {
        await BeforeExecutionAsync(Context);
        await GuildMusic.SeekAsync(offset, true);
        await MusicCommon.respond(Context, $"Seeking forward by {offset.ToDurationString()}...");
    }

    [Command("rewind"), Description("Rewinds the track by specified amount of time.")]
    public async Task RewindAsync([Description("By how much to rewind.")] TimeSpan offset) {
        await BeforeExecutionAsync(Context);
        await GuildMusic.SeekAsync(-offset, true);
        await MusicCommon.respond(Context, $"Seeking backward by {offset.ToDurationString()}...");
    }

    [Command("volume", "v"), Description("Sets playback volume.")]
    public async Task SetVolumeAsync(
        [Description("Volume to set. Can be 0-150. Default 100.")]
        int volume) {
        await BeforeExecutionAsync(Context);
        if (volume is < 0 or > 1000) {
            await MusicCommon.respond(Context, "Volume must be greater than 0, and less than or equal to 1000.");
            return;
        }

        await GuildMusic.SetVolumeAsync(volume);
        await MusicCommon.respond(Context, $"Volume set to {GuildMusic.effectiveVolume}%.");
    }

    [Command("volume"), Description("Gets playback volume.")]
    public async Task GetVolumeAsync() {
        await BeforeExecutionAsync(Context);
        await MusicCommon.respond(Context,
            $"Volume is {GuildMusic.volume} * {GuildMusic.artistVolume} = {GuildMusic.effectiveVolume}%.");
    }

    [Command("restart"), Description("Restarts the playback of the current track.")]
    public async Task RestartAsync() {
        await BeforeExecutionAsync(Context);
        var track = GuildMusic.queue.NowPlaying.track;
        await GuildMusic.queue.RestartAsync();
        await MusicCommon.respond(Context,
            $"{track.ToLimitedTrackString()} restarted.");
    }

    [Command("remove", "del", "rm"), Description("Removes a track from playback queue.")]
    public async Task RemoveAsync([Description("Which track to remove.")] int index) {
        await BeforeExecutionAsync(Context);
        var itemN = GuildMusic.queue.Remove(index - 1);
        if (itemN == null) {
            await MusicCommon.respond(Context, "No such track.");
            return;
        }

        await MusicCommon.respond(Context,
            $"{itemN.ToLimitedTrackString()} removed.");
    }

    [Command("queue", "q"), Description("Displays current playback queue.")]
    public async Task QueueAsync() {
        await BeforeExecutionAsync(Context);
        var track = GuildMusic.queue.NowPlaying;
        if (track == null && GuildMusic.queue.Queue.Count == 0 && GuildMusic.queue.autoQueue.Count == 0) {
            await MusicCommon.respond(ctx, "Queue is empty!");
            return;
        }

        var isPlaying = track != null;
        var interactivity = ctx.Client.GetInteractivity();
        var queue = GuildMusic.queue.getCombinedQueue();
        var pageCount = queue.Count / 10 + 1;
        if (queue.Count % 10 == 0) pageCount--;
        if (!isPlaying || queue.Count == 0) {
            await MusicCommon.respond(ctx, "Queue is empty!");
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
        if (pageCount == 1) {
            await ctx.Channel.SendMessageAsync(pages.First().Content);
        }
        else {
            _ = interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages, ems,
                PaginationBehaviour.Ignore,
                PaginationDeletion.KeepEmojis, TimeSpan.FromMinutes(2));
        }
    }

    [Command("nowplaying", "np"), Description("Displays information about currently-played track.")]
    public async Task NowPlayingAsync() {
        await BeforeExecutionAsync(Context);
        var track = GuildMusic.queue.NowPlaying;
        if (track == null) {
            await MusicCommon.respond(Context, "Not playing.");
        }
        else {
            await MusicCommon.respond(Context,
                $"Now playing: {track.track.ToLimitedTrackString()} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{track.track.Duration.ToDurationString()}].");
        }
    }

    [Command("playerinfo", "pinfo", "pinf"), Description("Displays information about current player.")]
    public async Task PlayerInfoAsync(CommandContext ctx) {
        await BeforeExecutionAsync(ctx);
        await MusicCommon.respond(ctx,
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
    public static string ToTrackString(this LavalinkTrack? x) {
        return x != null ? $"{(x.Title ?? "No title").Sanitize().Bold().URLDecode()} by {(x.Author ?? "No Author").Sanitize().Bold().URLDecode()} [{x.Duration.ToDurationString()}]" : "";
    }

    public static string URLDecode(this string title) {
        return WebUtility.HtmlDecode(WebUtility.UrlDecode(title));
    }

    public static string ToLimitedTrackString(this LavalinkTrack? x) {
        return x != null ? $"{(x.Title ?? "No title").Sanitize().Bold().URLDecode()} by {(x.Author ?? "No Author").Sanitize().Bold().URLDecode()}" : "";
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