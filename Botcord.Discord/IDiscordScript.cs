using System;
using System.Collections.Generic;
using System.Text;

namespace Botcord.Discord
{
    public interface IDiscordScript
    {
        string Name { get;  }

        void Initalise(DiscordScriptHost ActiveHost);
        void RegisterCommands(DiscordScriptHost ActiveHost);
        void Dispose();
    }
}
