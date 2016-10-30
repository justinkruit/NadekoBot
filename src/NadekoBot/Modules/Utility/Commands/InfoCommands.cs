using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Utility
{
    public partial class Utility
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task TogetherTube()
        {
            var channel = Context.Channel;

            Uri target;
            using (var http = new HttpClient())
            {
                var res = await http.GetAsync("https://togethertube.com/room/create").ConfigureAwait(false);
                target = res.RequestMessage.RequestUri;
            }

            await channel.SendMessageAsync($"{Context.User.Mention}, `Here is the link:` {target}")
                         .ConfigureAwait(false);
        }
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ServerInfo(string guildStr = null)
        {
            var channel = (SocketTextChannel)Context.Channel;

            guildStr = guildStr?.ToUpperInvariant();
            SocketGuild server;
            if (string.IsNullOrWhiteSpace(guildStr))
                server = channel.Guild;
            else
                server = NadekoBot.Client.Guilds.Where(g => g.Name.ToUpperInvariant() == guildStr.ToUpperInvariant()).FirstOrDefault();
            if (server == null)
                return;

            var sb = new StringBuilder();
                var users = server.Users;
                sb.AppendLine($@"`Name:` **{server.Name}**
`Owner:` **{server.GetUser(server.OwnerId)}**
`Id:` **{server.Id}**
`Icon Url:` **{ server.IconUrl}**
`TextChannels:` **{(await server.GetTextChannelsAsync()).Count()}** `VoiceChannels:` **{(await server.GetVoiceChannelsAsync()).Count()}**
`Members:` **{users.Count}** `-` {users.Count(u => u.Status == UserStatus.Online)}:green_heart: {users.Count(u => u.Status == UserStatus.Idle)}:yellow_heart: {users.Count(u => u.Status == UserStatus.DoNotDisturb)}:heart: {users.Count(u => u.Status == UserStatus.Offline || u.Status == UserStatus.Unknown)}:black_heart:
`Roles:` **{server.Roles.Count()}**
`Created At:` **{server.CreatedAt.ToString("dd.MM.yyyy HH:mm")}**
");
                if (server.Emojis.Count() > 0)
                    sb.AppendLine($"`Custom Emojis:` **{string.Join(", ", server.Emojis)}**");
                if (server.Features.Count() > 0)
                    sb.AppendLine($"`Features:` **{string.Join(", ", server.Features)}**");
                if (!string.IsNullOrWhiteSpace(server.SplashUrl))
                    sb.AppendLine($"`Region:` **{server.VoiceRegionId}**");
                await Context.Channel.SendMessageAsync(sb.ToString()).ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChannelInfo(ITextChannel channel = null)
            {
                var ch = channel ?? (ITextChannel)Context.Channel;

                var toReturn = $@"`Name:` **#{ch.Name}**
`Id:` **{ch.Id}**
`Created At:` **{ch.CreatedAt.ToString("dd.MM.yyyy HH:mm")}**
`Topic:` **{ch.Topic}**
`Users:` **{(await ch.GetUsersAsync().Flatten().ConfigureAwait(false)).Count()}**";
                await Context.Channel.SendMessageAsync(toReturn).ConfigureAwait(false);
            }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task UserInfo(IUserMessage msg, IGuildUser usr = null)
        {
            var channel = (ITextChannel)msg.Channel;
            var user = usr ?? msg.Author as IGuildUser;
            if (user == null)
                return;
            var toReturn = $"`Name#Discrim:` **#{user.Username}#{user.Discriminator}**\n";
            if (!string.IsNullOrWhiteSpace(user.Nickname))
                toReturn += $"`Nickname:` **{user.Nickname}**";
            toReturn += $@"`Id:` **{user.Id}**
`Current Game:` **{(!user.Game.HasValue ? "-" : user.Game?.Name)}**
`Joined Server:` **{user.JoinedAt?.ToString("dd.MM.yyyy HH:mm")}** 
`Joined Discord:` **{user.CreatedAt.ToString("dd.MM.yyyy HH:mm")}**
`Roles:` **({user.RoleIds.Count()}) - {string.Join(", ", user.GetRoles().Select(r => r.Name)).SanitizeMentions()}**
`AvatarUrl:` **{await NadekoBot.Google.ShortenUrl(user.AvatarUrl).ConfigureAwait(false)}**";
            await Context.Channel.SendMessageAsync(toReturn).ConfigureAwait(false);
        }
    }
}