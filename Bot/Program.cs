using Bot;
using DSharpPlus;
using Lavalink4NET.Extensions;
using Lavalink4NET.Lyrics.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddHostedService<DiscordBot>();
builder.Services.AddSingleton<DiscordClient>();
builder.Services.AddSingleton<DiscordConfiguration>(opts =>
{
    var token = builder.Configuration["Discord:Token"];
    var config = new DiscordConfiguration
    {
        Token = token,
        TokenType = TokenType.Bot,
        Intents = DiscordIntents.AllUnprivileged,
        MinimumLogLevel = LogLevel.Information,
        LogUnknownEvents = false
    };

    return config;
});

builder.Services.AddLavalink();
builder.Services.AddLyrics();
builder.Services.ConfigureLavalink(opts =>
{
    var secure = bool.Parse(builder.Configuration["Lavalink:Secure"] ?? "false");
    var host = builder.Configuration["Lavalink:Host"] ?? throw new InvalidOperationException("Lavalink host not set.");
    var port = int.Parse(builder.Configuration["Lavalink:Port"] ?? "2233");
    var password = builder.Configuration["Lavalink:Password"] ??
                   throw new InvalidOperationException("Lavalink password not set.");

    opts.BaseAddress = new Uri($"http{(secure ? "s" : "")}://{host}:{port}");
    opts.Passphrase = password;
    //opts.WebSocketUri = new Uri($"ws{(secure ? "s" : "")}://{host}:{port}");
});

var host = builder.Build();
host.Run();