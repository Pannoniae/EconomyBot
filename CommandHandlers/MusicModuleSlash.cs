﻿using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using DSharpPlus.Interactivity.Enums;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;

namespace EconomyBot;

[SlashModuleLifespan(SlashModuleLifespan.Singleton)]
public class MusicModuleSlash : ApplicationCommandModule {

    private MusicService Music { get; }
    private YouTubeSearchProvider YouTube { get; }

    public GuildMusicData GuildMusic { get; set; }

    public MusicCommon common;

    public MusicModuleSlash(YouTubeSearchProvider yt) {
        Music = Program.musicService;
        YouTube = yt;
    }
    
    private static DiscordChannel getChannel(InteractionContext ctx) {
        return ctx.Member!.VoiceState.Channel;
    }

    public override async Task<bool> BeforeSlashExecutionAsync(InteractionContext ctx) {
        if (ctx.CommandName == "join") {
            GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
            GuildMusic.CommandChannel = ctx.Channel;
            return false;
        }

        var chn = getChannel(ctx);
        if (chn == null) {
            await ctx.CreateResponseAsync(
                $"{Program.cube} You need to be in a voice channel.");
            throw new IdiotException("user error");
        }

        var mbr = ctx.Guild.CurrentMember?.VoiceState?.Channel;
        if (mbr != null && chn != mbr) {
            await ctx.CreateResponseAsync(
                $"{Program.cube} You need to be in the same voice channel.");
            throw new IdiotException("user error");
        }

        GuildMusic = await Music.GetOrCreateDataAsync(ctx.Guild);
        GuildMusic.CommandChannel = ctx.Channel;
        
        return true;
    }

    [SlashCommand("join", "Joins the voice channel.")]
    public async Task JoinAsync(InteractionContext ctx) {
        // yeet the bot in 
        var chn = getChannel(ctx);
        await GuildMusic.CreatePlayerAsync(chn);
        await ctx.CreateResponseAsync($"{Program.cube} Joined the channel.");
    }

    [SlashCommand("jazz", "Plays some jazz. :3")]
    public async Task PlayJazzAsync(InteractionContext ctx) {
        // yeet the bot in
        GuildMusic.queue.addToQueue("_fats");
        await GuildMusic.queue.seedQueue();
        var vs = ctx.Member.VoiceState;
        var chn = vs.Channel;
        await GuildMusic.CreatePlayerAsync(chn);
        await GuildMusic.queue.PlayAsync();
        await ctx.CreateResponseAsync($"{Program.cube} Started playing jazz.");
    }

    [SlashCommand("stopjazz", "Stops jazz.")]
    public async Task StopJazzAsync(InteractionContext ctx) {
        GuildMusic.queue.clearQueue();
        GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        await ctx.CreateResponseAsync($"{Program.cube} Stopped jazz.");
    }

    [SlashCommand("stop", "Stops playback and quits the voice channel.")]
    public async Task StopAsync(InteractionContext ctx) {
        int rmd = GuildMusic.queue.EmptyQueue();
        await GuildMusic.queue.StopAsync();
        await GuildMusic.DestroyPlayerAsync();

        await ctx.CreateResponseAsync(
            $"{Program.cube} Removed {rmd:#,##0} tracks from the queue.");
    }

    [SlashCommand("clear", "Clears the queue.")]
    public async Task ClearAsync(InteractionContext ctx) {
        int rmd = GuildMusic.queue.EmptyQueue();

        await ctx.CreateResponseAsync(
            $"{Program.cube} Removed {rmd:#,##0} tracks from the queue uwu");
    }

    [SlashCommand("pause", "Pauses playback.")]
    public async Task PauseAsync(InteractionContext ctx) {
        await GuildMusic.PauseAsync();
        await ctx.CreateResponseAsync(
            $"{Program.cube} Playback paused. Use {Formatter.InlineCode($"/resume")} to resume playback.");
    }

    [SlashCommand("resume", "Resumes playback.")]
    public async Task ResumeAsync(InteractionContext ctx) {
        await GuildMusic.ResumeAsync();
        await ctx.CreateResponseAsync($"{Program.cube} Playback resumed.");
    }

    [SlashCommand("skip", "Skips current track.")]
    public async Task SkipAsync(InteractionContext ctx) {
        var track = GuildMusic.queue.NowPlaying.track;
        await GuildMusic.queue.StopAsync();
        await ctx.CreateResponseAsync(
            $"{Program.cube} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} skipped.");
    }

    [SlashCommand("skipnum", "Skips current track.")]
    public async Task SkipAsync(InteractionContext ctx, [Option("tracks", "How many tracks to skip.")] long num) {
        for (int i = 0; i < num; i++) {
            var track = GuildMusic.queue.NowPlaying.track;
            await GuildMusic.queue.StopAsync();
            await ctx.CreateResponseAsync(
                $"{Program.cube} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} skipped.");
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
                $"{Program.cube} Volume must be greater than 0, and less than or equal to 1000.");
            return;
        }

        await GuildMusic.SetVolumeAsync((int)volume);
        await ctx.CreateResponseAsync($"{Program.cube} Volume set to {volume}%.");
    }

    [SlashCommand("volume", "Gets playback volume.")]
    public async Task GetVolumeAsync(InteractionContext ctx) {
        await ctx.CreateResponseAsync($"{Program.cube} Volume is {GuildMusic.volume}%.");
    }

    [SlashCommand("restart", "Restarts the playback of the current track.")]
    public async Task RestartAsync(InteractionContext ctx) {
        var track = GuildMusic.queue.NowPlaying.track;
        await GuildMusic.queue.RestartAsync();
        await ctx.CreateResponseAsync(
            $"{Program.cube} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} restarted.");
    }

    [SlashCommand("remove", "Removes a track from playback queue.")]
    public async Task RemoveAsync(InteractionContext ctx,
        [Option("remove", "Which track to remove.")]
        long index) {
        var itemN = GuildMusic.queue.Remove((int)(index - 1));
        if (itemN == null) {
            await ctx.CreateResponseAsync($"{Program.cube} No such track.");
            return;
        }

        var track = itemN;
        await ctx.CreateResponseAsync(
            $"{Program.cube} {Formatter.Bold(Formatter.Sanitize(track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.Author))} removed.");
    }

    [SlashCommand("queue", "Displays current playback queue.")]
    public async Task QueueAsync(InteractionContext ctx) {
        var interactivity = ctx.Client.GetInteractivity();

        var pageCount = GuildMusic.queue.Queue.Count / 10 + 1;
        if (GuildMusic.queue.Queue.Count % 10 == 0) pageCount--;
        var pages = GuildMusic.queue.Queue.Select(x => x.track.ToTrackString())
            .Select((s, i) => new { str = s, index = i })
            .GroupBy(x => x.index / 10)
            .Select(xg =>
                new Page(
                    $"Now playing: {GuildMusic.queue.NowPlaying?.track.ToTrackString()}\n\n{string.Join("\n", xg.Select(xa => $"`{xa.index + 1:00}` {xa.str}"))}\n\nPage {xg.Key + 1}/{pageCount}"))
            .ToArray();

        var trk = GuildMusic.queue.NowPlaying;
        if (!pages.Any()) {
            if (trk?.track.TrackString == null)
                await ctx.CreateResponseAsync("Queue is empty!");
            else
                await ctx.CreateResponseAsync($"Now playing: {GuildMusic.queue.NowPlaying?.track.ToTrackString()}");

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
        var track = GuildMusic.queue.NowPlaying;
        if (GuildMusic.queue.NowPlaying?.track.TrackString == null) {
            await ctx.CreateResponseAsync("Not playing.");
        }
        else {
            await ctx.CreateResponseAsync(
                $"Now playing: {Formatter.Bold(Formatter.Sanitize(track.track.Title))} by {Formatter.Bold(Formatter.Sanitize(track.track.Author))} [{GuildMusic.GetCurrentPosition().ToDurationString()}/{GuildMusic.queue.NowPlaying.track.Length.ToDurationString()}].");
        }
    }

    [SlashCommand("playerinfo", "Displays information about current player.")]
    public async Task PlayerInfoAsync(InteractionContext ctx) {
        await ctx.CreateResponseAsync(
            $"Queue length: {GuildMusic.queue.Queue.Count}\nVolume: {GuildMusic.volume}%");
    }
}