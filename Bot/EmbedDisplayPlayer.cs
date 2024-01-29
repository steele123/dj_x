using DSharpPlus.Entities;
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

        await EmbedMessage.ModifyAsync(embed: new DiscordEmbedBuilder()
            .WithTitle(queueItem.Track!.Title)
            .WithDescription(queueItem.Track.Author)
            .WithThumbnail(queueItem.Track.ArtworkUri)
            .WithUrl(queueItem.Track.Uri)
            .WithBranding());
    }

    public override async ValueTask StopAsync(CancellationToken cancellationToken = new())
    {
        await base.StopAsync(cancellationToken);

        if (EmbedMessage is null)
        {
            return;
        }

        await EmbedMessage.ModifyAsync(embed: new DiscordEmbedBuilder()
            .WithDescription("That's a wrap, you've reached the end of the queue bro.")
            .WithBranding());
        
        // Delete message after 10 seconds
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        await EmbedMessage.DeleteAsync();
    }
}

public record EmbedDisplayPlayerOptions : QueuedLavalinkPlayerOptions
{
}