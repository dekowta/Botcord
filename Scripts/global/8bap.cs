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

        private string help = $"\nShake the 8bap to know the future." +
              "\n\nKnow Your Meme:" +
              "\nIf I remember correctly this was 8ball until an article about baps" +
              "\n(what the norf call a sandwich made with a bun) came up and Mohammed" +
              "\n'ironically' went !8bap and so it was change to 8bap to commemorate this moment.";

        public void Initalise(DiscordScriptHost ActiveHost)
        {
        }

        public void RegisterCommands(DiscordScriptHost ActiveHost)
        {
            ActiveHost.RegisterCommand("8bap", "version", help, Shake);
            ActiveHost.RegisterCommand("8bap", "really?", "really really Shake the 8bap", ReallyReallyShake);
            ActiveHost.RegisterCommand("8bap", "really", "really Shake the 8bap", ReallyShake);
            ActiveHost.RegisterCommand("8bap", "Shake the 8bap", Shake);
            
        }

        public void Dispose()
        {
        }

        public async void Shake(Dictionary<string, object> parameters, SocketMessage e)
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            int id = rand.Next(c_answers.Length);
            await e.Channel.SendMessageAsync("`" + c_answers[id] + "`");
        }

        public async void ReallyShake(Dictionary<string, object> parameters, SocketMessage e)
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            int id = rand.Next(c_answers.Length);
            await e.Channel.SendMessageAsync("`" + c_answers[id] + "`");
            id = rand.Next(c_answers.Length);
            await e.Channel.SendMessageAsync("`" + c_answers[id] + "`");
        }

        public async void ReallyReallyShake(Dictionary<string, object> parameters, SocketMessage e)
        {
            Random rand = new Random((int)DateTime.Now.Ticks);
            int id = rand.Next(c_answers.Length);
            await e.Channel.SendMessageAsync("`" + c_answers[id] + "`");
            id = rand.Next(c_answers.Length);
            await e.Channel.SendMessageAsync("`" + c_answers[id] + "`");
            id = rand.Next(c_answers.Length);
            await e.Channel.SendMessageAsync("`" + c_answers[id] + "`");
        }
    }
}
