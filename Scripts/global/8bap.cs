using Botcord.Discord;
using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Scripts.global
{
    public class bap : IDiscordScript
    {
        const int max = 20;

        string[] c_answers = {

            "It is certain",
            "It is decidedly so",
            "Without a doubt",
            "Yes, definitely",
            "You may rely on it",
            "As I see it, yes",
            "Most likely",
            "Outlook good",
            "Yes",
            "Signs point to yes",
            "Reply hazy try again",
            "Ask again later",
            "Better not tell you now",
            "Cannot predict now",
            "Concentrate and ask again",
            "Don't count on it",
            "My reply is no",
            "My sources say no",
            "Outlook not so good",
            "Very doubtful"

        };

        public string Name => "8bap 1.0";

        public void Initalise(DiscordScriptHost ActiveHost)
        {
        }

        public void RegisterCommands(DiscordScriptHost ActiveHost)
        {
            ActiveHost.RegisterCommand("8bap", "Shake the 8bap", Shake);
        }

        public void Dispose()
        {
        }

        public async void Shake(Dictionary<string, object> parameters, SocketMessage e)
        {
            Random rand = new Random();
            int id = rand.Next(max);
            await e.Channel.SendMessageAsync("The GODLY 8bap says `" + c_answers[id] + "`");
        }        
    }
}
