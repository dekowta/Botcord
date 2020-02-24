using Botcord.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Botcord.Discord
{
    public static class DiscordData
    {
        public static string ScriptFolder
        {
            get { return Path.Combine(Utilities.AssemblyPath, "scripts"); }
        }


        public static string ScriptAdminFolder
        {
            get { return Path.Combine(ScriptFolder, "admin"); }
        }

        public static string ScriptGlobalFolder
        {
            get { return Path.Combine(ScriptFolder, "global"); }
        }
    }
}
