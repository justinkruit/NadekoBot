using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NadekoBot.Services;
using NLog;

namespace NadekoBot.Modules
{
    public class DiscordModule : ModuleBase
    {
        protected Logger _log { get; }
        private string _prefix { get; }

        public DiscordModule()
        {
            string prefix;
            if (NadekoBot.ModulePrefixes.TryGetValue(this.GetType().Name, out prefix))
                _prefix = prefix;
            else
                _prefix = "?missing_prefix?";
            _log = LogManager.GetCurrentClassLogger();
        }
    }
}
