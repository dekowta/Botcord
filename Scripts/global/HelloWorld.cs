using Botcord.Discord;
using Botcord.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime;
using Discord.WebSocket;

//*ASM:
namespace DiscordSharp.CSharp
{
    public class blank : IDiscordScript
    {
        public string Name
        {
            get { return "Hello World Test"; }
        }

        public void Initalise(DiscordScriptHost ActiveHost)
        {

        }

        public void RegisterCommands(DiscordScriptHost ActiveHost)
        {
            ActiveHost.RegisterCommand("hello", "test", MessageRecieved);
        }

        public void Dispose()
        {

        }

        public void MessageRecieved(Dictionary<string, object> parameters, SocketMessage e)
        {
            Logging.LogInfo(LogType.Script, "Hello World");

            e.Channel.SendMessageAsync("Hello world");
        }        
    }
}