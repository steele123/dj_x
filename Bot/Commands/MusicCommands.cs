using System.Text;
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
            HistoryCapacity = 20,
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
                .WithDescription("DJ X is cookin that shit up for ya, gimme a sec")
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

        var isPlaylist = query.Contains("playlist");
        if (isPlaylist)
        {
            var tracks = await audioService.Tracks.LoadTracksAsync(query, searchMode);
            if (tracks.Count is 0)
            {
                await ctx.EditResponseAsync(
                    new DiscordWebhookBuilder().WithContent(
                        "No tracks found, try a different provider with the command options."));
                return;
            }

            var firstTrack = tracks.Tracks.Take(1).First();
            var queueItems = tracks.Tracks.Select(x => new TrackQueueItem(new TrackReference(x))).ToList();
            await player.Queue.AddRangeAsync(queueItems);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
                $"Added {tracks.Count + 1} tracks to the queue from playlist {tracks.Playlist!.Name}"));
            await player.PlayAsync(firstTrack, false);
            return;
        }

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

    [SlashCommand("nowplaying", "Shows the current song")]
    public async Task NowPlaying(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        var currentTrack = player.CurrentTrack;
        if (currentTrack is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No track playing."));
            return;
        }

        var embed = new DiscordEmbedBuilder()
            .WithTitle(currentTrack.Title)
            .WithDescription(currentTrack.Author)
            .WithUrl(currentTrack.Uri)
            .WithThumbnail(currentTrack.ArtworkUri)
            .WithBranding();

        await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
    }

    [SlashCommand("fill", "Fills the queue with a preset playlist")]
    public async Task Fill(InteractionContext ctx,
        [Option("playlist", "The playlist to fill the queue with")]
        PresetPlaylists playlist)
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
                .WithDescription("DJ X is cookin that shit up for ya, gimme a sec")
                .WithBranding();

            var msg = await ctx.Channel.SendMessageAsync(new DiscordMessageBuilder().WithContent("").AddEmbed(embed));

            if (msg is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Failed to create embed message."));
                return;
            }

            player.EmbedMessage = msg;
        }

        var playlistUrl = playlist switch
        {
            PresetPlaylists.Rap => "https://open.spotify.com/playlist/37i9dQZF1DX0XUsuxWHRQd?si=32f6cef96dbb49fa",
            PresetPlaylists.Indie => "https://open.spotify.com/playlist/37i9dQZF1DX2sUQwD7tbmL?si=ef0c982971c445ae",
            PresetPlaylists.Pop => "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M?si=15a3cbd2ca994b79",
            PresetPlaylists.Dance => "https://open.spotify.com/playlist/37i9dQZF1DX4dyzvuaRJ0n?si=2e77a916ab5446b2",
            PresetPlaylists.LoFi => "https://open.spotify.com/playlist/37i9dQZF1DWWQRwui0ExPn?si=1cf892cf35d440fc",
            _ => throw new ArgumentOutOfRangeException(nameof(playlist), playlist, null)
        };

        var tracks = await audioService.Tracks.LoadTracksAsync(playlistUrl, TrackSearchMode.Spotify);
        if (tracks.Count is 0)
        {
            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder().WithContent(
                    "No tracks found, try a different provider with the command options."));
            return;
        }

        var firstTrack = tracks.Tracks.Take(1).First();
        var queueItems = tracks.Tracks.Select(x => new TrackQueueItem(new TrackReference(x))).ToList();
        await player.Queue.AddRangeAsync(queueItems);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(
            $"Added {tracks.Count + 1} tracks to the queue from playlist {tracks.Playlist!.Name}"));
        await player.PlayAsync(firstTrack, false);
    }

    [SlashCommand("stop", "Stops the current song")]
    public async Task Stop(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        await player.StopAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Stopped"));
    }

    [SlashCommand("skip", "Skips the current song")]
    public async Task Skip(InteractionContext ctx,
        [Option("position", "The position to skip from (default: current song, 0)")]
        long position = 0,
        [Option("quantity", "The quantity of songs to skip (default: 1)")]
        long quantity = 1)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        if (position is 0)
        {
            await player.SkipAsync((int) quantity);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Skipped {quantity} songs"));
            return;
        }

        await player.Queue.RemoveRangeAsync((int) position, (int) quantity);
        await ctx.EditResponseAsync(
            new DiscordWebhookBuilder().WithContent($"Skipped {quantity} songs from position {position}"));
    }

    [SlashCommand("pause", "Pauses the current song")]
    public async Task Pause(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        await player.PauseAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Paused"));
    }

    [SlashCommand("resume", "Resumes the current song")]
    public async Task Resume(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        await player.ResumeAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Resumed"));
    }

    [SlashCommand("volume", "Sets the volume")]
    public async Task Volume(InteractionContext ctx,
        [Option("volume", "The volume to set")]
        double volume)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        await player.SetVolumeAsync((float) volume);
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"Volume set to {volume}"));
    }

    [SlashCommand("queue", "Shows the current queue")]
    public async Task Queue(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        var queue = player.Queue;
        var currentTrack = player.CurrentTrack;
        var builder = new DiscordWebhookBuilder();

        var embed = new DiscordEmbedBuilder()
            .WithColor(Constants.Color);

        var sb = new StringBuilder();
        if (currentTrack is not null) sb.AppendLine($"**Now Playing:** {currentTrack.Title} - {currentTrack.Author}\n");

        if (queue.Count is 0)
        {
            sb.AppendLine("No tracks in queue.");
        }
        else
        {
            var totalDuration = queue.Sum(x => x.Track?.Duration.TotalMilliseconds ?? 0);

            sb.AppendLine("**Queue:**");
            for (var i = 0; i < queue.Count; i++)
            {
                // After 20 tracks show a truncated message of the remaining tracks
                if (i is 20)
                {
                    sb.AppendLine($"... {queue.Count - i} more tracks");
                    break;
                }

                var queueItem = queue[i];
                var track = queueItem.Track;
                if (track is null) continue;

                sb.AppendLine($"{i}. [{track.Title}]({track.Uri}) - {track.Author} `[src: {track.SourceName}]`");
            }

            sb.AppendLine($"\n**Total Duration:** {TimeSpan.FromMilliseconds(totalDuration).ToString("mm\\:ss")}");
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

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
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
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
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
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
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
            sb.AppendLine("**Last 20 Songs** (most recent first):");
            foreach (var historyItem in history.Reverse())
            {
                var track = historyItem.Track;
                if (track is null) continue;

                sb.AppendLine($"- [{track.Title}]({track.Uri}) - {track.Author} `[via {track.SourceName}]`");
            }
        }

        embed.WithDescription(sb.ToString());
        embed.WithBranding();
        builder.AddEmbed(embed);

        await ctx.EditResponseAsync(builder);
    }
    
    [SlashCommand("bump", "Bumps the message to the top of the channel")]
    public async Task Bump(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
        }

        if (player?.EmbedMessage is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("No embed message found."));
            return;
        }

        // Delete the message then send it again
        await player.EmbedMessage.DeleteAsync();
        var msg = await ctx.Channel.SendMessageAsync(player.CreateMessage(player.CurrentTrack!));
        player.EmbedMessage = msg;
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Bumped the message."));
    }

    [SlashCommand("clear", "Clears the queue")]
    public async Task Clear(InteractionContext ctx)
    {
        await ctx.DeferAsync(true);

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
            return;
        }

        await player.Queue.ClearAsync();
        await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Cleared the queue"));
    }

    [SlashCommand("lyrics", "Shows the lyrics of the current song")]
    public async Task Lyrics(InteractionContext ctx)
    {
        await ctx.DeferAsync();

        var player = await audioService.Players.GetPlayerAsync<QueuedLavalinkPlayer>(ctx.Guild.Id);
        if (player is null)
        {
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("DJ X isn't playing anything."));
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

        // Remove the first line, it's the title
        lyrics = lyrics[(lyrics.IndexOf('\n') + 1)..];
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
            SoundProvider.Plain => TrackSearchMode.None,
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
    [ChoiceName("Plain")] Plain
}

public enum RepeatMode
{
    [ChoiceName("Off")] Off,
    [ChoiceName("Track")] Track,
    [ChoiceName("Queue")] Queue
}

public enum PresetPlaylists
{
    Rap,
    Indie,
    Pop,
    Dance,
    LoFi
}