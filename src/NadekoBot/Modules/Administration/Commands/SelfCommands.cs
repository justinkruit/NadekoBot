using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        class SelfCommands : ModuleBase
        {
            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task Leave([Remainder] SocketGuild guild)
            {
                var channel = (SocketTextChannel)Context.Channel;

                if (guild == null)
                {
                    await channel.SendMessageAsync("Cannot find that server").ConfigureAwait(false);
                    return;
                }
                if (guild.OwnerId != NadekoBot.Client.CurrentUser.Id)
                {
                    await guild.LeaveAsync().ConfigureAwait(false);
                    await channel.SendMessageAsync("Left server " + guild.Name).ConfigureAwait(false);
                }
                else
                {
                    await guild.DeleteAsync().ConfigureAwait(false);
                    await channel.SendMessageAsync("Deleted server " + guild.Name).ConfigureAwait(false);
                }
            }
        }
    }
}