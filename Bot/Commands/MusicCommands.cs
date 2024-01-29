using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Lavalink4NET.Lyrics;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Rest.Entities.Tracks;

namespace Bot.Commands;

public class MusicCommands(IAudioService audioService, ILogger<MusicCommands> logger, ILyricsService lyricsService)
    : ApplicationCommandModule
{
    [SlashCommand("status", "Checks whether the bot is online and able to play music")]
    public async Task Status(InteractionContext ctx)
    {
        var player = await audioService.Players.GetPlayerAsync(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.CreateResponseAsync("bot: ✅\nlavalink: ❌");

            return;
        }

        await ctx.CreateResponseAsync("bot: ✅\nlavalink: ✅");
    }

    [SlashCommand("play", "Plays a song")]
    public async Task Play(InteractionContext ctx,
        [Option("query", "The song to play")] string query,
        [Option("provider", "The website to provide the sound (default: Spotify)")]
        SoundProvider provider = SoundProvider.Spotify,
        [Option("bump", "Whether to bump the song to the top of the queue")]
        bool bump = false)
    {
        await ctx.DeferAsync(true);

        var opts = new QueuedLavalinkPlayerOptions
        {
            SelfDeaf = true,
            HistoryCapacity = 30,
        };

        var vc = ctx.Member?.VoiceState?.Channel;
        if (vc is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You must be in a voice channel."));
            return;
        }

        var player = await audioService.Players.JoinAsync(ctx.Guild.Id, vc.Id, PlayerFactory.Queued, opts);
        var searchMode = GetTrackSearchMode(provider);
        var track = await audioService.Tracks.LoadTrackAsync(query, searchMode);

        if (track is null)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent("No tracks found, try a different provider with the command options."));
            return;
        }

        var pos = await player.PlayAsync(track);

        if (pos is 0)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Playing {track.Title}"));
        }

        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Added {track.Title} to the queue"));
    }

    [SlashCommand("stop", "Stops the current song")]
    public async Task Stop(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        await player.StopAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Stopped"));
    }

    [SlashCommand("skip", "Skips the current song")]
    public async Task Skip(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        await player.SkipAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Skipped"));
    }

    [SlashCommand("pause", "Pauses the current song")]
    public async Task Pause(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        await player.PauseAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Paused"));
    }

    [SlashCommand("resume", "Resumes the current song")]
    public async Task Resume(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        await player.ResumeAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Resumed"));
    }

    [SlashCommand("volume", "Sets the volume")]
    public async Task Volume(InteractionContext ctx,
        [Option("volume", "The volume to set")]
        long volume)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        await player.SetVolumeAsync(volume);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Volume set to {volume}"));
    }

    [SlashCommand("queue", "Shows the current queue")]
    public async Task Queue(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        var queue = player.Queue;
        var currentTrack = player.CurrentTrack;
        var builder = new DiscordWebhookBuilder();

        var embed = new DiscordEmbedBuilder()
            .WithColor(Constants.Color);
        
        var sb = new StringBuilder();
        if (currentTrack is not null)
        {
            sb.AppendLine($"**Now Playing:** {currentTrack.Title} - {currentTrack.Author}\n");
        }

        if (queue.Count is 0)
        {
            sb.AppendLine("No tracks in queue.");
        }
        else
        {
            sb.AppendLine("**Queue:**");
            foreach (var queueItem in queue)
            {
                var track = queueItem.Track;
                if (track is null)
                {
                    continue;
                }
                
                sb.AppendLine($"- {track.Title} - {track.Author} `[via {track.SourceName}]`");
            }
        }

        embed.WithDescription(sb.ToString());
        embed.AddBotMeta();
        builder.AddEmbed(embed);

        await ctx.EditResponseAsync(builder);
    }

    [SlashCommand("lyrics", "Shows the lyrics of the current song")]
    public async Task Lyrics(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        var currentTrack = player.CurrentTrack;
        if (currentTrack is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No track playing."));
            return;
        }

        var lyrics = await lyricsService.GetLyricsAsync(currentTrack.Author, currentTrack.Title);
        if (lyrics is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No lyrics found."));
            return;
        }

        var builder = new DiscordWebhookBuilder()
            .AddEmbed(new DiscordEmbedBuilder()
                .WithTitle(currentTrack.Title)
                .WithDescription(lyrics)
                .AddBotMeta());

        await ctx.EditResponseAsync(builder);
    }

    private static TrackSearchMode GetTrackSearchMode(SoundProvider provider)
    {
        return provider switch
        {
            SoundProvider.YouTube => TrackSearchMode.YouTube,
            SoundProvider.YouTubeMusic => TrackSearchMode.YouTubeMusic,
            SoundProvider.SoundCloud => TrackSearchMode.SoundCloud,
            SoundProvider.Spotify => TrackSearchMode.Spotify,
            SoundProvider.AppleMusic => TrackSearchMode.AppleMusic,
            SoundProvider.Deezer => TrackSearchMode.Deezer,
            SoundProvider.YandexMusic => TrackSearchMode.YandexMusic,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
    }
}

public enum SoundProvider
{
    [ChoiceName("YouTube Music")] YouTubeMusic,
    [ChoiceName("Apple Music")] AppleMusic,
    [ChoiceName("SoundCloud")] SoundCloud,
    [ChoiceName("Deezer")] Deezer,
    [ChoiceName("YouTube")] YouTube,
    [ChoiceName("Spotify")] Spotify,
    [ChoiceName("Yandex Music")] YandexMusic,
}