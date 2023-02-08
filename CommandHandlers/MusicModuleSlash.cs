using System.Collections.Immutable;
using System.Globalization;
using System.Net;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.Lavalink;
using DSharpPlus.SlashCommands;
using DSharpPlus.SlashCommands.Attributes;

namespace EconomyBot;

[SlashModuleLifespan(SlashModuleLifespan.Singleton)]
public class MusicModuleSlash : ApplicationCommandModule {
    private static ImmutableDictionary<int, DiscordEmoji> NumberMappings { get; }

    private static ImmutableDictionary<DiscordEmoji, int> NumberMappingsReverse { get; }
    private static ImmutableArray<DiscordEmoji> Numbers { get; }

    private MusicService Music { get; }
    private YouTubeSearchProvider YouTube { get; }

    public GuildMusicData GuildMusic { get; set; }

    public MusicModuleSlash(YouTubeSearchProvider yt) {
        Music = Program.musicService;
        YouTube = yt;
    }

    static MusicModuleSlash() {
        var iab = ImmutableArray.CreateBuilder<DiscordEmoji>();
        iab.Add(DiscordEmoji.FromUnicode("1\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("2\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("3\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("4\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("5\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("6\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("7\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("8\u20e3"));
        iab.Add(DiscordEmoji.FromUnicode("9\u20e3"));
        iab.Add(DiscordEmoji.FromName(Program.client, ":keycap_ten:"));
        iab.Add(DiscordEmoji.FromUnicode("\u274c"));
        Numbers = iab.ToImmutable();

        var idb = ImmutableDictionary.CreateBuilder<int, DiscordEmoji>();
        idb.Add(1, DiscordEmoji.FromUnicode("1\u20e3"));
        idb.Add(2, DiscordEmoji.FromUnicode("2\u20e3"));
        idb.Add(3, DiscordEmoji.FromUnicode("3\u20e3"));
        idb.Add(4, DiscordEmoji.FromUnicode("4\u20e3"));
        idb.Add(5, DiscordEmoji.FromUnicode("5\u20e3"));
        idb.Add(6, DiscordEmoji.FromUnicode("6\u20e3"));
        idb.Add(7, DiscordEmoji.FromUnicode("7\u20e3"));
        idb.Add(8, DiscordEmoji.FromUnicode("8\u20e3"));
        idb.Add(9, DiscordEmoji.FromUnicode("9\u20e3"));
        idb.Add(10, DiscordEmoji.FromName(Program.client, ":keycap_ten:"));
        idb.Add(-1, DiscordEmoji.FromUnicode("\u274c"));
        NumberMappings = idb.ToImmutable();
        var idb2 = ImmutableDictionary.CreateBuilder<DiscordEmoji, int>();
        idb2.AddRange(NumberMappings.ToDictionary(x => x.Value, x => x.Key));
        NumberMappingsReverse = idb2.ToImmutable();
    }

    public override async Task<bool> BeforeSlashExecutionAsync(InteractionContext ctx) {
        if (ctx.CommandName == "join") {
            GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
            GuildMusic.CommandChannel = ctx.Channel;
            return false;
        }

        var vs = ctx.Member.VoiceState;
        var chn = vs?.Channel;
        if (chn == null) {
            await ctx.CreateResponseAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} You need to be in a voice channel.");
            throw new Exception();
        }

        var mbr = ctx.Guild.CurrentMember?.VoiceState?.Channel;
        if (mbr != null && chn != mbr) {
            await ctx.CreateResponseAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} You need to be in the same voice channel.");
            throw new Exception();
        }

        GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
        GuildMusic.CommandChannel = ctx.Channel;
        
        return true;
    }

    [SlashCommand("join", "Joins the voice channel.")]
    public async Task JoinAsync(InteractionContext ctx) {
        // yeet the bot in 
        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Joined the channel.");
    }

    [SlashCommand("jazz", "Plays some jazz. :3")]
    public async Task PlayJazzAsync(InteractionContext ctx) {
        // yeet the bot in
        await GuildMusic.StartJazz();
        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();
        await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Started playing jazz.");
    }

    [SlashCommand("stopjazz", "Stops jazz.")]
    public async Task StopJazzAsync(InteractionContext ctx) {
        GuildMusic.StopJazz();
        int rmd = GuildMusic.EmptyQueue();
        await GuildMusic.StopAsync();
        await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Stopped jazz.");
    }

    /*[SlashCommand("play", "Plays supplied URL or searches for specified keywords.")]
    public async Task PlayAsync(InteractionContext ctx,
        [Option("url", "URL to play from.")] Uri uri) {
        var trackLoad = await Music.GetTracksAsync(uri);
        var tracks = trackLoad.Tracks;
        if (trackLoad.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks.Any()) {
            await ctx.CreateResponseAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link.");
            return;
        }

        if (GuildMusic.isShuffled) {
            tracks = Music.Shuffle(tracks);
        }
        else if (trackLoad.LoadResultType == LavalinkLoadResultType.PlaylistLoaded &&
                 trackLoad.PlaylistInfo.SelectedTrack > 0) {
            var index = trackLoad.PlaylistInfo.SelectedTrack;
            tracks = tracks.Skip(index).Concat(tracks.Take(index));
        }

        var trackCount = tracks.Count();
        foreach (var track in tracks)
            GuildMusic.Enqueue(track);

        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();

        if (trackCount > 1)
            await ctx.CreateResponseAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount:#,##0} tracks to playback queue.");
        else {
            var track = tracks.First();
            await ctx.CreateResponseAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue.");
        }
    }*/

    [SlashCommand("searchjazz", "Plays jazz. :3")]
    public async Task PlayJazzAsync(InteractionContext ctx,
        [Option("search", "Terms to search for.")]
        string term) {
        var interactivity = ctx.Client.GetInteractivity();

        var results = await GuildMusic.StartJazz("*" + term + "*");
        if (!results.Any()) {
            await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Nothing was found.");
            return;
        }

        if (results.Count == 1) {
            // only one result
            var el_ = results[0];
            var tracks_ = el_.Tracks;
            if (el_.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks_.Any()) {
                await ctx.CreateResponseAsync(
                    $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link.");
                return;
            }

            if (GuildMusic.isShuffled)
                tracks_ = Music.Shuffle(tracks_);
            var trackCount_ = tracks_.Count();
            foreach (var track in tracks_)
                GuildMusic.Enqueue(track);

            var vs_ = ctx.Member.VoiceState;
            var chn_ = vs_.Channel;
            await GuildMusic.CreatePlayerAsync(chn_);
            await GuildMusic.PlayAsync();

            if (trackCount_ > 1) {
                await ctx.CreateResponseAsync(
                    $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount_:#,##0} tracks to playback queue.");
            }
            else {
                var track = tracks_.First();
                await ctx.CreateResponseAsync(
                    $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue.");
            }

            return;
        }

        var pageCount = results.Count / 10 + 1;
        if (results.Count % 10 == 0) pageCount--;
        var pages = results.Select((x, i) => x)
            .Select((s, i) => new { str = s, index = i })
            .GroupBy(x => x.index / 10)
            .Select(xg =>
                new Page(
                    $"{string.Join("\n", xg.Select(xa => $"`{xa.index + 1:00}` {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(xa.str.Tracks.First().Title)))} by {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(xa.str.Tracks.First().Author)))}"))}\n\nPage {xg.Key + 1}/{pageCount}"))
            .ToArray();

        var ems = new PaginationEmojis {
            SkipLeft = null,
            SkipRight = null,
            Stop = DiscordEmoji.FromUnicode("⏹"),
            Left = DiscordEmoji.FromUnicode("◀"),
            Right = DiscordEmoji.FromUnicode("▶")
        };
        interactivity.SendPaginatedMessageAsync(ctx.Channel, ctx.User, pages, ems, PaginationBehaviour.Ignore,
            PaginationDeletion.KeepEmojis, TimeSpan.FromMinutes(2)).ConfigureAwait(false);


        //var msgC = string.Join("\n",
        //    results.Select((x, i) => $"{NumberMappings[i + 1]} {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(x.Tracks.First().Title)))} by {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(x.Tracks.First().Author)))}"));
        var msgC =
            $"Type a number 1-{results.Count} to queue a track. To cancel, type cancel or {Numbers.Last()}.";
        await ctx.CreateResponseAsync(msgC);

        //foreach (var emoji in Numbers)
        //    await msg.CreateReactionAsync(emoji);
        //var res = await interactivity.WaitForMessageReactionAsync(x => NumberMappingsReverse.ContainsKey(x), msg, ctx.User, TimeSpan.FromSeconds(30));

        var res = await interactivity.WaitForMessageAsync(x => x.Author == ctx.User, TimeSpan.FromMinutes(2));
        if (res.TimedOut || res.Result == null) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No choice was made."));
            return;
        }

        var resInd = res.Result.Content.Trim();
        if (!int.TryParse(resInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elInd)) {
            if (resInd.ToLowerInvariant() == "cancel") {
                elInd = -1;
            }
            else {
                if (elInd < 0 || elInd > results.Count) {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                        $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made."));
                    return;
                }
            }
        }

        //var elInd = NumberMappingsReverse[res.Emoji];
        if (elInd == -1) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Choice cancelled."));
            return;
        }

        var el = results.ElementAt(elInd - 1);
        //var url = new Uri($"https://youtu.be/{el.Id}");
        //var url = el.Uri;
        //Console.Out.WriteLine(e);

        //var trackLoad = await Music.GetTracksAsync(url);
        //var tracks = trackLoad.Tracks;
        var tracks = el.Tracks;


        if (el.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks.Any()) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link."));
            return;
        }

        if (GuildMusic.isShuffled)
            tracks = Music.Shuffle(tracks);
        var trackCount = tracks.Count();
        foreach (var track in tracks)
            GuildMusic.Enqueue(track);

        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();

        if (trackCount > 1) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount:#,##0} tracks to playback queue."));
        }
        else {
            var track = tracks.First();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue."));
        }
    }

    [SlashCommand("play", "Play a song.")]
    public async Task PlayAsync(InteractionContext ctx,
        [Option("search", "Terms to search for.")]
        string term) {
        var interactivity = ctx.Client.GetInteractivity();

        var results = await YouTube.SearchAsync(term);
        if (!results.Any()) {
            await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Nothing was found.");
            return;
        }

        var msgC = string.Join("\n",
            results.Select((x, i) =>
                $"{NumberMappings[i + 1]} {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(x.Title)))} by {Formatter.Bold(Formatter.Sanitize(WebUtility.HtmlDecode(x.Author)))}"));
        msgC =
            $"{msgC}\n\nType a number 1-{results.Count()} to queue a track. To cancel, type cancel or {Numbers.Last()}.";
        await ctx.CreateResponseAsync(msgC);

        //foreach (var emoji in Numbers)
        //    await msg.CreateReactionAsync(emoji);
        //var res = await interactivity.WaitForMessageReactionAsync(x => NumberMappingsReverse.ContainsKey(x), msg, ctx.User, TimeSpan.FromSeconds(30));

        var res = await interactivity.WaitForMessageAsync(x => x.Author == ctx.User, TimeSpan.FromSeconds(30));
        if (res.TimedOut || res.Result == null) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No choice was made."));
            return;
        }

        var resInd = res.Result.Content.Trim();
        if (!int.TryParse(resInd, NumberStyles.Integer, CultureInfo.InvariantCulture, out var elInd)) {
            if (resInd.ToLowerInvariant() == "cancel") {
                elInd = -1;
            }
            else {
                DiscordEmoji? em;
                try {
                    em = DiscordEmoji.FromUnicode(resInd);
                }
                catch (ArgumentException e) {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                        $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made."));
                    return;
                }

                if (!NumberMappingsReverse.ContainsKey(em)) {
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                        $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made."));
                    return;
                }

                elInd = NumberMappingsReverse[em];
            }
        }
        else if (elInd < 1) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made."));
            return;
        }

        if (!NumberMappings.ContainsKey(elInd)) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Invalid choice was made."));
            return;
        }

        //var elInd = NumberMappingsReverse[res.Emoji];
        if (elInd == -1) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Choice cancelled."));
            return;
        }

        var el = results.ElementAt(elInd - 1);
        var url = new Uri($"https://youtu.be/{el.Id}");

        var trackLoad = await Music.GetTracksAsync(url);
        var tracks = trackLoad.Tracks;
        if (trackLoad.LoadResultType == LavalinkLoadResultType.LoadFailed || !tracks.Any()) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No tracks were found at specified link."));
            return;
        }

        if (GuildMusic.isShuffled)
            tracks = Music.Shuffle(tracks);
        var trackCount = tracks.Count();
        foreach (var track in tracks)
            GuildMusic.Enqueue(track);

        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.PlayAsync();

        if (trackCount > 1) {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {trackCount:#,##0} tracks to playback queue."));
        }
        else {
            var track = tracks.First();
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Added {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} to the playback queue."));
        }
    }

    [SlashCommand("stop", "Stops playback and quits the voice channel.")]
    public async Task StopAsync(InteractionContext ctx) {
        int rmd = GuildMusic.EmptyQueue();
        await GuildMusic.StopAsync();
        await GuildMusic.DestroyPlayerAsync();

        await ctx.CreateResponseAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Removed {rmd:#,##0} tracks from the queue.");
    }

    [SlashCommand("clear", "Clears the queue.")]
    public async Task ClearAsync(InteractionContext ctx) {
        int rmd = GuildMusic.EmptyQueue();

        await ctx.CreateResponseAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Removed {rmd:#,##0} tracks from the queue uwu");
    }

    [SlashCommand("pause", "Pauses playback.")]
    public async Task PauseAsync(InteractionContext ctx) {
        await GuildMusic.PauseAsync();
        await ctx.CreateResponseAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Playback paused. Use {Formatter.InlineCode($"/resume")} to resume playback.");
    }

    [SlashCommand("resume", "Resumes playback.")]
    public async Task ResumeAsync(InteractionContext ctx) {
        await GuildMusic.ResumeAsync();
        await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Playback resumed.");
    }

    [SlashCommand("skip", "Skips current track.")]
    public async Task SkipAsync(InteractionContext ctx) {
        var track = GuildMusic.NowPlaying;
        await GuildMusic.StopAsync();
        await ctx.CreateResponseAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} skipped.");
    }

    [SlashCommand("skipnum", "Skips current track.")]
    public async Task SkipAsync(InteractionContext ctx, [Option("tracks", "How many tracks to skip.")] long num) {
        for (int i = 0; i < num; i++) {
            var track = GuildMusic.NowPlaying;
            await GuildMusic.StopAsync();
            await ctx.CreateResponseAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} skipped.");
            await Task.Delay(500); // wait for the next one
        }
    }

    [SlashCommand("seek", "Seeks to specified time in current track.")]
    public async Task SeekAsync(InteractionContext ctx,
        [Option("seek", "Which time point to seek to.")]
        string position) {
        var pos = (await new CustomTimeSpanConverter().ConvertAsync(position, null)).Value;
        await GuildMusic.SeekAsync(pos, false);
    }

    [SlashCommand("forward", "Forwards the track by specified amount of time.")]
    public async Task ForwardAsync(InteractionContext ctx,
        [Option("forward", "By how much to forward.")]
        string offset) {
        var o = (await new CustomTimeSpanConverter().ConvertAsync(offset, null)).Value;
        await GuildMusic.SeekAsync(o, true);
    }

    [SlashCommand("rewind", "Rewinds the track by specified amount of time.")]
    public async Task RewindAsync(InteractionContext ctx,
        [Option( "rewind", "By how much to rewind.")]
        string offset) {
        var o = (await new CustomTimeSpanConverter().ConvertAsync(offset, null)).Value;
        await GuildMusic.SeekAsync(-o, true);
    }

    [SlashCommand("setvolume", "Sets playback volume.")]
    public async Task SetVolumeAsync(InteractionContext ctx,
        [Option("volume", "Volume to set. Can be 0-150. Default 100.")]
        long volume) {
        if (volume is < 0 or > 1000) {
            await ctx.CreateResponseAsync(
                $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Volume must be greater than 0, and less than or equal to 1000.");
            return;
        }

        await GuildMusic.SetVolumeAsync((int)volume);
        await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Volume set to {volume}%.");
    }

    [SlashCommand("volume", "Gets playback volume.")]
    public async Task GetVolumeAsync(InteractionContext ctx) {
        await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Volume is {GuildMusic.volume}%.");
    }

    [SlashCommand("restart", "Restarts the playback of the current track.")]
    public async Task RestartAsync(InteractionContext ctx) {
        var track = GuildMusic.NowPlaying;
        await GuildMusic.RestartAsync();
        await ctx.CreateResponseAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} restarted.");
    }

    [SlashCommand("shuffle", "Toggles shuffle mode.")]
    public async Task ShuffleAsync(InteractionContext ctx) {
        if (GuildMusic.isShuffled) {
            GuildMusic.StopShuffle();
            await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Queue is no longer shuffled.");
        }
        else {
            GuildMusic.Shuffle();
            await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Queue is now shuffled.");
        }
    }

    [SlashCommand("reshuffle", "Reshuffles the queue. If queue is not shuffled, it won't enable shuffle mode.")]
    public async Task ReshuffleAsync(InteractionContext ctx) {
        GuildMusic.Reshuffle();
        await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} Queue reshuffled.");
    }

    [SlashCommand("remove", "Removes a track from playback queue.")]
    public async Task RemoveAsync(InteractionContext ctx,
        [Option("remove", "Which track to remove.")]
        long index) {
        var itemN = GuildMusic.Remove((int)(index - 1));
        if (itemN == null) {
            await ctx.CreateResponseAsync($"{DiscordEmoji.FromName(ctx.Client, ":cube:")} No such track.");
            return;
        }

        var track = itemN;
        await ctx.CreateResponseAsync(
            $"{DiscordEmoji.FromName(ctx.Client, ":cube:")} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} removed.");
    }

    [SlashCommand("queue", "Displays current playback queue.")]
    public async Task QueueAsync(InteractionContext ctx) {
        var interactivity = ctx.Client.GetInteractivity();

        var pageCount = GuildMusic.Queue.Count / 10 + 1;
        if (GuildMusic.Queue.Count % 10 == 0) pageCount--;
        var pages = GuildMusic.Queue.Select(x => x.ToTrackString())
            .Select((s, i) => new { str = s, index = i })
            .GroupBy(x => x.index / 10)
            .Select(xg =>
                new Page(
                    $"Now playing: {GuildMusic.NowPlaying.ToTrackString()}\n\n{string.Join("\n", xg.Select(xa => $"`{xa.index + 1:00}` {xa.str}"))}\n\nPage {xg.Key + 1}/{pageCount}"))
            .ToArray();

        var trk = GuildMusic.NowPlaying;
        if (!pages.Any()) {
            if (trk?.TrackString == null)
                await ctx.CreateResponseAsync("Queue is empty!");
            else
                await ctx.CreateResponseAsync($"Now playing: {GuildMusic.NowPlaying.ToTrackString()}");

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

    [SlashCommand("nowplaying", "Displays information about currently-played track.")]
    public async Task NowPlayingAsync(InteractionContext ctx) {
        var track = GuildMusic.NowPlaying;
        if (GuildMusic.NowPlaying?.TrackString == null) {
            await ctx.CreateResponseAsync("Not playing.");
        }
        else {
            await ctx.CreateResponseAsync(
                $"Now playing: {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{GuildMusic.NowPlaying.Length.ToDurationString()}].");
        }
    }

    [SlashCommand("playerinfo", "Displays information about current player.")]
    public async Task PlayerInfoAsync(InteractionContext ctx) {
        await ctx.CreateResponseAsync(
            $"Queue length: {GuildMusic.Queue.Count}\nIs shuffled? {(GuildMusic.isShuffled ? "Yes" : "No")}\nVolume: {GuildMusic.volume}%");
    }
}