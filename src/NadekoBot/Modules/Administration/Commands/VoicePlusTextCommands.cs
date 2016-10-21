using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Services;
using NadekoBot.Services.Database;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        public class VoicePlusTextCommands : ModuleBase
        {
            private static Regex channelNameRegex { get; } = new Regex(@"[^a-zA-Z0-9 -]", RegexOptions.Compiled);
            
            private static ConcurrentHashSet<ulong> voicePlusTextCache { get; }

            static VoicePlusTextCommands()
            {
                using (var uow = DbHandler.UnitOfWork())
                {
                    voicePlusTextCache = new ConcurrentHashSet<ulong>(uow.GuildConfigs.GetAll().Where(g => g.VoicePlusTextEnabled).Select(g => g.GuildId));
                }
                NadekoBot.Client.UserVoiceStateUpdated += UserUpdatedEventHandler;
            }

            private static Task UserUpdatedEventHandler(SocketUser iuser, SocketVoiceState before, SocketVoiceState after)
            {
                var user = (iuser as SocketGuildUser);
                var guild = user?.Guild;

                if (guild == null)
                    return Task.CompletedTask;
                var task = Task.Run(async () =>
                {
                    var botUserPerms = guild.CurrentUser.GuildPermissions;
                    try
                    {
                        if (before.VoiceChannel == after.VoiceChannel) return;
                        
                        if (!voicePlusTextCache.Contains(guild.Id))
                            return;

                        if (!botUserPerms.ManageChannels || !botUserPerms.ManageRoles)
                        {
                            try
                            {
                                await (await guild.GetOwnerAsync()).SendMessageAsync(
                                    "I don't have manage server and/or Manage Channels permission," +
                                    $" so I cannot run voice+text on **{guild.Name}** server.").ConfigureAwait(false);
                            }
                            catch { }
                            using (var uow = DbHandler.UnitOfWork())
                            {
                                uow.GuildConfigs.For(guild.Id).VoicePlusTextEnabled = false;
                                voicePlusTextCache.TryRemove(guild.Id);
                                await uow.CompleteAsync().ConfigureAwait(false);
                            }
                            return;
                        }


                        var beforeVch = before.VoiceChannel;
                        if (beforeVch != null)
                        {
                            var textChannel = guild.GetTextChannels().Where(t => t.Name == GetChannelName(beforeVch.Name)).FirstOrDefault();
                            if (textChannel != null)
                                await textChannel.AddPermissionOverwriteAsync(user,
                                    new OverwritePermissions(readMessages: PermValue.Deny,
                                                       sendMessages: PermValue.Deny)).ConfigureAwait(false);
                        }
                        var afterVch = after.VoiceChannel;
                        if (afterVch != null && guild.AFKChannelId != afterVch.Id)
                        {
                            ITextChannel textChannel = guild.GetTextChannels()
                                                        .Where(t => t.Name ==  GetChannelName(afterVch.Name))
                                                        .FirstOrDefault();
                            if (textChannel == null)
                            {
                                textChannel = (await guild.CreateTextChannelAsync(GetChannelName(afterVch.Name)).ConfigureAwait(false));
                                await textChannel.AddPermissionOverwriteAsync(guild.EveryoneRole,
                                    new OverwritePermissions(readMessages: PermValue.Deny,
                                                       sendMessages: PermValue.Deny)).ConfigureAwait(false);
                            }
                            await textChannel.AddPermissionOverwriteAsync(user,
                                new OverwritePermissions(readMessages: PermValue.Allow,
                                                        sendMessages: PermValue.Allow)).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                });
                return Task.CompletedTask;
            }

            private static string GetChannelName(string voiceName) =>
                channelNameRegex.Replace(voiceName, "").Trim().Replace(" ", "-").TrimTo(90, true) + "-voice";

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageRoles)]
            [RequirePermission(GuildPermission.ManageChannels)]
            public async Task VoicePlusText()
            {
                var channel = (SocketTextChannel)Context.Channel;
                var guild = channel.Guild;

                var botUser = guild.CurrentUser;
                if (!botUser.GuildPermissions.ManageRoles || !botUser.GuildPermissions.ManageChannels)
                {
                    await channel.SendMessageAsync(":anger: `I require manage roles and manage channels permissions to enable this feature.`");
                    return;
                }
                try
                {
                    bool isEnabled;
                    using (var uow = DbHandler.UnitOfWork())
                    {
                        var conf = uow.GuildConfigs.For(guild.Id);
                        isEnabled = conf.VoicePlusTextEnabled = !conf.VoicePlusTextEnabled;
                        await uow.CompleteAsync().ConfigureAwait(false);
                    }
                    voicePlusTextCache.Add(guild.Id);
                    if (!isEnabled)
                    {
                        foreach (var textChannel in guild.GetTextChannels().Where(c => c.Name.EndsWith("-voice")))
                        {
                            try { await textChannel.DeleteAsync().ConfigureAwait(false); } catch { }
                        }
                        await channel.SendMessageAsync("Successfuly removed voice + text feature.").ConfigureAwait(false);
                        return;
                    }
                    await channel.SendMessageAsync("Successfuly enabled voice + text feature.").ConfigureAwait(false);

                }
                catch (Exception ex)
                {
                    await channel.SendMessageAsync(ex.ToString()).ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageChannels)]
            [RequirePermission(GuildPermission.ManageRoles)]
            public async Task CleanVPlusT()
            {
                var channel = (SocketTextChannel)Context.Channel;

                var guild = channel.Guild;
                if (!guild.CurrentUser.GuildPermissions.ManageChannels)
                {
                    await channel.SendMessageAsync("`I have insufficient permission to do that.`").ConfigureAwait(false);
                    return;
                }

                var allTxtChannels = guild.GetTextChannels().Where(c => c.Name.EndsWith("-voice"));
                var validTxtChannelNames = guild.GetVoiceChannels().Select(c => GetChannelName(c.Name));

                var invalidTxtChannels = allTxtChannels.Where(c => !validTxtChannelNames.Contains(c.Name));

                foreach (var c in invalidTxtChannels)
                {
                    try { await c.DeleteAsync().ConfigureAwait(false); } catch { }
                    await Task.Delay(500);
                }

                await channel.SendMessageAsync("`Done.`").ConfigureAwait(false);
            }
        }
    }
}