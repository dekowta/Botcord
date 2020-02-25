using Botcord.Audio;
using Botcord.Core;
using Botcord.Script;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Botcord.Discord
{

    public class DiscordScriptManager
    {
        private Dictionary<SocketGuild, DiscordScriptCollection> m_scriptCollections;
        private DiscordSocketClient m_client;

        private DiscordScriptCollection m_adminScriptCollection;

        private object m_mutex = new object();

        public DiscordScriptManager()
        {
            m_scriptCollections = new Dictionary<SocketGuild, DiscordScriptCollection>();            
        }

        public Task Initalise(DiscordSocketClient client)
        {
            if(client != m_client)
            {
                Logging.LogDebug(LogType.Discord, "Client Reset");
            }

            if (client != null)
            {
                Logging.Log(LogType.Discord, LogLevel.Debug, $"Initalising Client");


                //can fail if they havent already been registered
                try
                {
                    m_client.GuildAvailable -= client_GuildAvailable;
                    m_client.Ready -= client_Ready;
                }
                catch { }

                m_client = client;
                m_client.GuildAvailable += client_GuildAvailable;
                m_client.Ready          += client_Ready;
                if (m_adminScriptCollection == null)
                {
                    lock (m_mutex)
                    {
                        m_adminScriptCollection = new DiscordScriptCollection();
                        Utilities.ExecuteAndWait(async () => await LoadScripts(DiscordData.ScriptAdminFolder, m_adminScriptCollection));
                    }
                }
            }

            return Task.CompletedTask;
        }

        private Task client_Ready()
        {
            Logging.Log(LogType.Discord, LogLevel.Debug, $"Client Ready");
            return Task.CompletedTask;
        }

        private Task client_GuildAvailable(SocketGuild arg)
        {
            lock (m_mutex)
            {
                if (!m_scriptCollections.ContainsKey(arg))
                {
                    Logging.Log(LogType.Discord, LogLevel.Debug, $"Connected to guild {arg.Name}");

                    Utilities.Execute(() => LoadScriptCollection(arg));
                }
                else
                {
                    Logging.Log(LogType.Discord, LogLevel.Debug, $"Reconnected to guild {arg.Name}");

                    DiscordScriptCollection collection = m_scriptCollections[arg];
                    collection.Dispose();
                    collection = null;

                    m_scriptCollections.Remove(arg);

                    Utilities.Execute(() => LoadScriptCollection(arg));
                }
            }

            return Task.CompletedTask;
        }

        public Task Uninitalise()
        {
            Logging.Log(LogType.Discord, LogLevel.Debug, $"Uninitalising Client");

            return Task.CompletedTask;
        }

        public async Task<bool> LoadScriptCollection(SocketGuild guild)
        {
            if (!m_scriptCollections.ContainsKey(guild))
            {
                DiscordScriptCollection scriptCollection = new DiscordScriptCollection(guild);
                m_scriptCollections.Add(guild, scriptCollection);

                await LoadScripts(DiscordData.ScriptAdminFolder, scriptCollection);
                await LoadScripts(DiscordData.ScriptGlobalFolder, scriptCollection);
                string serverScripts = Path.Combine(DiscordData.ScriptFolder, guild.Id.ToString());
                await LoadScripts(serverScripts, scriptCollection);
            }

            return true;
        }

        public Task<bool> TryScriptCompile(string script)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    IEnumerable<IDiscordScript> scripts = null;
                    if (TryCompileScript(script, out scripts))
                    {
                        if (scripts.Count() > 0)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.LogException(LogType.Bot, ex, "Failed to load scripts");
                    return false;
                }

                return false;
            });
        }

        private Task LoadScripts(string folder, DiscordScriptCollection collection)
        {
            try
            {
                if(!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var csScripts = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories);
                foreach (string script in csScripts)
                {
                    IEnumerable<IDiscordScript> scripts = null;
                    if (TryCompileScript(script, out scripts))
                    {
                        foreach (IDiscordScript compiledScript in scripts)
                        {
                            DiscordScriptHost scriptHost = new DiscordScriptHost(m_client, compiledScript);
                            collection.Add(scriptHost);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Failed to load scripts");
            }

            return Task.CompletedTask;
        }

        private bool TryCompileScript(string script, out IEnumerable<IDiscordScript> scripts)
        {
            CompilerOptions options = new CompilerOptions();
            options.AddReference(Utilities.AssemblyLocation<Logging>());
            options.AddReference(Utilities.AssemblyLocation<IDiscordScript>());
            options.AddReference(Utilities.AssemblyLocation<MediaPlayer>());

#if DEBUG
            options.AddReferenceSearchFolder(Path.GetDirectoryName(typeof(IDiscordClient).GetTypeInfo().Assembly.Location)); //core
            options.AddReferenceSearchFolder(Path.GetDirectoryName(typeof(Discord​Socket​Client).GetTypeInfo().Assembly.Location)); //web socket
            options.AddReferenceSearchFolder(Path.GetDirectoryName(typeof(Discord​Rest​Client).GetTypeInfo().Assembly.Location)); //Rest
#endif
            options.AddReference("Discord.Net.Core.dll");
            options.AddReference("Discord.Net.WebSocket.dll");
            options.AddReference("Discord.Net.Rest.dll");

            return ScriptBuilder.Instance.TryBuildScript(script, options, out scripts);
        }
    }
}
