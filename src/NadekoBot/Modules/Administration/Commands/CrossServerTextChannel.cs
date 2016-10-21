using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Services;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        public class CrossServerTextChannel : ModuleBase
        {

            public static readonly ConcurrentDictionary<int, ConcurrentHashSet<SocketTextChannel>> Subscribers = new ConcurrentDictionary<int, ConcurrentHashSet<SocketTextChannel>>();
            private static Logger _log { get; }

            static CrossServerTextChannel()
            {
                _log = LogManager.GetCurrentClassLogger();
                NadekoBot.Client.MessageReceived += (imsg) =>
                {
                    if (imsg.Author.IsBot)
                        return Task.CompletedTask;

                    var msg = imsg as SocketUserMessage;
                    if (msg == null)
                        return Task.CompletedTask;

                    var channel = msg.Channel as SocketTextChannel;
                    if (channel == null)
                        return Task.CompletedTask;

                    Task.Run(async () =>
                    {
                            if (msg.Author.Id == NadekoBot.Client.CurrentUser.Id) return;
                            foreach (var subscriber in Subscribers)
                            {
                                var set = subscriber.Value;
                                if (!set.Contains(msg.Channel))
                                    continue;
                                foreach (var chan in set.Except(new[] { channel }))
                                {
                                    try { await chan.SendMessageAsync(GetText(channel.Guild, channel, (IGuildUser)msg.Author, msg)).ConfigureAwait(false); } catch (Exception ex) { _log.Warn(ex); }
                                }
                            }
                    });
                    return Task.CompletedTask;
                };
            }

            private static string GetText(SocketGuild server, ITextChannel channel, IGuildUser user, IUserMessage message) =>
                $"**{server.Name} | {channel.Name}** `{user.Username}`: " + message.Content;

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [OwnerOnly]
            public async Task Scsc()
            {
                var channel = (SocketTextChannel)Context.Channel;

                var token = new NadekoRandom().Next();
                var set = new ConcurrentHashSet<SocketTextChannel>();
                if (Subscribers.TryAdd(token, set))
                {
                    set.Add(channel);
                    await ((SocketGuildUser)Context.Message.Author).SendMessageAsync("This is your CSC token:" + token.ToString()).ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task Jcsc(int token)
            {
                var channel = (SocketTextChannel)Context.Channel;

                ConcurrentHashSet<SocketTextChannel> set;
                if (!Subscribers.TryGetValue(token, out set))
                    return;
                set.Add(channel);
                await channel.SendMessageAsync(":ok:").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task Lcsc()
            {
                var channel = (SocketTextChannel)Context.Channel;

                foreach (var subscriber in Subscribers)
                {
                    subscriber.Value.TryRemove(channel);
                }
                await channel.SendMessageAsync(":ok:").ConfigureAwait(false);
            }
        }
    }
}