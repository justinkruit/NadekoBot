﻿using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using System.Diagnostics;

namespace NadekoBot
{
    public class ShardedDiscordClient
    {
        private DiscordSocketConfig discordSocketConfig;
        private Logger _log { get; }

        public event Action<SocketGuildUser> UserJoined = delegate { };
        public event Action<SocketMessage> MessageReceived = delegate { };
        public event Action<SocketGuildUser> UserLeft = delegate { };
        public event Action<SocketUser, SocketUser> UserUpdated = delegate { };
        public event Action<Optional<SocketMessage>, SocketMessage> MessageUpdated = delegate { };
        public event Action<ulong, Optional<SocketMessage>> MessageDeleted = delegate { };
        public event Action<SocketUser, SocketGuild> UserBanned = delegate { };
        public event Action<SocketUser, SocketGuild> UserUnbanned = delegate { };
        public event Action<Optional<SocketGuild>, SocketUser, SocketPresence, SocketPresence> UserPresenceUpdated = delegate { };
        public event Action<SocketUser, SocketVoiceState, SocketVoiceState> UserVoiceStateUpdated = delegate { };
        public event Action<SocketChannel> ChannelCreated = delegate { };
        public event Action<SocketChannel> ChannelDestroyed = delegate { };
        public event Action<SocketChannel, SocketChannel> ChannelUpdated = delegate { };
        public event Action<Exception> Disconnected = delegate { };

        private uint _connectedCount = 0;
        private uint _downloadedCount = 0;

        private IReadOnlyList<DiscordSocketClient> Clients { get; }

        public ShardedDiscordClient(DiscordSocketConfig discordSocketConfig)
        {
            _log = LogManager.GetCurrentClassLogger();
            this.discordSocketConfig = discordSocketConfig;

            var clientList = new List<DiscordSocketClient>();
            for (int i = 0; i < discordSocketConfig.TotalShards; i++)
            {
                discordSocketConfig.ShardId = i;
                var client = new DiscordSocketClient(discordSocketConfig);
                clientList.Add(client);
                client.UserJoined += arg1 => { UserJoined(arg1); return Task.CompletedTask; };
                client.MessageReceived += arg1 =>
                {
                    if (arg1.Author == null || arg1.Author.IsBot)
                        return Task.CompletedTask; MessageReceived(arg1);
                    return Task.CompletedTask;
                };
                client.UserLeft += arg1 => { UserLeft(arg1); return Task.CompletedTask; };
                client.UserUpdated += (arg1, gu2) => { UserUpdated(arg1, gu2); return Task.CompletedTask; };
                client.MessageUpdated += (arg1, m2) => { MessageUpdated(arg1, m2); return Task.CompletedTask; };
                client.MessageDeleted += (arg1, arg2) => { MessageDeleted(arg1, arg2); return Task.CompletedTask; };
                client.UserBanned += (arg1, arg2) => { UserBanned(arg1, arg2); return Task.CompletedTask; };
                client.UserUnbanned += (arg1, arg2) => { UserUnbanned(arg1, arg2); return Task.CompletedTask; };
                client.UserPresenceUpdated += (arg1, arg2, arg3, arg4) => { UserPresenceUpdated(arg1, arg2, arg3, arg4); return Task.CompletedTask; };
                client.UserVoiceStateUpdated += (arg1, arg2, arg3) => { UserVoiceStateUpdated(arg1, arg2, arg3); return Task.CompletedTask; };
                client.ChannelCreated += arg => { ChannelCreated(arg); return Task.CompletedTask; };
                client.ChannelDestroyed += arg => { ChannelDestroyed(arg); return Task.CompletedTask; };
                client.ChannelUpdated += (arg1, arg2) => { ChannelUpdated(arg1, arg2); return Task.CompletedTask; };

                _log.Info($"Shard #{i} initialized.");
                client.Log += Client_Log;
                var j = i;
                client.Disconnected += (ex) =>
                {
                    _log.Error("Shard #{0} disconnected", j);
                    _log.Error(ex, ex?.Message ?? "No error");
                    return Task.CompletedTask;
                };
            }

            Clients = clientList.AsReadOnly();
        }

        private Task Client_Log(LogMessage arg)
        {
            _log.Warn(arg.Message);
            _log.Error(arg.Exception);
            return Task.CompletedTask;
        }

        public DiscordSocketClient MainClient =>
            Clients[0];

        public SocketSelfUser CurrentUser() =>
            Clients[0].CurrentUser;

        public IReadOnlyCollection<SocketGuild> GetGuilds() =>
            Clients.SelectMany(c => c.Guilds).ToList();

        public SocketGuild GetGuild(ulong id) =>
            Clients.Select(c => c.GetGuild(id)).FirstOrDefault(g => g != null);

        public Task<IDMChannel> GetDMChannelAsync(ulong channelId) =>
            Clients[0].GetDMChannelAsync(channelId);

        internal async Task LoginAsync(TokenType tokenType, string token)
        {
            foreach (var c in Clients)
            {
                await c.LoginAsync(tokenType, token).ConfigureAwait(false);
                _log.Info($"Shard #{c.ShardId} logged in.");
            }
        }

        internal async Task ConnectAsync()
        {

            foreach (var c in Clients)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    await c.ConnectAsync().ConfigureAwait(false);
                    sw.Stop();
                    _log.Info($"Shard #{c.ShardId} connected after {sw.Elapsed.TotalSeconds:F2}s ({++_connectedCount}/{Clients.Count})");
                }
                catch
                {
                    _log.Error($"Shard #{c.ShardId} FAILED CONNECTING.");
                    try { await c.ConnectAsync().ConfigureAwait(false); }
                    catch (Exception ex2)
                    {
                        _log.Error($"Shard #{c.ShardId} FAILED CONNECTING TWICE.");
                        _log.Error(ex2);
                    }
                }
            }
        }

        internal Task DownloadAllUsersAsync() =>
            Task.WhenAll(Clients.Select(async c =>
            {
                var sw = Stopwatch.StartNew();
                await c.DownloadAllUsersAsync().ConfigureAwait(false);
                sw.Stop();
                _log.Info($"Shard #{c.ShardId} downloaded {c.Guilds.Sum(g => g.Users.Count)} users after {sw.Elapsed.TotalSeconds:F2}s ({++_downloadedCount}/{Clients.Count}).");
            }));

        public Task SetGame(string game) => Task.WhenAll(Clients.Select(ms => ms.SetGameAsync(game)));


        public Task SetStream(string name, string url) => Task.WhenAll(Clients.Select(ms => ms.SetGameAsync(name, url, StreamType.NotStreaming)));

        public Task SetStatus(SettableUserStatus status) => Task.WhenAll(Clients.Select(ms => ms.SetStatusAsync(SettableUserStatusToUserStatus(status))));

        private static UserStatus SettableUserStatusToUserStatus(SettableUserStatus sus)
        {
            switch (sus)
            {
                case SettableUserStatus.Online:
                    return UserStatus.Online;
                case SettableUserStatus.Invisible:
                    return UserStatus.Invisible;
                case SettableUserStatus.Idle:
                    return UserStatus.AFK;
                case SettableUserStatus.Dnd:
                    return UserStatus.DoNotDisturb;
            }

            return UserStatus.Online;
        }
    }

    public enum SettableUserStatus
    {
        Online = 1,
        On = 1,
        Invisible = 2,
        Invis = 2,
        Idle = 3,
        Afk = 3,
        Dnd = 4,
        DoNotDisturb = 4,
        Busy = 4,
    }
}