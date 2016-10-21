using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using System;
using System.Linq;
using System.Threading.Tasks;
using NadekoBot.Services;
using System.Text;
using NadekoBot.Extensions;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Reflection;
using Discord.WebSocket;

namespace NadekoBot.Modules.Utility
{
    [NadekoModule("Utility", ".")]
    public partial class Utility : DiscordModule
    {
        public Utility(): base()
        {

        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task WhosPlaying([Remainder] string game = null)
        {
            var channel = (SocketTextChannel)Context.Channel;
            game = game.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(game))
                return;
            var arr = channel.Guild.Users
                                   .Where(u => u.Game?.Name?.ToUpperInvariant() == game)
                                   .Select(u => u.Username)
                                   .ToList();

            int i = 0;
            if (!arr.Any())
                await channel.SendMessageAsync("`Nobody is playing that game.`").ConfigureAwait(false);
            else
                await channel.SendMessageAsync("```xl\n" + string.Join("\n", arr.GroupBy(item => (i++) / 3).Select(ig => string.Concat(ig.Select(el => $"• {el,-35}")))) + "\n```").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task InRole([Remainder] string roles)
        {
            if (string.IsNullOrWhiteSpace(roles))
                return;
            var channel = (SocketTextChannel)Context.Channel;
            var arg = roles.Split(',').Select(r => r.Trim().ToUpperInvariant());
            string send = "`Here is a list of users in a specfic role:`";
            foreach (var roleStr in arg.Where(str => !string.IsNullOrWhiteSpace(str) && str != "@EVERYONE" && str != "EVERYONE"))
            {
                var role = channel.Guild.Roles.Where(r => r.Name.ToUpperInvariant() == roleStr).FirstOrDefault();
                if (role == null) continue;
                send += $"\n`{role.Name}`\n";
                send += string.Join(", ", channel.Guild.Users.Where(u => u.GetRoles().Contains(role)).Select(u => u.ToString()));
            }
            var usr = Context.User as IGuildUser;
            while (send.Length > 2000)
            {
                if (!usr.GetPermissions(channel).ManageMessages)
                {
                    await channel.SendMessageAsync($"{usr.Mention} you are not allowed to use this command on roles with a lot of users in them to prevent abuse.").ConfigureAwait(false);
                    return;
                }
                var curstr = send.Substring(0, 2000);
                await channel.SendMessageAsync(curstr.Substring(0,
                        curstr.LastIndexOf(", ", StringComparison.Ordinal) + 1)).ConfigureAwait(false);
                send = curstr.Substring(curstr.LastIndexOf(", ", StringComparison.Ordinal) + 1) +
                       send.Substring(2000);
            }
            await channel.SendMessageAsync(send).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task CheckMyPerms()
        {

            StringBuilder builder = new StringBuilder("```\n");
            var user = Context.User as IGuildUser;
            var perms = user.GetPermissions((ITextChannel)Context.Channel);
            foreach (var p in perms.GetType().GetProperties().Where(p => !p.GetGetMethod().GetParameters().Any()))
            {
                builder.AppendLine($"{p.Name} : {p.GetValue(perms, null).ToString()}");
            }

            builder.Append("```");
            await Context.Channel.SendMessageAsync(builder.ToString()).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task UserId(IGuildUser target = null)
        {
            var usr = target ?? Context.User;
            await Context.Channel.SendMessageAsync($"Id of the user { usr.Username } is { usr.Id }").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task ChannelId()
        {
            await Context.Channel.SendMessageAsync($"This Channel's ID is {Context.Channel.Id}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ServerId()
        {
            await Context.Channel.SendMessageAsync($"This server's ID is {Context.Guild.Id}").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Roles(IGuildUser target, int page = 1)
        {
            var channel = (SocketGuildChannel)Context.Channel;
            var guild = channel.Guild;

            const int RolesPerPage = 20;

            if (page < 1 || page > 100)
                return;
            if (target != null)
            {
                await Context.Channel.SendMessageAsync($"`Page #{page} of roles for **{target.Username}**:` \n• " + string.Join("\n• ", target.GetRoles().Except(new[] { guild.EveryoneRole }).OrderBy(r => r.Position).Skip((page - 1) * RolesPerPage).Take(RolesPerPage)).SanitizeMentions());
            }
            else
            {
                await Context.Channel.SendMessageAsync($"`Page #{page} of all roles on this server:` \n• " + string.Join("\n• ", guild.Roles.Except(new[] { guild.EveryoneRole }).OrderBy(r => r.Position).Skip((page - 1) * RolesPerPage).Take(RolesPerPage)).SanitizeMentions());
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public Task Roles(int page = 1) =>
            Roles(null, page);

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ChannelTopic()
        {
            var channel = (SocketTextChannel)Context.Channel;

            var topic = channel.Topic;
            if (string.IsNullOrWhiteSpace(topic))
                await channel.SendMessageAsync("`No topic set.`").ConfigureAwait(false);
            else
                await channel.SendMessageAsync("`Topic:` " + topic).ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        public async Task Stats()
        {
            await Context.Channel.SendMessageAsync(await NadekoBot.Stats.Print()).ConfigureAwait(false);
        }

        private Regex emojiFinder { get; } = new Regex(@"<:(?<name>.+?):(?<id>\d*)>", RegexOptions.Compiled);
        [NadekoCommand, Usage, Description, Aliases]
        public async Task Showemojis([Remainder] string emojis)
        {
            var matches = emojiFinder.Matches(emojis);



            var result = string.Join("\n", matches.Cast<Match>()
                                                  .Select(m => $"`Name:` {m.Groups["name"]} `Link:` http://discordapp.com/api/emojis/{m.Groups["id"]}.png"));
            if (!string.IsNullOrWhiteSpace(result))
                await Context.Channel.SendMessageAsync(result).ConfigureAwait(false);
            else
                await Context.Channel.SendMessageAsync(Format.Code("No special Emojis found")).ConfigureAwait(false);
        }
    }
}

