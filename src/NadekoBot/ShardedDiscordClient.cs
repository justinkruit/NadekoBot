using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.API;
using Discord.Logging;
using System.IO;
using NLog;
using NadekoBot.Extensions;

namespace NadekoBot
{
    public class ShardedDiscordClient 
    {
        private DiscordSocketConfig discordSocketConfig;
        private Logger _log { get; }

        public event Func<SocketGuildUser, Task> UserJoined =  delegate { return Task.CompletedTask; };
        public event Func<SocketMessage, Task> MessageReceived = delegate { return Task.CompletedTask; };
        public event Func<SocketGuildUser, Task> UserLeft = delegate { return Task.CompletedTask; };
        public event Func<SocketUser, SocketUser, Task> UserUpdated = delegate { return Task.CompletedTask; };
        public event Func<Optional<SocketMessage>, SocketMessage, Task> MessageUpdated = delegate { return Task.CompletedTask; };
        public event Func<ulong, Optional<SocketMessage>, Task> MessageDeleted = delegate { return Task.CompletedTask; };
        public event Func<SocketUser, SocketGuild, Task> UserBanned = delegate { return Task.CompletedTask; };
        public event Func<SocketGuildUser, SocketGuild, Task> UserUnbanned = delegate { return Task.CompletedTask; };
        public event Func<Optional<SocketGuild>, SocketUser, SocketPresence, SocketPresence, Task> UserPresenceUpdated = delegate { return Task.CompletedTask; };
        public event Func<SocketUser, SocketVoiceState, SocketVoiceState, Task> UserVoiceStateUpdated = delegate { return Task.CompletedTask; };
        public event Func<SocketChannel, Task> ChannelCreated = delegate { return Task.CompletedTask; };
        public event Func<SocketChannel, Task> ChannelDestroyed = delegate { return Task.CompletedTask; };
        public event Func<SocketChannel, SocketChannel, Task> ChannelUpdated = delegate { return Task.CompletedTask; };
        public event Func<Exception, Task> Disconnected = delegate { return Task.CompletedTask; };

        private IReadOnlyList<DiscordSocketClient> Clients { get; }
        public IDiscordClient Zero => Clients[0];

        public ShardedDiscordClient (DiscordSocketConfig discordSocketConfig)
        {
            _log = LogManager.GetCurrentClassLogger();
            this.discordSocketConfig = discordSocketConfig;

            var clientList = new List<DiscordSocketClient>();
            for (int i = 0; i < discordSocketConfig.TotalShards; i++)
            {
                discordSocketConfig.ShardId = i;
                var client = new DiscordSocketClient(discordSocketConfig);
                clientList.Add(client);
                client.UserJoined += async arg1 => await UserJoined(arg1);
                client.MessageReceived += async arg1 => await MessageReceived(arg1);
                client.UserLeft += async arg1 => await UserLeft(arg1);
                client.UserUpdated += async (arg1, gu2) => await UserUpdated(arg1, gu2);
                client.MessageUpdated += async (arg1, m2) => await MessageUpdated(arg1, m2);
                client.MessageDeleted += async (arg1, arg2) => await MessageDeleted(arg1, arg2);
                client.UserBanned += async (arg1, arg2) => await UserBanned(arg1, arg2);
                client.UserPresenceUpdated += async (arg1, arg2, arg3, arg4) => await UserPresenceUpdated(arg1, arg2, arg3, arg4);
                client.UserVoiceStateUpdated += async (arg1, arg2, arg3) => await UserVoiceStateUpdated(arg1, arg2, arg3);
                client.ChannelCreated += async arg => await ChannelCreated(arg);
                client.ChannelDestroyed += async arg => await ChannelDestroyed(arg);
                client.ChannelUpdated += async (arg1, arg2) => await ChannelUpdated(arg1, arg2);

                _log.Info($"Shard #{i} initialized.");
            }

            Clients = clientList.AsReadOnly();
        }

        public SocketSelfUser CurrentUser => Clients[0].CurrentUser;

        public IReadOnlyCollection<SocketSelfUser> AllCurrentUsers => Clients.Select(c => c.CurrentUser).ToArray();

        public IReadOnlyCollection<SocketGuild> Guilds => Clients.SelectMany(c => c.Guilds).ToArray();

        public SocketGuild GetGuild(ulong id) =>
            Clients.Select(c => c.GetGuild(id)).FirstOrDefault(g => g != null);

        public Task<IDMChannel> GetDMChannelAsync(ulong channelId) =>
            Clients[0].GetDMChannelAsync(channelId);

        public Task LoginAsync(TokenType tokenType, string token) =>
            Task.WhenAll(Clients.Select(async c => { await c.LoginAsync(tokenType, token); _log.Info($"Shard #{c.ShardId} logged in."); }));

        public Task ConnectAsync() =>
            Task.WhenAll(Clients.Select(async c => { await c.ConnectAsync(); _log.Info($"Shard #{c.ShardId} connected."); }));

        public Task DownloadAllUsersAsync() =>
            Task.WhenAll(Clients.Select(async c => { await c.DownloadAllUsersAsync(); _log.Info($"Shard #{c.ShardId} downloaded {c.Guilds.Sum(g => g.Users.Count)} users."); }));

        public Task SetGameAsync(string game) => 
            Task.WhenAll(Clients.Select(c => c.SetGame(game)));

        public Task SetStreamAsync(string name, string url) => 
            Task.WhenAll(Clients.Select(c => c.SetGame(name, url, StreamType.Twitch)));
    }
}
