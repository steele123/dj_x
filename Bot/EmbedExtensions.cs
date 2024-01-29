using DSharpPlus.Entities;

namespace Bot;

public static class EmbedExtensions
{
    public static DiscordEmbedBuilder WithBranding(this DiscordEmbedBuilder builder)
    {
        return builder
            //.WithAuthor(Constants.BotName, url: Constants.ImageUrl)
            .WithFooter(Constants.FooterText, Constants.ImageUrl)
            .WithColor(Constants.Color);
    }
}