using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using Lavalink4NET.Players;
using Lavalink4NET.Players.Queued;
using Lavalink4NET.Tracks;

namespace Bot;

/// <summary>
/// Plays songs and sends an embed to the specified text channel.
/// </summary>
public class EmbedDisplayPlayer(IPlayerProperties<EmbedDisplayPlayer, EmbedDisplayPlayerOptions> properties)
    : QueuedLavalinkPlayer(properties)
{
    public DiscordMessage? EmbedMessage { get; set; }

    protected override async ValueTask NotifyTrackStartedAsync(ITrackQueueItem queueItem,
        CancellationToken cancellationToken = new CancellationToken())
    {
        await base.NotifyTrackStartedAsync(queueItem, cancellationToken);

        if (EmbedMessage is null)
        {
            return;
        }

        await EmbedMessage.ModifyAsync(CreateMessage(queueItem.Track!, RepeatMode != TrackRepeatMode.None, IsPaused));
    }

    public override async ValueTask PauseAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        await base.PauseAsync(cancellationToken);

        if (EmbedMessage is null)
        {
            return;
        }

        await TriggerMessageUpdate();
    }

    public override async ValueTask ResumeAsync(CancellationToken cancellationToken = new CancellationToken())
    {
        await base.ResumeAsync(cancellationToken);

        if (EmbedMessage is null)
        {
            return;
        }

        await TriggerMessageUpdate();
    }

    /// <summary>
    /// We use this to trigger an update to the embed message.
    /// </summary>
    public async Task TriggerMessageUpdate()
    {
        await EmbedMessage!.ModifyAsync(CreateMessage(CurrentTrack!, RepeatMode != TrackRepeatMode.None, IsPaused));
    }

    public override async ValueTask StopAsync(CancellationToken cancellationToken = new())
    {
        await base.StopAsync(cancellationToken);

        if (EmbedMessage is null)
        {
            return;
        }

        try
        {
            var embed = new DiscordEmbedBuilder()
                .WithDescription(
                    "That's a wrap, you've reached the end of the queue.\n\n`/play` to add more songs.")
                .WithBranding();

            var msg = new DiscordMessageBuilder()
                .AddEmbed(embed);

            await EmbedMessage.ModifyAsync(msg);
        }
        catch (NotFoundException)
        {
            // Message was deleted..
            EmbedMessage = null;
            return;
        }

        // Delete message after 10 seconds
        var cloneMessage = EmbedMessage;
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        await cloneMessage.DeleteAsync();
    }

    public DiscordMessageBuilder CreateMessage(LavalinkTrack track, bool repeat, bool paused)
    {
        return new DiscordMessageBuilder()
            .WithEmbed(new DiscordEmbedBuilder()
                .WithTitle(track.Title)
                .WithUrl(track.Uri)
                .WithThumbnail(track.ArtworkUri)
                .AddField("Duration", track.Duration.ToString("mm\\:ss"), true)
                .AddField("Author", track.Author, true)
                .AddField("Paused", paused ? "Yes" : "No", true)
                .AddField("Repeat", repeat ? "Yes" : "No", true)
                .AddField("Source", track.SourceName, true)
                .AddField("Shuffle Mode", Shuffle ? "On" : "Off", true)
                .WithBranding()
            )
            .AddComponents(new DiscordComponent[]
            {
                new DiscordLinkButtonComponent(track.Uri?.ToString(), "Link"),
                new DiscordButtonComponent(ButtonStyle.Success, "toggle_playback", paused ? "Play" : "Pause"),
                new DiscordButtonComponent(ButtonStyle.Danger, "skip", "Skip"),
                new DiscordButtonComponent(ButtonStyle.Secondary, "toggle_repeat", repeat ? "Repeat Off" : "Repeat On"),
                new DiscordButtonComponent(ButtonStyle.Primary, "toggle_shuffle", Shuffle ? "Shuffle Off" : "Shuffle On")
            });
    }
}

public record EmbedDisplayPlayerOptions : QueuedLavalinkPlayerOptions
{
}