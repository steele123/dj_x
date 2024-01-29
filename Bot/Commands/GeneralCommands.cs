using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;

namespace Bot.Commands;

public class GeneralCommands : ApplicationCommandModule
{
    [SlashCommand("invite", "Get the invite link for the bot")]
    public async Task Invite(InteractionContext ctx)
    {
        await ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
            new DiscordInteractionResponseBuilder()
                .WithContent(
                    "Invite Link: \nhttps://discord.com/oauth2/authorize?client_id=1156325302859997225&scope=bot&permissions=397556132976"));
    }
}