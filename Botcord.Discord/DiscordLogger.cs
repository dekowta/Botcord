using Botcord.Core;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using Botcord.Core.DiscordExtensions;
using Botcord.Core.Extensions;
using System.Threading.Tasks;
using Discord;

namespace Botcord.Discord
{
    public class DiscordLogger : Singleton<DiscordLogger>
    {
        private const string c_exceptionChannel = "exception";
        private const string c_errorChannel = "error";
        private const string c_debugChannel = "debug";

        private SocketTextChannel m_debugChannel = null;
        private SocketTextChannel m_errorChannel = null;
        private SocketTextChannel m_exceptionChannel = null;
        private ulong m_guildId = 0;


        public DiscordLogger()
        {
            Logging.OnLog += Logging_OnLog;
        }

        public void Initalise(DiscordSocketClient client, ulong guildId)
        { 
            m_guildId = guildId;
            client.GuildAvailable -= Client_GuildAvailable;
            client.GuildAvailable += Client_GuildAvailable;
        }

        private Task Client_GuildAvailable(SocketGuild arg)
        {
            if (arg.Id == m_guildId)
            {
                if(TryFindChannel(arg, c_exceptionChannel, out m_exceptionChannel))
                {
                    Utilities.Execute(async (token) =>
                    {
                        await m_exceptionChannel.SendMessageAsync($"Exception channel set to `{m_exceptionChannel.Name}` (`{m_exceptionChannel.Id}`) on server `{arg.Name}` (`{arg.Id}`).");
                    }, 1.Minute());
                }

                if (TryFindChannel(arg, c_errorChannel, out m_errorChannel))
                {
                    Utilities.Execute(async (token) =>
                    {
                        await m_errorChannel.SendMessageAsync($"Error channel set to `{m_errorChannel.Name}` (`{m_errorChannel.Id}`) on server `{arg.Name}` (`{arg.Id}`).");
                    }, 1.Minute());
                }

                if (TryFindChannel(arg, c_debugChannel, out m_debugChannel))
                {
                    Utilities.Execute(async (token) =>
                    {
                        await m_debugChannel.SendMessageAsync($"Debug channel set to `{m_debugChannel.Name}` (`{m_debugChannel.Id}`) on server `{arg.Name}` (`{arg.Id}`).");
                    }, 1.Minute());
                }
            }

            return Task.CompletedTask;
        }

        private void Logging_OnLog(LogLevel level, bool isException, string message)
        {
            if (DiscordHost.Instance.Client.LoginState != LoginState.LoggedIn) return;

            if (isException && m_debugChannel != null)
            {
                IEnumerable<string> msg = EnsureMessageSize(message);
                foreach (var messageItem in msg)
                {
                    Utilities.Execute(async (token) =>
                    {
                        await m_exceptionChannel.SendMessageAsync(messageItem);
                        await Task.Delay(500);
                    }, 3.Minute());
                }
            }
            else if (level == LogLevel.Error && m_errorChannel != null)
            {
                IEnumerable<string> msg = EnsureMessageSize(message);
                foreach (var messageItem in msg)
                {
                    Utilities.Execute(async (token) =>
                    {
                        await m_errorChannel.SendMessageAsync(messageItem);
                    }, 1.Minute());
                }
            }
            else if(level == LogLevel.Debug && m_debugChannel != null)
            {
                if(message.Contains($"Rate limit triggered: channels/{m_debugChannel.Id}/messages"))
                {
                    return;
                }

                IEnumerable<string> msg = EnsureMessageSize(message);
                foreach (var messageItem in msg)
                {
                    Utilities.Execute(async (token) =>
                    {
                        await m_debugChannel.SendMessageAsync(messageItem);
                    }, 1.Minute());
                }
            }
        }

        private IEnumerable<string> EnsureMessageSize(string message, int sizeLimit = 2000)
        {
            List<string> messages = new List<string>();
            if (message.Length > sizeLimit)
            {
                messages = message.SplitAndWrapString("\n", sizeLimit);
            }
            else
            {
                messages.Add(message);
            }

            return messages;
        }

        private bool TryFindChannel(SocketGuild guild, string name, out SocketTextChannel channel)
        {
            channel = null;
            SocketTextChannel foundChannel = guild.GetTextChannel(name);
            if (foundChannel != null)
            {
                channel = foundChannel;
            }
            else
            {
                channel = guild.GetTextChannel();
            }

            if (channel != null)
            {
                Logging.LogInfo(LogType.Bot, $"{name} channel set to {channel.Name} ({channel.Id}) on server {guild.Name} ({guild.Id}).");
                return true;
            }
            else
            {
                Logging.LogError(LogType.Bot, $"Failed to find {name} channel to log exceptions to.");
                return false;
            }
        }
    }
}
