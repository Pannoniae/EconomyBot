using System.Net;
using DisCatSharp;
using DisCatSharp.Entities;
using DisCatSharp.Interactivity.Entities;
using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using EconomyBot.Logging;
using Soulseek;
using Spectre.Console;
using Directory = System.IO.Directory;
using File = System.IO.File;

namespace EconomyBot;

/// The music commands themselves, shared by the text (MusicModule) and slash (MusicModuleSlash) stubs.
/// One instance per invocation, so the guild's music data can't leak between two servers running commands at once.
public sealed class MusicCommands {
    private const string TEMP_FOLDER = @"D:\music\temp";

    private static readonly Logger logger = Logger.getClassLogger("MusicModule");

    // don't allow skipping more at the same time
    private static readonly SemaphoreSlim skipLock = new(1, 1);

    private static MusicService Music => Program.musicService;

    private readonly Cmd c;
    private readonly GuildMusicData GuildMusic;

    private MusicCommands(Cmd c, GuildMusicData guildMusic) {
        this.c = c;
        GuildMusic = guildMusic;
    }

    /// Checks everything a music command needs before it may run, then runs it.
    /// <paramref name="voiceChecks"/> is off for the commands which work from outside the voice channel (join, queue).
    public static async Task run(Cmd c, Func<MusicCommands, Task> body, bool voiceChecks = true) {
        if (!Program.lavalinkInit) {
            await c.respond(
                $"{Program.cube} Lavalink not initialised, can't play music right now. (Check output for details)");
            return;
        }

        if (c.member.Roles.Any(r => r.Name.Contains("No music", StringComparison.OrdinalIgnoreCase))) {
            await c.respond("Leave me alone...");
            return;
        }

        if (voiceChecks) {
            var chn = c.member.VoiceState?.Channel;
            if (chn is null) {
                await c.respond($"{Program.cube} You need to be in a voice channel.");
                return;
            }

            var mbr = c.guild.CurrentMember?.VoiceState?.Channel;
            if (mbr is not null && chn != mbr) {
                await c.respond($"{Program.cube} You need to be in the same voice channel.");
                return;
            }
        }

        var data = await Music.GetOrCreateDataAsync(c.guild);
        data.CommandChannel = c.channel;
        await body(new MusicCommands(c, data));
    }

    private Task<DiscordMessage> respond(string response) => c.respond($"{Program.cube} {response}");

    private Task modify(DiscordMessage msg, string response) => c.modify(msg, $"{Program.cube} {response}");

    private async Task startPlayer() {
        var chn = c.member.VoiceState?.Channel;
        await GuildMusic.CreatePlayerAsync(chn!);
    }

    private async Task reset() {
        GuildMusic.queue.clearQueue();
        GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
    }

    private void enqueue(LavalinkTrack track) {
        if (c.front) {
            GuildMusic.queue.EnqueueFront(track);
        }
        else {
            GuildMusic.queue.Enqueue(track);
        }
    }

    /// Enqueue, make sure we're in the channel and playing, and say so - in the selection message if there was one.
    private async Task enqueueAndPlay(LavalinkTrack track, DiscordMessage? msg) {
        enqueue(track);
        await startPlayer();
        await GuildMusic.queue.PlayAsync();

        var text = $"Added {track.ToLimitedTrackString()} to the playback queue.";
        if (msg! != null!) {
            await modify(msg, text);
        }
        else {
            await respond(text);
        }
    }

    // Shared search result handling logic
    private async Task<(T? item, DiscordMessage? message)> pick<T>(List<T> results, Func<T, int, string> line,
        Func<T, string> label) where T : class {
        if (results.Count == 0) {
            await respond("Nothing was found.");
            return (null, null);
        }

        if (results.Count == 1) {
            logger.info("Returning only one result");
            return (results[0], null);
        }

        var (index, msg) = await c.choose(results.Select(line).ToList(), results.Select(label).ToList());
        return index < 0 ? (null, msg) : (results[index], msg);
    }

    private Task<(LavalinkTrack? item, DiscordMessage? message)> pickTrack(List<LavalinkTrack> results) => pick(
        results,
        (x, i) => $"`{i + 1}` {x.ToLimitedTrackString()}",
        x => $"{(x.Info.Title ?? "No title").URLDecode()} by {(x.Info.Author ?? "No Author").URLDecode()}");

    private Task<(SLSKResult? item, DiscordMessage? message)> pickSLSK(List<SLSKResult> results) => pick(results,
        (x, i) => $"**{i + 1}.** {WebUtility.HtmlDecode(x.file.Filename.ReversePath()).InlineCode()}" +
                  $" {TimeSpan.FromSeconds(x.file.Length.GetValueOrDefault()).musicLength().Bold()} ({x.file.getBitrateString()})",
        x => $"{WebUtility.HtmlDecode(x.file.Filename.Split('\\').Last())} ({x.file.getBitrateString()})");

    // Shared Soulseek download and playback logic
    private async Task<LavalinkTrack?> downloadAndGetTrack(SLSKResult chosen, DiscordMessage msg) {
        // actually download it from soulseek

        // basically, the "filename" goes like this:
        // for example: @@xrknr\Music\Dream Theater\2002 - six degrees of inner turbulence (flac)\(09) [Dream Theater] IV. The Test That Stumped Them All.flac
        // we want the LAST part of the path as the actual filename to download to.
        var actualFilename = chosen.file.Filename.Split('\\').Last();
        await modify(msg, $"Downloading {actualFilename}...");

        // we hash the filename so we don't reDL the same file
        var hash = chosen.file.Filename.GetHashCode().ToString("x8");
        var localPath = Path.Join(TEMP_FOLDER, hash, actualFilename);

        // if hash exists, play from that
        // if not, create folder

        // if the hash directory exists + the file exists
        if (Directory.Exists(Path.Join(TEMP_FOLDER, hash)) &&
            File.Exists(Path.Join(TEMP_FOLDER, hash, actualFilename))) {
            return await GuildMusicData.getTrackAsync(GuildMusic.Node, localPath);
        }

        Directory.CreateDirectory(Path.Join(TEMP_FOLDER, hash));
        try {
            await Music.slsk.DownloadAsync(chosen.response.Username, chosen.file.Filename, localPath);
            return await GuildMusicData.getTrackAsync(GuildMusic.Node, localPath);
        }
        catch (TimeoutException e) {
            AnsiConsole.WriteLine(e.ToString());
            await modify(msg, "Download timed out...");
            return null;
        }
    }

    public async Task eq() {
        GuildMusic.toggleEQ();
        await respond(GuildMusic.eq ? "Enabled EQ." : "Disabled EQ.");
    }

    public async Task resetPlayer() {
        await reset();
        await GuildMusic.DestroyPlayerAsync();
    }

    public async Task join() {
        // yeet the bot in
        await startPlayer();
        await respond("Joined the channel.");
    }

    public async Task jazz() {
        // yeet the bot in
        GuildMusic.queue.addToQueue("_fats");
        await GuildMusic.queue.seedQueue();
        await startPlayer();
        await GuildMusic.queue.PlayAsync();
        await respond("Started playing jazz.");
    }

    public async Task live() {
        // yeet the bot in
        GuildMusic.queue.addToQueue("_fatslive");
        await GuildMusic.queue.seedQueue();
        await startPlayer();
        await GuildMusic.queue.PlayAsync();
        await respond("Started playing jazz.");
    }

    public async Task analyse() {
        var sum = GuildMusicData.artistWeights.Values.Sum();

        var weights = GuildMusicData.artistWeights.Select(w => $"{w.Key}: {w.Value}");
        var weightsp = GuildMusicData.artistWeights.Select(w => $"{w.Key}: {w.Value / sum * 100:#.##}%");

        // get number of tracks per artist
        var tracks = new Dictionary<string, int>();
        foreach (string artist in GuildMusicData.artistWeights.Keys) {
            var path = GuildMusicData.getPath(GuildMusicData.artistMappings[artist].path);
            try {
                var files = Directory.GetFiles(path, "*",
                    new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive }).Where(GuildMusicData.extensionFilter).ToArray();
                tracks[artist] = files.Length;
            }
            catch (Exception) {
                tracks[artist] = 0;
            }
        }

        await respond($"Weights:\n{string.Join("\n", weights)}");
        await respond($"Weights (percent):\n{string.Join("\n", weightsp)}");
        await respond($"Tracks:\n{string.Join("\n", tracks.Select(t => $"{t.Key}: {t.Value}"))}\nTotal: {tracks.Sum(t => t.Value)}");
    }

    public async Task reload() {
        GuildMusicData.reload();
        await respond("Reloaded music data.");
    }

    public async Task stopJazz() {
        await reset();
        await respond("Stopped jazz.");
    }

    public async Task play(Uri uri) {
        var trackLoad = await Music.GetTracksAsync(uri);
        var result = trackLoad.Result;
        List<LavalinkTrack> tracks = [];
        if (trackLoad.LoadType == LavalinkLoadResultType.Error) {
            await respond($"No tracks were found at specified link. {((LavalinkException)trackLoad.Result).Cause}: {((LavalinkException)trackLoad.Result).Message}");
            return;
        }

        if (trackLoad.LoadType == LavalinkLoadResultType.Playlist) {
            var playlist = (LavalinkPlaylist)result;
            tracks = playlist.Tracks;
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
        if (trackCount == 0) {
            await respond("No tracks were found at specified link.");
            return;
        }

        foreach (var track in tracks) {
            enqueue(track);
        }
        await startPlayer();
        await GuildMusic.queue.PlayAsync();

        if (trackCount > 1) {
            await respond($"Added {trackCount:#,##} tracks to playback queue.");
        }
        else {
            var track = tracks.First();
            await respond($"Added {track.ToLimitedTrackString()} to the playback queue.");
        }
    }

    public async Task play(string term) {
        var r = await Music.SearchTracksAsync(term);
        if (r.LoadType == LavalinkLoadResultType.Error) {
            await respond($"No tracks were found at specified link.  {((LavalinkException)r.Result).Cause}: {((LavalinkException)r.Result).Message}");
            return;
        }

        if (r.LoadType != LavalinkLoadResultType.Search) {
            await respond($"No tracks were found. (this is {r.LoadType}!)");
            return;
        }

        var (track, msg) = await pickTrack((List<LavalinkTrack>)r.Result);
        if (track == null) {
            return;
        }

        await enqueueAndPlay(track, msg);
    }

    public async Task jazz(string term) {
        if (term == "all") {
            GuildMusic.queue.addAllToQueue();
            await GuildMusic.queue.seedQueue();
            await startPlayer();
            await GuildMusic.queue.PlayAsync();
            await respond($"Started playing {GuildMusic.queue.artistQueue.Count} cats.");
            return;
        }

        List<LavalinkTrack> results = (await GuildMusic.getJazz("*" + term + "*")).Where(t => t != null).ToList()!;
        var (track, msg) = await pickTrack(results);
        if (track == null) {
            return;
        }

        await enqueueAndPlay(track, msg);
    }

    /// <summary>
    /// Note to Mr. or Ms. Library Author.
    /// I won't make my bot GPL just because you want me to. I'm not using your library to make money.
    /// I am not creating a "derivative work" using your library because the bot is perfectly functional without this functionality.
    /// This is "mere aggregation" in GPL-speak.
    /// P.S. fuck copyright as a concept
    /// </summary>
    public Task soulseek(string term) => playSLSK(term, () => Music.getSLSK(term));

    public Task soulseekUser(string user, string term) => playSLSK(term, () => Music.getSLSKbyUser(term, user));

    private async Task playSLSK(string term, Func<Task<List<SLSKResult>>> search) {
        if (string.IsNullOrWhiteSpace(term)) {
            await respond("No query was entered :(");
            return;
        }

        var (chosen, msg) = await pickSLSK(await search());
        if (chosen == null) {
            return;
        }

        // a single result skips the selection, so there's no message to report progress in yet
        msg ??= await respond("Found exactly one result.");

        var track = await downloadAndGetTrack(chosen, msg);
        if (track == null) {
            return;
        }

        await enqueueAndPlay(track, msg);
    }

    public async Task artist(string artist) {
        string matchedArtist =
            GuildMusicData.artistMappings.Keys.MaxBy(values => ActualFuzz.partialFuzz(artist, values))!;
        GuildMusic.queue.addToQueue(matchedArtist);

        await GuildMusic.queue.seedQueue();

        await startPlayer();
        await GuildMusic.queue.PlayAsync();
        await respond($"Started playing {matchedArtist}.");
    }

    public async Task stopArtist() {
        GuildMusic.queue.clearQueue();

        int rmd = GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        await GuildMusic.DestroyPlayerAsync();

        await respond($"Removed {rmd:#,##0} tracks from the queue.");
    }

    public async Task stop() {
        int rmd = GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        GuildMusic.queue.clearQueue();
        await GuildMusic.DestroyPlayerAsync();

        await respond($"Removed {rmd:#,##0} tracks from the queue.");
    }

    public async Task repeat() {
        bool repeat = GuildMusic.queue.repeatQueue;
        GuildMusic.queue.repeatQueue = !repeat;
        // seed the queue if it doesn't exist and we are playing a manual song
        if (GuildMusic.queue.NowPlaying != null && GuildMusic.queue.NowPlaying.artist == null &&
            GuildMusic.queue.Queue.Count == 0) {
            GuildMusic.queue.Queue.Add(GuildMusic.queue.NowPlaying);
        }

        await respond(repeat ? "Disabled repeat." : "Enabled repeat.");
    }

    public async Task earrape() {
        bool earrape = GuildMusic.queue.earrapeMode;
        GuildMusic.queue.earrapeMode = !earrape;
        await respond(earrape ? "Disabled earrape mode." : "Enabled earrape mode.");
    }

    public async Task clear() {
        int rmd = GuildMusic.queue.EmptyQueue();
        GuildMusic.queue.clearQueue();

        await respond($"Removed {rmd:#,##0} tracks from the queue uwu");
    }

    public async Task pause() {
        await GuildMusic.PauseAsync();
        await respond($"Playback paused. Use {$"{c.prefix}resume".InlineCode()} to resume playback.");
    }

    public async Task resume() {
        await GuildMusic.ResumeAsync();
        await respond("Playback resumed.");
    }

    public async Task shuffle() {
        GuildMusic.queue.shuffle();
        await respond("Queue shuffled.");
    }

    public async Task skip(int num) {
        // don't allow skipping more at the same time
        try {
            await skipLock.WaitAsync();
            for (int i = 0; i < num; i++) {
                var track = GuildMusic.queue.NowPlaying?.track;
                await GuildMusic.queue.StopAsync();
                await respond($"{track.ToLimitedTrackString()} skipped.");
                if (i + 1 < num) {
                    await Task.Delay(500); // wait for the next one
                }
            }
        }
        finally {
            skipLock.Release();
        }
    }

    public async Task seek(TimeSpan position) {
        await GuildMusic.SeekAsync(position, false);
        await respond($"Seeking to {position.ToDurationString()}...");
    }

    public async Task forward(TimeSpan offset) {
        await GuildMusic.SeekAsync(offset, true);
        await respond($"Seeking forward by {offset.ToDurationString()}...");
    }

    public async Task rewind(TimeSpan offset) {
        await GuildMusic.SeekAsync(-offset, true);
        await respond($"Seeking backward by {offset.ToDurationString()}...");
    }

    public async Task setVolume(int volume) {
        if (volume is < 0 or > 1000) {
            await respond("Volume must be greater than 0, and less than or equal to 1000.");
            return;
        }

        await GuildMusic.SetVolumeAsync(volume);
        await respond($"Volume set to {GuildMusic.effectiveVolume}%.");
    }

    public async Task getVolume() {
        await respond($"Volume is {GuildMusic.volume} * {GuildMusic.artistVolume} = {GuildMusic.effectiveVolume}%.");
    }

    public async Task restart() {
        var track = GuildMusic.queue.NowPlaying?.track;
        await GuildMusic.queue.RestartAsync();
        await respond($"{track.ToLimitedTrackString()} restarted.");
    }

    public async Task remove(int index) {
        var itemN = GuildMusic.queue.Remove(index - 1);
        if (itemN == null) {
            await respond("No such track.");
            return;
        }

        await respond($"{itemN.ToLimitedTrackString()} removed.");
    }

    public async Task queue() {
        var track = GuildMusic.queue.NowPlaying;
        if (track is null && GuildMusic.queue.Queue.Count == 0 && GuildMusic.queue.autoQueue.Count == 0) {
            await respond("Queue is empty!");
            return;
        }

        var isPlaying = track != null;
        var queue = GuildMusic.queue.getCombinedQueue();
        var pageCount = queue.Count / 10 + 1;
        if (queue.Count % 10 == 0) {
            pageCount--;
        }
        if (!isPlaying && queue.Count == 0) {
            await respond("Queue is empty!");
            return;
        }
        var pages = queue.Select(x => x.track.ToTrackString())
            .Select((s, i) => (s, i))
            .GroupBy(x => x.i / 10)
            .Select(xg =>
                new Page(
                    $"Now playing: {(isPlaying ? $"{track?.track.ToLimitedTrackString()} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{track?.track.Info.Length.ToDurationString()}]" : "Nothing".Bold())}\n\n" +
                    $"{string.Join("\n", xg.Select(xa => $"`{xa.i + 1:00}` {xa.s}"))}\n\nPage {xg.Key + 1}/{pageCount}"))
            .ToList();

        // queue is empty but we are playing
        if (pages.Count == 0) {
            pages.Add(new Page(
                $"Now playing: {(isPlaying ? $"{track?.track.ToLimitedTrackString()} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{track?.track.Info.Length.ToDurationString()}]" : "Nothing".Bold())}"));
        }

        await c.paginate(pages);
    }

    public async Task nowPlaying() {
        var track = GuildMusic.queue.NowPlaying;
        if (track == default) {
            await respond("Not playing.");
        }
        else {
            await respond(
                $"Now playing: {track.track.ToLimitedTrackString()} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{track.track.Info.Length.ToDurationString()}].");
        }
    }

    public async Task playerInfo() {
        await respond($"Queue length: {GuildMusic.queue.getCombinedQueue().Count}\nVolume: {GuildMusic.volume}%");
    }
}
