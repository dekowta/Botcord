using Botcord.Audio;
using Botcord.Core;
using Botcord.Script;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Botcord.Discord
{

    public class DiscordScriptManager
    {
        private ConcurrentDictionary<ulong, DiscordScriptCollection> m_scriptCollections;
        private DiscordSocketClient m_client;

        private DiscordScriptCollection m_adminScriptCollection;

        private object m_mutex = new object();

        public DiscordScriptManager()
        {
            m_scriptCollections = new ConcurrentDictionary<ulong, DiscordScriptCollection>();            
        }

        public async Task Initalise(DiscordSocketClient client)
        {
            if(client != m_client)
            {
                Logging.LogDebug(LogType.Bot, "Client Reset");
            }
            else
            {
                Logging.LogDebug(LogType.Bot, "Client Reset but returned the same client will rehook");
            }

            if (client != null)
            {

                Logging.LogDebug(LogType.Bot, $"Initalising Client");

                //can fail if they havent already been registered
                try
                {
                    m_client.GuildAvailable -= client_GuildAvailable;
                    m_client.Ready -= client_Ready;
                }
                catch { }

                m_client = client;
                m_client.GuildAvailable += client_GuildAvailable;
                m_client.Ready += client_Ready;

                if (m_adminScriptCollection != null)
                {
                    m_adminScriptCollection.Dispose();
                }

                Logging.LogDebug(LogType.Bot, $"Creating Admin script collection");
                m_adminScriptCollection = new DiscordScriptCollection();
                await LoadScripts(DiscordData.ScriptAdminFolder, m_adminScriptCollection);      
            }
        }

        public Task Uninitalise()
        {
            lock (m_mutex)
            {
                Logging.LogDebug(LogType.Bot, $"Uninitalising Client");
                foreach (var scriptCollection in m_scriptCollections)
                {
                    Logging.LogDebug(LogType.Bot, $"Disposing of script from guild {scriptCollection.Key}");
                    scriptCollection.Value.Dispose();
                }
            }

            return Task.CompletedTask;
        }

        private Task client_Ready()
        {
            Logging.LogDebug(LogType.Discord, $"Client Ready");
            return Task.CompletedTask;
        }

        private Task client_GuildAvailable(SocketGuild arg)
        {
            Logging.LogDebug(LogType.Discord, $"Guild Available {arg.Name}");

            if (!m_scriptCollections.ContainsKey(arg.Id))
            {
                Logging.LogDebug(LogType.Bot, $"Connected to guild {arg.Name}");
            }
            else
            {
                Logging.LogDebug(LogType.Bot,$"Reconnected to guild {arg.Name}");

                DiscordScriptCollection collection;
                if (!m_scriptCollections.TryRemove(arg.Id, out collection))
                {
                    Logging.LogError(LogType.Bot, $"Failed to remove previous instance of guild {arg.Name} attempting to dispose any way.");
                    collection = m_scriptCollections[arg.Id];
                }

                if (collection != null)
                {
                    Logging.LogDebug(LogType.Bot, $"Disposing of collection for guild {arg.Name}");
                    collection.Dispose();
                }    
            }

            Logging.LogDebug(LogType.Bot, $"Starting script loading for guild {arg.Name}");
            Utilities.Execute(() => LoadScriptCollection(arg));

            return Task.CompletedTask;
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

        private async Task<bool> LoadScriptCollection(SocketGuild guild)
        {
            Logging.LogDebug(LogType.Bot, $"Loading scripts for guild {guild.Name}");

            if (!m_scriptCollections.ContainsKey(guild.Id))
            {
                DiscordScriptCollection scriptCollection = new DiscordScriptCollection(guild);
                if (!m_scriptCollections.TryAdd(guild.Id, scriptCollection))
                {
                    Logging.LogError(LogType.Bot, $"Failed to add guild {guild.Name} to script collection will be ignored");
                    return false;
                }

                Logging.LogDebug(LogType.Bot, $"Starting Loading scripts for guild {guild.Name}");

                await LoadScripts(DiscordData.ScriptAdminFolder, scriptCollection);
                await LoadScripts(DiscordData.ScriptGlobalFolder, scriptCollection);
                string serverScripts = Path.Combine(DiscordData.ScriptFolder, guild.Id.ToString());
                await LoadScripts(serverScripts, scriptCollection);
            }
            else
            {
                DiscordScriptCollection scriptCollection;
                if (m_scriptCollections.TryGetValue(guild.Id, out scriptCollection))
                {
                    Logging.LogDebug(LogType.Bot, $"Starting Reloading scripts for guild {guild.Name}");

                    await LoadScripts(DiscordData.ScriptAdminFolder, scriptCollection);
                    await LoadScripts(DiscordData.ScriptGlobalFolder, scriptCollection);
                    string serverScripts = Path.Combine(DiscordData.ScriptFolder, guild.Id.ToString());
                    await LoadScripts(serverScripts, scriptCollection);
                }
                else
                {
                    Logging.LogError(LogType.Bot, $"Failed to get script collection for guild {guild.Name} no script will be loaded");
                    return false;
                }
            }

            Logging.LogDebug(LogType.Bot, $"Finished Loading scripts for guild {guild.Name}");

            return true;
        }

        private Task LoadScripts(string folder, DiscordScriptCollection collection)
        {
            Logging.LogDebug(LogType.Bot, $"Loading scripts from folder '{folder}'");

            try
            {
                if (!Directory.Exists(folder))
                {
                    Logging.LogDebug(LogType.Bot, $"Folder '{folder}' doesnt exist creating folder");
                    Directory.CreateDirectory(folder);
                }
                else
                {
                    var csScripts = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories);
                    Logging.LogDebug(LogType.Bot, $"Found '{csScripts.Count()}' scripts to compile");
                    foreach (string script in csScripts)
                    {
                        IEnumerable<IDiscordScript> scripts = null;
                        if (TryCompileScript(script, out scripts))
                        {
                            Logging.LogDebug(LogType.Bot, $"Build '{scripts.Count()}' scripts from file '{script}'");
                            foreach (IDiscordScript compiledScript in scripts)
                            {
                                Logging.LogDebug(LogType.Bot, $"Adding script '{compiledScript.Name}' to collection'");
                                DiscordScriptHost scriptHost = new DiscordScriptHost(m_client, compiledScript);
                                collection.Add(scriptHost);
                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.LogException(LogType.Bot, ex, "Failed to load scripts");
            }

            Logging.LogDebug(LogType.Bot, $"Finished Loading scripts from folder '{folder}'");

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
