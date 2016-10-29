using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using NadekoBot.Extensions;
using NadekoBot.Services;
using NadekoBot.Services.Database;
using NadekoBot.Services.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Utility
{
    public partial class Utility
    {
        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ListQuotes(, int page = 1)
        {
            var channel = Context.Channel;

            page -= 1;

            if (page < 0)
                return;

            IEnumerable<Quote> quotes;
            using (var uow = DbHandler.UnitOfWork())
            {
                quotes = uow.Quotes.GetGroup(page * 16, 16);
            }

            if (quotes.Any())
                await channel.SendMessageAsync($"`Page {page + 1} of quotes:`\n```xl\n" + String.Join("\n", quotes.Select((q) => $"{q.Keyword,-20} by {q.AuthorName}")) + "\n```")
                             .ConfigureAwait(false);
            else
                await channel.SendMessageAsync("`No quotes on this page.`").ConfigureAwait(false);
        }

        [NadekoCommand, Usage, Description, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task ShowQuote([Remainder] string keyword)
        {
            var channel = (SocketTextChannel)Context.Channel;
            var channel = (ITextChannel)umsg.Channel;

                if (string.IsNullOrWhiteSpace(keyword))
                    return;

                keyword = keyword.ToUpperInvariant();

                Quote quote;
                using (var uow = DbHandler.Instance.GetUnitOfWork())
                {
                    quote = await uow.Quotes.GetRandomQuoteByKeywordAsync(channel.Guild.Id, keyword).ConfigureAwait(false);
                }

                if (quote == null)
                    return;

                await channel.SendMessageAsync("📣 " + quote.Text.SanitizeMentions());
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task AddQuote(string keyword, [Remainder] string text)
            {
                var channel = (SocketTextChannel)Context.Channel;

                if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(text))
                    return;

                keyword = keyword.ToUpperInvariant();

                using (var uow = DbHandler.UnitOfWork())
                {
                    uow.Quotes.Add(new Quote
                    {
                        AuthorId = Context.User.Id,
                        AuthorName = Context.User.Username,
                        GuildId = channel.Guild.Id,
                        Keyword = keyword,
                        Text = text,
                    });
                    await uow.CompleteAsync().ConfigureAwait(false);
                }
                await channel.SendMessageAsync("`Quote added.`").ConfigureAwait(false);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task DeleteQuote([Remainder] string keyword)
            {
                var channel = (SocketTextChannel)Context.Channel;

                if (string.IsNullOrWhiteSpace(keyword))
                    return;

                var isAdmin = ((IGuildUser)Context.User).GuildPermissions.Administrator;

                keyword = keyword.ToUpperInvariant();
                string response;
                using (var uow = DbHandler.UnitOfWork())
                {
                    var qs = uow.Quotes.GetAllQuotesByKeyword(channel.Guild.Id, keyword);

                    if (qs == null || !qs.Any())
                    {
                        response = "`No quotes found.`";
                        return;
                    }

                    var q = qs.Shuffle().FirstOrDefault(elem => isAdmin || elem.AuthorId == Context.User.Id);

                    uow.Quotes.Remove(q);
                    await uow.CompleteAsync().ConfigureAwait(false);
                    response = "`Deleted a random quote`";
                }
                await channel.SendMessageAsync(response);
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.Administrator)]
            public async Task DelAllQuotes([Remainder] string keyword)
            {
                var channel = (SocketTextChannel)Context.Channel;

                if (string.IsNullOrWhiteSpace(keyword))
                    return;

                keyword = keyword.ToUpperInvariant();

                using (var uow = DbHandler.UnitOfWork())
                {
                    var quotes = uow.Quotes.GetAllQuotesByKeyword(channel.Guild.Id, keyword);

                    uow.Quotes.RemoveRange(quotes.ToArray());//wtf?!

                    await uow.CompleteAsync();
                }

                await channel.SendMessageAsync($"`Deleted all quotes with '{keyword}' keyword`");
            }
        }
    }
}