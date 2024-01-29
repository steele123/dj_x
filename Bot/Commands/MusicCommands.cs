using System.Text;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Lavalink4NET;
using Lavalink4NET.Extensions;
using Lavalink4NET.Filters;
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
        await ctx.DeferAsync();

        var embed = new DiscordEmbedBuilder()
            .WithBranding().WithDescription("DJ X is online and ready to play some music!");
        
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
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

        var opts = new EmbedDisplayPlayerOptions
        {
            SelfDeaf = true,
            HistoryCapacity = 30,
            DisconnectOnDestroy = true,
            ClearQueueOnStop = true
        };

        var vc = ctx.Member?.VoiceState?.Channel;
        if (vc is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You must be in a voice channel."));
            return;
        }

        var player = await audioService.Players.JoinAsync<EmbedDisplayPlayer, EmbedDisplayPlayerOptions>(ctx.Guild.Id,
            vc.Id,
            CreatePlayerAsync, opts);

        if (player.State != PlayerState.Playing)
        {
            var embed = new DiscordEmbedBuilder()
                .WithDescription("DJ X cookin this one up for you, gimme a sec...")
                .WithBranding();

            var msg = await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent("").AddEmbed(embed));

            if (msg is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Failed to create embed message."));
                return;
            }

            player.EmbedMessage = msg;
        }

        var searchMode = GetTrackSearchMode(provider);
        var track = await audioService.Tracks.LoadTrackAsync(query, searchMode);

        if (track is null)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent(
                    "No tracks found, try a different provider with the command options."));
            return;
        }

        var pos = await player.PlayAsync(track);

        if (pos is 0)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Playing {track.Title}"));
            return;
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
        embed.WithBranding();
        builder.AddEmbed(embed);

        await ctx.EditResponseAsync(builder);
    }

    [SlashCommand("repeat", "Sets the repeat mode")]
    public async Task Repeat(InteractionContext ctx,
        [Option("mode", "The repeat mode to set")]
        RepeatMode mode)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        var newMode = mode switch
        {
            RepeatMode.Off => TrackRepeatMode.None,
            RepeatMode.Track => TrackRepeatMode.Track,
            RepeatMode.Queue => TrackRepeatMode.Queue,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        player.RepeatMode = newMode;
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Repeat mode set to {mode}"));
    }

    [SlashCommand("shuffle", "Enables or disables shuffle mode")]
    public async Task Shuffle(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        player.Shuffle = !player.Shuffle;
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Shuffle set to " + player.Shuffle));
    }

    [SlashCommand("history", "Shows the history of the current queue")]
    public async Task History(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No player found."));
            return;
        }

        var history = player.Queue.History;
        var builder = new DiscordWebhookBuilder();

        var embed = new DiscordEmbedBuilder()
            .WithColor(Constants.Color);

        var sb = new StringBuilder();
        if (history?.Count is 0 || history is null)
        {
            sb.AppendLine("No tracks in history.");
        }
        else
        {
            sb.AppendLine("**History:**");
            foreach (var historyItem in history)
            {
                var track = historyItem.Track;
                if (track is null)
                {
                    continue;
                }

                sb.AppendLine($"- {track.Title} - {track.Author} `[via {track.SourceName}]`");
            }
        }

        embed.WithDescription(sb.ToString());
        embed.WithBranding();
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
                .WithBranding());

        await ctx.EditResponseAsync(builder);
    }

    private static ValueTask<EmbedDisplayPlayer> CreatePlayerAsync(
        IPlayerProperties<EmbedDisplayPlayer, EmbedDisplayPlayerOptions> properties,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(properties);

        return ValueTask.FromResult(new EmbedDisplayPlayer(properties));
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

public enum RepeatMode
{
    [ChoiceName("Off")] Off,
    [ChoiceName("Track")] Track,
    [ChoiceName("Queue")] Queue
}