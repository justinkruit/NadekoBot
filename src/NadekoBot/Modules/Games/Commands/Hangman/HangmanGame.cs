﻿using Discord;
using Discord.WebSocket;
using NadekoBot.Extensions;
using NadekoBot.Services;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Games.Commands.Hangman
{
    public class HangmanModel
    {
        public List<HangmanObject> All { get; set; }
        public List<HangmanObject> Animals { get; set; }
        public List<HangmanObject> Countries { get; set; }
        public List<HangmanObject> Movies { get; set; }
        public List<HangmanObject> Things { get; set; }
    }

    public class HangmanTermPool
    {
        public enum HangmanTermType
        {
            All,
            Animals,
            Countries,
            Movies,
            Things
        }

        const string termsPath = "data/hangman.json";
        public static HangmanModel data { get; }
        static HangmanTermPool()
        {
            try
            {
                data = JsonConvert.DeserializeObject<HangmanModel>(File.ReadAllText(termsPath));
                data.All = data.Animals.Concat(data.Countries)
                                       .Concat(data.Movies)
                                       .Concat(data.Things)
                                       .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        public static HangmanObject GetTerm(HangmanTermType type)
        {
            var rng = new NadekoRandom();
            switch (type)
            {
                case HangmanTermType.Animals:
                    return data.Animals[rng.Next(0, data.Animals.Count)];
                case HangmanTermType.Countries:
                    return data.Countries[rng.Next(0, data.Countries.Count)];
                case HangmanTermType.Movies:
                    return data.Movies[rng.Next(0, data.Movies.Count)];
                case HangmanTermType.Things:
                    return data.Things[rng.Next(0, data.Things.Count)];
                default:
                    return data.All[rng.Next(0, data.All.Count)];
            }

        }
    }

    public class HangmanGame
    {
        private readonly Logger _log;

        public IMessageChannel GameChannel { get; }
        public HashSet<char> Guesses { get; } = new HashSet<char>();
        public HangmanObject Term { get; private set; }
        public uint Errors { get; private set; } = 0;
        public uint MaxErrors { get; } = 6;
        public uint MessagesSinceLastPost { get; private set; } = 0;
        public string ScrambledWord => "`" + String.Concat(Term.Word.Select(c =>
         {
             if (!(char.IsLetter(c) || char.IsDigit(c)))
                 return $" {c}";

             c = char.ToUpperInvariant(c);

             if (c == ' ')
                 return "   ";
             return Guesses.Contains(c) ? $" {c}" : " _";
         })) + "`";

        public bool GuessedAll => Guesses.IsSupersetOf(Term.Word.ToUpperInvariant()
                                                           .Where(c => char.IsLetter(c) || char.IsDigit(c)));

        public HangmanTermPool.HangmanTermType TermType { get; }

        public event Action<HangmanGame> OnEnded;

        public HangmanGame(IMessageChannel channel, HangmanTermPool.HangmanTermType type)
        {
            _log = LogManager.GetCurrentClassLogger();
            this.GameChannel = channel;
            this.TermType = type;
        }

        public void Start()
        {
            this.Term = HangmanTermPool.GetTerm(TermType);
            // start listening for answers when game starts
            NadekoBot.Client.MessageReceived += PotentialGuess;
        }

        public async Task End()
        {
            NadekoBot.Client.MessageReceived -= PotentialGuess;
            OnEnded(this);
            var toSend = "Game ended. You **" + (Errors >= MaxErrors ? "LOSE" : "WIN") + "**!\n" + GetHangman();
            var embed = new EmbedBuilder().WithTitle("Hangman Game")
                                          .WithDescription(toSend)
                                          .AddField(efb => efb.WithName("It was").WithValue(Term.Word))
                                          .WithImageUrl(Term.ImageUrl)
                                          .WithFooter(efb => efb.WithText(string.Join(" ", Guesses)));
            if (Errors >= MaxErrors)
                await GameChannel.EmbedAsync(embed.WithErrorColor()).ConfigureAwait(false);
            else
                await GameChannel.EmbedAsync(embed.WithOkColor()).ConfigureAwait(false);
        }

        private async void PotentialGuess(SocketMessage msg)
        {
            try
            {
                if (!(msg is SocketUserMessage))
                    return;

                if (msg.Channel != GameChannel)
                    return; // message's channel has to be the same as game's
                if (msg.Content.Length == 1) // message must be 1 char long
                {
                    if (++MessagesSinceLastPost > 10)
                    {
                        MessagesSinceLastPost = 0;
                        try
                        {
                            await GameChannel.SendConfirmAsync("Hangman Game",
                                ScrambledWord + "\n" + GetHangman(),
                                footer: string.Join(" ", Guesses)).ConfigureAwait(false);
                        }
                        catch { }
                    }

                    if (!(char.IsLetter(msg.Content[0]) || char.IsDigit(msg.Content[0])))// and a letter or a digit
                        return;

                    var guess = char.ToUpperInvariant(msg.Content[0]);
                    if (Guesses.Contains(guess))
                    {
                        MessagesSinceLastPost = 0;
                        ++Errors;
                        if (Errors < MaxErrors)
                            await GameChannel.SendErrorAsync("Hangman Game", $"{msg.Author.Mention} Letter `{guess}` has already been used.\n" + ScrambledWord + "\n" + GetHangman(),
                                footer: string.Join(" ", Guesses)).ConfigureAwait(false);
                        else
                            await End().ConfigureAwait(false);
                        return;
                    }

                    Guesses.Add(guess);

                    if (Term.Word.ToUpperInvariant().Contains(guess))
                    {
                        if (GuessedAll)
                        {
                            try { await GameChannel.SendConfirmAsync("Hangman Game", $"{msg.Author.Mention} guessed a letter `{guess}`!").ConfigureAwait(false); } catch { }

                            await End().ConfigureAwait(false);
                            return;
                        }
                        MessagesSinceLastPost = 0;
                        try
                        {
                            await GameChannel.SendConfirmAsync("Hangman Game", $"{msg.Author.Mention} guessed a letter `{guess}`!\n" + ScrambledWord + "\n" + GetHangman(),
                          footer: string.Join(" ", Guesses)).ConfigureAwait(false);
                        }
                        catch { }

                    }
                    else
                    {
                        MessagesSinceLastPost = 0;
                        ++Errors;
                        if (Errors < MaxErrors)
                            await GameChannel.SendErrorAsync("Hangman Game", $"{msg.Author.Mention} Letter `{guess}` does not exist.\n" + ScrambledWord + "\n" + GetHangman(),
                                footer: string.Join(" ", Guesses)).ConfigureAwait(false);
                        else
                            await End().ConfigureAwait(false);
                    }

                }
            }
            catch (Exception ex) { _log.Warn(ex); }
        }

        public string GetHangman() => $@"\_\_\_\_\_\_\_\_\_
      |           |
      |           |
   {(Errors > 0 ? "😲" : "      ")}        |
   {(Errors > 1 ? "/" : "  ")} {(Errors > 2 ? "|" : "  ")} {(Errors > 3 ? "\\" : "  ")}       | 
    {(Errors > 4 ? "/" : "  ")} {(Errors > 5 ? "\\" : "  ")}        |
               /-\";
    }
}