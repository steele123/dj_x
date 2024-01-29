using System.Reflection;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.SlashCommands;
using Lavalink4NET;
using Lavalink4NET.Events.Players;
using Lavalink4NET.Players.Queued;

namespace Bot;

public class DiscordBot(
    ILogger<DiscordBot> logger,
    DiscordClient client,
    IAudioService audioService,
    IServiceProvider serviceProvider,
    IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var commands = client.UseSlashCommands(new SlashCommandsConfiguration
        {
            Services = serviceProvider
        });

        commands.RegisterCommands(Assembly.GetExecutingAssembly());

        client.ComponentInteractionCreated += ClientOnComponentInteractionCreated;

        audioService.WebSocketClosed += AudioServiceOnWebSocketClosed;

        await client.ConnectAsync();

        logger.LogInformation("Connected to Discord");
    }

    private async Task ClientOnComponentInteractionCreated(DiscordClient sender,
        ComponentInteractionCreateEventArgs args)
    {
        //var scope = serviceScopeFactory.CreateScope();
        var id = args.Id!;

        var player = await audioService.Players.GetPlayerAsync<EmbedDisplayPlayer>(args.Guild.Id);
        if (player is null)
        {
            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("No player found").AsEphemeral());
            return;
        }

        var member = args.Guild.Members.GetValueOrDefault(args.User.Id);
        if (member is null)
        {
            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("No member found").AsEphemeral());
            return;
        }

        var vc = member.VoiceState?.Channel;
        if (vc?.Id != player.VoiceChannelId)
        {
            await args.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("You are not in the same voice channel as the bot").AsEphemeral());
            return;
        }

        switch (id)
        {
            case "toggle_playback":
            {
                if (player.IsPaused)
                {
                    await player.ResumeAsync();
                }
                else
                {
                    await player.PauseAsync();
                }

                break;
            }
            case "skip":
            {
                await player.SkipAsync();
                break;
            }
            case "stop":
            {
                await player.StopAsync();
                break;
            }
            case "toggle_repeat":
            {
                player.RepeatMode = player.RepeatMode == TrackRepeatMode.None
                    ? TrackRepeatMode.Queue
                    : TrackRepeatMode.None;

                await player.TriggerMessageUpdate();

                break;
            }
        }

        await args.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
    }

    private async Task AudioServiceOnWebSocketClosed(object sender, WebSocketClosedEventArgs eventargs)
    {
        logger.LogInformation("Socket Closed - Code: {Code}, Reason: {Reason}", eventargs.CloseCode, eventargs.Reason);
    }
}