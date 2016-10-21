using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using NadekoBot.Services;
using NadekoBot.Services.Database;
using NadekoBot.Services.Database.Models;
using NLog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        public class AutoAssignRoleCommands : ModuleBase
        {
            private static Logger _log { get; }

            static AutoAssignRoleCommands()
            {
                var _client = NadekoBot.Client;
                _log = LogManager.GetCurrentClassLogger();
                _client.UserJoined += (user) =>
                {
                    var t = Task.Run(async () =>
                    {
                        GuildConfig conf;
                        using (var uow = DbHandler.UnitOfWork())
                        {
                            conf = uow.GuildConfigs.For(user.Guild.Id);
                        }

                        if (conf.AutoAssignRoleId == 0)
                            return;

                        var role = user.Guild.Roles.FirstOrDefault(r => r.Id == conf.AutoAssignRoleId);

                        if (role != null)
                            try { await user.AddRolesAsync(role); } catch (Exception ex) { _log.Warn(ex); }
                    });
                    return Task.CompletedTask;
                };
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageRoles)]
            public async Task AutoAssignRole([Remainder] IRole role = null)
            {
                var channel = (SocketTextChannel)Context.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    if (role == null)
                        conf.AutoAssignRoleId = 0;
                    else
                        conf.AutoAssignRoleId = role.Id;

                    uow.GuildConfigs.Update(conf);
                    await uow.CompleteAsync().ConfigureAwait(false);
                }

                if (role == null)
                {
                    await channel.SendMessageAsync("`Auto assign role on user join is now disabled.`").ConfigureAwait(false);
                    return;
                }

                await channel.SendMessageAsync("`Auto assigned role is set.`").ConfigureAwait(false);
            }
        }
    }
}
