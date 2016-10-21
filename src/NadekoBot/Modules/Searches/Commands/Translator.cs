using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using System;
using System.Threading.Tasks;
using NadekoBot.Services;
using Discord.WebSocket;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Translate(string langs, [Remainder] string text = null)
        {
            var channel = (SocketTextChannel)Context.Channel;

            try
            {
                var langarr = langs.ToLowerInvariant().Split('>');
                if (langarr.Length != 2)
                    return;
                string from = langarr[0];
                string to = langarr[1];
                text = text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                await Context.Channel.TriggerTypingAsync().ConfigureAwait(false);
                string translation = await GoogleTranslator.Instance.Translate(text, from, to).ConfigureAwait(false);
                await channel.SendMessageAsync(translation).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                await channel.SendMessageAsync("Bad input format, or something went wrong...").ConfigureAwait(false);
            }
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Translangs()
        {
            var channel = (SocketTextChannel)Context.Channel;

            await channel.SendTableAsync(GoogleTranslator.Instance.Languages, str => $"{str,-15}", columns: 3);
        }

    }
}
