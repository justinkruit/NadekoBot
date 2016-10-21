using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Attributes;
using NadekoBot.Modules.Searches.Models;
using NadekoBot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Searches
{
    public partial class Searches
    {
        [Group]
        public class JokeCommands : ModuleBase
        {
            private static List<WoWJoke> wowJokes { get; } = new List<WoWJoke>();
            private static List<MagicItem> magicItems { get; } = new List<MagicItem>();
            private static Logger _log { get; }

            static JokeCommands()
            {
                _log = LogManager.GetCurrentClassLogger();
                if (File.Exists("data/wowjokes.json"))
                {
                    wowJokes = JsonConvert.DeserializeObject<List<WoWJoke>>(File.ReadAllText("data/wowjokes.json"));
                }
                else
                    _log.Warn("data/wowjokes.json is missing. WOW Jokes are not loaded.");

                if (File.Exists("data/magicitems.json"))
                {
                    magicItems = JsonConvert.DeserializeObject<List<MagicItem>>(File.ReadAllText("data/magicitems.json"));
                }
                else
                    _log.Warn("data/magicitems.json is missing. Magic items are not loaded.");
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Yomama()
            {
                var channel = (SocketTextChannel)Context.Channel;
                using (var http = new HttpClient())
                {
                    var response = await http.GetStringAsync("http://api.yomomma.info/").ConfigureAwait(false);
                    await channel.SendMessageAsync("`" + JObject.Parse(response)["joke"].ToString() + "` 😆").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task Randjoke()
            {
                var channel = (SocketTextChannel)Context.Channel;
                using (var http = new HttpClient())
                {
                    var response = await http.GetStringAsync("http://tambal.azurewebsites.net/joke/random").ConfigureAwait(false);
                    await channel.SendMessageAsync("`" + JObject.Parse(response)["joke"].ToString() + "` 😆").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task ChuckNorris()
            {
                var channel = (SocketTextChannel)Context.Channel;
                using (var http = new HttpClient())
                {
                    var response = await http.GetStringAsync("http://api.icndb.com/jokes/random/").ConfigureAwait(false);
                    await channel.SendMessageAsync("`" + JObject.Parse(response)["value"]["joke"].ToString() + "` 😆").ConfigureAwait(false);
                }
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task WowJoke()
            {
                var channel = (SocketTextChannel)Context.Channel;

                if (!wowJokes.Any())
                {
                }
                await channel.SendMessageAsync(wowJokes[new NadekoRandom().Next(0, wowJokes.Count)].ToString());
            }

            [NadekoCommand, Usage, Description, Aliases]
            [RequireContext(ContextType.Guild)]
            public async Task MagicItem()
            {
                var channel = (SocketTextChannel)Context.Channel;
                var rng = new NadekoRandom();
                var item = magicItems[rng.Next(0, magicItems.Count)].ToString();

                await channel.SendMessageAsync(item).ConfigureAwait(false);
            }
        }
    }
}
