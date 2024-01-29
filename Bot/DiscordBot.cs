using System.Reflection;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Lavalink4NET;
using Lavalink4NET.Events.Players;

namespace Bot;

public class DiscordBot(
    ILogger<DiscordBot> logger,
    DiscordClient client,
    IAudioService audioService,
    IServiceProvider serviceProvider)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var commands = client.UseSlashCommands(new SlashCommandsConfiguration
        {
            Services = serviceProvider
        });

        commands.RegisterCommands(Assembly.GetExecutingAssembly());
        
        audioService.WebSocketClosed += AudioServiceOnWebSocketClosed;
        
        await client.ConnectAsync();

        logger.LogInformation("Connected to Discord");
    }

    private async Task AudioServiceOnWebSocketClosed(object sender, WebSocketClosedEventArgs eventargs)
    {
        logger.LogInformation("Socket Closed - Code: {Code}, Reason: {Reason}", eventargs.CloseCode, eventargs.Reason);
    }
}